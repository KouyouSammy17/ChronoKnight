using Cysharp.Threading.Tasks;
using DG.Tweening;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using System;
using System.IO;
using System.Threading;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
// using UnityEngine.Timeline; // not needed here

public enum GameState { Title, Playing, Clear, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ───────────────────────────────────────────────────────────────────────────────
    // ACTION MAP CONSTANTS (avoid string typos)
    const string MAP_PLAYER = "Player";
    const string MAP_UI = "UI";

    [Header("Scene Names")]
    [SerializeField] private string _titleScene = "Title";
    [SerializeField] private string _firstLevel = "Level_01";

    [Header("Title UI")]
    [SerializeField] private GameObject _titleFirstSelected;         // assign your Title button root
    [SerializeField] private string _titleFirstSelectedTag = "FirstSelected"; // optional fallback
    [SerializeField] private string _titleFirstSelectedName = "Btn_Title";    // optional fallback

    [Header("Tutorial UI")]
    [SerializeField] private GameObject _firstTutorialFirstSelected; // ← NEW: Momentum tutorial "Continue" button

    [Header("Momentum Tutorial Reveal")]
    [SerializeField] private CanvasGroup _momentumFocusMask;   // black fullscreen panel with hole around gauge
    [SerializeField] private float _momentumFocusFadeDuration = 0.35f;
    [SerializeField] private float _momentumPreTutorialDelay = 0.5f;
    [SerializeField] private MMF_Player _momentumIntroFeedbacks;
    [SerializeField] private string _momentumIntroTag = "MomentumIntro";

    // NEW: Turbo tutorial reveal
#if CINEMACHINE
    [Header("Turbo Tutorial Cameras (Manual)")]
    [SerializeField] private CinemachineCamera _turboPlayerCam;   // usually your main gameplay cam
    [SerializeField] private CinemachineCamera _turboTrapCam;     // camera that frames the trap/gimmick
    [SerializeField] private float _turboToTrapBlendTime = 0.8f;
    [SerializeField] private float _turboTrapHoldTime = 1.0f;
    [SerializeField] private float _turboBackBlendTime = 0.8f;

    [Header("Turbo Tutorial Camera Auto-Fetch")]
    [SerializeField] private string _turboPlayerCamTag = "TurboPlayerCam";
    [SerializeField] private string _turboTrapCamTag = "TurboTrapCam";
#endif

    [Header("Turbo Tutorial Reveal")]
    // can reuse the same panel as momentum if you want
    [SerializeField] private CanvasGroup _turboFocusMask;
    [SerializeField] private float _turboFocusFadeDuration = 0.35f;
    [SerializeField] private float _turboPreTutorialDelay = 0.5f;

    [Header("Feel MMSceneLoading")]
    [SerializeField] private bool _useAdditive = false;
    [SerializeField] private string _feelLoadingScene = "LoadingScreen";
    [SerializeField] private string _feelAdditiveLoadingScene = "MMAdditiveLoadingScreen";
    [SerializeField, Range(0f, 1f)] private float _entryFade = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _exitFade = 0.35f;
    [SerializeField] private bool _interpolateProgress = true;

    [Header("Fall Respawn")]
    [SerializeField] private bool _enableFallCheck = true;
    [SerializeField] private float _fallKillY = -20f;   // when player.y < this → respawn
    [SerializeField] private int _fallDamage = 20;      // HP lost on fall
    [SerializeField] private float _respawnFreeze = 0.1f; // short freeze before snap (sec, realtime)
    [SerializeField] private float _respawnIFrames = 1.0f; // optional: post-respawn grace (sec)

    [Header("Player Spawn")]
    [SerializeField] private PlayerController _playerPrefab;
    // NEW: reference to PauseMenu (assign in Inspector or auto-resolve)
    [SerializeField] private PauseMenu _pauseMenu;
    [SerializeField] private float _pauseInputBuffer = 0.35f;
    [SerializeField] private bool _freezeTimeOnResults = true;

    // ── HUD delayed reveal ───────────────────────────────────────────────────────────
    [SerializeField] private float _hudRevealDelay = 5f;              // tweak as needed
    private CancellationTokenSource _hudRevealCts;

    [Header("Debug")]
    [SerializeField] private bool _allowStartFromAnyScene = true;

    public GameState State { get; private set; } = GameState.Title;

    // runtime refs
    private PlayerController _player;
    private PlayerInput _playerInput;
    private InputAction _pauseAction;   // from Player map
    private InputAction _cancelAction;  // from UI map
    private bool _isRespawningFromFall = false;
    private Transform _spawnPoint;      // Tag: Respawn
    private MomentumGaugeUI _gauge; // auto-fetched from the Player

    private bool _hasShownFirstTutorial = false; // shows Move tutorial once per session
    private bool _hasShownMomentumTutorial = false; // shows Momentum tutorial once per session
    private bool _hasShownTurboTutorial = false;

    private bool _pauseBlocked = false;
    private bool _isPaused = false;
    public bool IsPaused => _isPaused;
    public PlayerController Player => _player;
    public PlayerInput PlayerInput => _playerInput;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResolvePauseMenu(); // try resolve at boot
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (_allowStartFromAnyScene)
        {
            State = SceneManager.GetActiveScene().name == _titleScene ? GameState.Title : GameState.Playing;
            BindSpawnAndPlayer();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;

        _hudRevealCts?.Cancel();
        _hudRevealCts?.Dispose();
        _hudRevealCts = null;
    }

    private void Update()
    {
        if (_enableFallCheck && State == GameState.Playing && _player != null && !_isRespawningFromFall)
        {
            if (_player.transform.position.y <= _fallKillY)
            {
                TriggerFallRespawn();
            }
        }
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Input callbacks (wired from WireInput)
    public void OnPauseStarted(InputAction.CallbackContext ctx)
    {
        if (State != GameState.Playing) return;
        TogglePause();
    }

    public void OnCancelStarted(InputAction.CallbackContext ctx)
    {
        if (IsPaused) ResumeGame();
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Public API
    public void LoadTitle()
    {
        // Ensure we are not paused anymore
        Time.timeScale = 1f;
        _isPaused = false;

        // Make sure pause UI is hidden
        ResolvePauseMenu();
        _pauseMenu?.HideMenuInstant();   // instant, no tween
        // Reset / hide gameplay-related UI (including tutorials)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ResetAllUI();      // your existing global clean
        }

        // Do the actual scene transition (Feel loader etc.)
        LoadWithFeel(_titleScene, GameState.Title);

        // Cursor should be visible on title
        UpdateCursorState();
    }

    public void StartNewGame() => LoadWithFeel(_firstLevel, GameState.Playing);

    public void RestartLevel()
    {
        TurboModeManager.Instance?.ForceReset(clearCooldown: true);
        string current = SceneManager.GetActiveScene().name;
        LoadWithFeel(current, GameState.Playing);
        _player?.EnableInput();
        _isPaused = false;

        ResolvePauseMenu();
        _pauseMenu?.HideMenu();  // <-- direct call
        UpdateCursorState();
    }

    public void LoadNextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(next);
            string name = Path.GetFileNameWithoutExtension(path);
            LoadWithFeel(name, GameState.Playing);
        }
        else
        {
            LoadTitle();
        }
    }

    public void WinLevel()
    {
        if (State != GameState.Playing) return;
        TurboModeManager.Instance?.ForceReset(clearCooldown: true);
        EnterResultMode(GameState.Clear, () =>
        {
            UIManager.Instance?.ShowGameClearUI();
        });
    }

    public void GameOver()
    {
        if (State == GameState.GameOver) return;
        TurboModeManager.Instance?.ForceReset(clearCooldown: true);
        EnterResultMode(GameState.GameOver, () =>
        {
            UIManager.Instance?.ShowGameOverUI();
        });
    }

    public PlayerController GetPlayer() => _player;

    // GameManager.cs
    public void RespawnPlayer(bool resetStats = true)
    {
        if (_player == null) return;

        // 1) snap & stop
        if (_spawnPoint != null)
            _player.transform.position = _spawnPoint.position;

        var rb = _player.GetRigidbody();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        // 2) make the new transform "real" for raycasts this frame
        Physics.SyncTransforms();

        // 3) use a respawn-aware reset (does not kill jump buffer, seeds coyote)
        _player.OnRespawnSnap();

        if (resetStats)
            _player.GetComponent<PlayerStats>()?.ResetStats();
        
        // NEW: if Turbo is active when we fall, stop it and start cooldown
        var turbo = TurboModeManager.Instance;
        if (turbo != null && turbo.IsActive)
        {
            turbo.StopTurbo();   // this will:
                                 // - restore timeScale
                                 // - set _onCooldown = true
                                 // - start the cooldown timer
                                 // - invoke onTurboEnd => TurboCooldownUI starts cooldown anim
        }

    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Pause System (directly calls PauseMenu)
    public void TogglePause()
    {
        if (_pauseBlocked || State != GameState.Playing) return;
        PauseBufferAsync(this.GetCancellationTokenOnDestroy()).Forget();
        if (_isPaused) ResumeGame(); else PauseGame();
    }

    private async UniTaskVoid PauseBufferAsync(CancellationToken ct)
    {
        _pauseBlocked = true;
        await UniTask.Delay(TimeSpan.FromSeconds(_pauseInputBuffer), DelayType.Realtime, PlayerLoopTiming.Update, ct);
        _pauseBlocked = false;
    }
    public void ShowMomentumTutorial()
    {
        // Only once per session
        if (_hasShownMomentumTutorial) return;
        _hasShownMomentumTutorial = true;
       
        var mm = MomentumManager.Instance;
        if (mm != null)
        {
            mm.SetMomentumPercent(50f);
        }

        // Switch to UI action map (same idea as PauseGame, but without pause menu)
        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();

            if (_playerInput.actions.FindActionMap(MAP_UI, false) != null)
            {
                _playerInput.defaultActionMap = MAP_UI;
                _playerInput.SwitchCurrentActionMap(MAP_UI);
            }
        }

        // Disable direct player control (even though timeScale = 0, just to be safe)
        _player?.DisableInput();

        // Pause world & mark as paused so cursor shows
        Time.timeScale = 0f;
        _isPaused = true;

        // Make sure cursor matches paused state
        UpdateCursorState();

        // 🔹 NEW: focus the Continue button on the tutorial UI
        FocusFirstTutorialFirstSelectedNextFrame().Forget();

        // Run the special reveal sequence
        RunMomentumTutorialSequence().Forget();
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid RunMomentumTutorialSequence()
    {
        var ct = this.GetCancellationTokenOnDestroy();

        // 1) Ensure gauge reference and make it visible
        ResolveGauge();
        _gauge?.ShowTutorialHighlight(true);
        _gauge?.TL_ShowGauge();

        // 2) Optional camera zoom / effects
        if (_momentumIntroFeedbacks != null)
        {
            _momentumIntroFeedbacks.PlayFeedbacks();
        }

        // 3) Optional black panel that darkens everything except gauge
        if (_momentumFocusMask != null)
        {
            _momentumFocusMask.gameObject.SetActive(true);
            _momentumFocusMask.alpha = 0f;
            _momentumFocusMask
                .DOFade(1f, _momentumFocusFadeDuration)
                .SetUpdate(true); // ignore timeScale
        }

        // 4) wait a bit so player can see gauge highlighted before text appears
        await Cysharp.Threading.Tasks.UniTask.Delay(
            System.TimeSpan.FromSeconds(_momentumPreTutorialDelay),
            Cysharp.Threading.Tasks.DelayType.Realtime,
            Cysharp.Threading.Tasks.PlayerLoopTiming.Update,
            ct);

        // 5) Show the actual Momentum tutorial UI
        UIManager.Instance?.ShowTutorial(TutorialKey.Momentum);
        
        FocusFirstTutorialFirstSelectedNextFrame().Forget();
    }

    private void ResolveMomentumIntroFeedbacks()
    {
        _momentumIntroFeedbacks = null;

        if (!IsFirstLevelActive()) return;   // only needed on Level_01 (optional)

        var obj = GameObject.FindWithTag(_momentumIntroTag);
        if (obj != null)
        {
            _momentumIntroFeedbacks = obj.GetComponent<MMF_Player>();
        }
    }

    public void OnMomentumTutorialCompleted()
    {
        // Unlock momentum gain / buffs
        MomentumManager.Instance?.SetGainPaused(false);
        
        ResolveGauge();
        _gauge?.ShowTutorialHighlight(false);

        // Hide black mask if we used it
        if (_momentumFocusMask != null)
        {
            _momentumFocusMask
                .DOFade(0f, _momentumFocusFadeDuration)
                .SetUpdate(true)
                .OnComplete(() => _momentumFocusMask.gameObject.SetActive(false));
        }

        // Resume normal gameplay (unpause + input + player action map)
        ResumeGame();
    }

    public void ShowTurboTutorial()
    {
        if (_hasShownTurboTutorial) return;
        _hasShownTurboTutorial = true;

        #if CINEMACHINE
        ResolveTurboCameras();
        #endif

        // Switch to UI action map so buttons work
        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();

            if (_playerInput.actions.FindActionMap(MAP_UI, false) != null)
            {
                _playerInput.defaultActionMap = MAP_UI;
                _playerInput.SwitchCurrentActionMap(MAP_UI);
            }
        }

        // Disable player control so he doesn't move during camera tour
        _player?.DisableInput();

        // IMPORTANT: don't pause yet, keep timeScale = 1 so Cinemachine blends work
        RunTurboTutorialSequence().Forget();
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid RunTurboTutorialSequence()
    {
        var ct = this.GetCancellationTokenOnDestroy();

        // Make sure world is running while cameras blend
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
            _isPaused = false;
            UpdateCursorState();
        }

#if CINEMACHINE
        // ─────────────────────────────────────────────────────
        // 1) Camera tour: Player → Trap → Player
        // ─────────────────────────────────────────────────────
        if (_turboPlayerCam != null && _turboTrapCam != null)
        {
            _turboPlayerCam.gameObject.SetActive(true);
            _turboTrapCam.gameObject.SetActive(true);

            // Baseline priorities (you can tweak if you already use others)
            int basePriority = 10;
            _turboPlayerCam.Priority = basePriority;
            _turboTrapCam.Priority = basePriority - 1;

            // a) Blend to trap
            _turboTrapCam.Priority = basePriority + 1;
            await Cysharp.Threading.Tasks.UniTask.Delay(
                System.TimeSpan.FromSeconds(_turboToTrapBlendTime),
                Cysharp.Threading.Tasks.DelayType.DeltaTime,
                Cysharp.Threading.Tasks.PlayerLoopTiming.Update,
                ct);

            // b) Hold on trap
            await Cysharp.Threading.Tasks.UniTask.Delay(
                System.TimeSpan.FromSeconds(_turboTrapHoldTime),
                Cysharp.Threading.Tasks.DelayType.DeltaTime,
                Cysharp.Threading.Tasks.PlayerLoopTiming.Update,
                ct);

            // c) Blend back to player
            _turboPlayerCam.Priority = basePriority + 2;
            _turboTrapCam.Priority = basePriority - 1;
            await Cysharp.Threading.Tasks.UniTask.Delay(
                System.TimeSpan.FromSeconds(_turboBackBlendTime),
                Cysharp.Threading.Tasks.DelayType.DeltaTime,
                Cysharp.Threading.Tasks.PlayerLoopTiming.Update,
                ct);

            // d) Final: keep gameplay cam as main
            _turboPlayerCam.Priority = basePriority;
            _turboTrapCam.Priority = basePriority - 1;
        }
#endif

        // ─────────────────────────────────────────────────────
        // 2) Pause the game and show black panel + Turbo UI
        // ─────────────────────────────────────────────────────
        Time.timeScale = 0f;
        _isPaused = true;
        UpdateCursorState();

        if (_turboFocusMask != null)
        {
            _turboFocusMask.gameObject.SetActive(true);
            _turboFocusMask.alpha = 0f;
            _turboFocusMask
                .DOFade(1f, _turboFocusFadeDuration)
                .SetUpdate(true); // unscaled
        }

        await Cysharp.Threading.Tasks.UniTask.Delay(
            System.TimeSpan.FromSeconds(_turboPreTutorialDelay),
            Cysharp.Threading.Tasks.DelayType.Realtime,
            Cysharp.Threading.Tasks.PlayerLoopTiming.Update,
            ct);

        // show Turbo tutorial panel
        UIManager.Instance?.ShowTutorial(TutorialKey.Turbo);
        FocusFirstTutorialFirstSelectedNextFrame().Forget();
    }

    // Called by the Turbo tutorial "Continue" button
    public void OnTurboTutorialCompleted()
    {   

        if (_turboFocusMask != null)
        {
            _turboFocusMask
                .DOFade(0f, _turboFocusFadeDuration)
                .SetUpdate(true)
                .OnComplete(() => _turboFocusMask.gameObject.SetActive(false));
        }

        // Resume game like normal
        ResumeGame();
    }

#if CINEMACHINE
    private void ResolveTurboCameras()
    {
        if (State != GameState.Playing) return;

        // ─ Player cam ─────────────────────────────────────
        if (_turboPlayerCam == null || !_turboPlayerCam.gameObject.scene.IsValid())
        {
            // 1) Try by tag
            if (!string.IsNullOrEmpty(_turboPlayerCamTag))
            {
                var go = GameObject.FindGameObjectWithTag(_turboPlayerCamTag);
                if (go != null)
                    _turboPlayerCam = go.GetComponent<CinemachineCamera>();
            }

            // 2) Fallback: any CinemachineCamera that follows / looks at the player
            if (_turboPlayerCam == null && _player != null)
            {
#if UNITY_6000_0_OR_NEWER
                var cams = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
#else
                var cams = UnityEngine.Object.FindObjectsOfType<CinemachineCamera>();
#endif
                foreach (var cam in cams)
                {
                    if (cam == null) continue;
                    if (cam.Follow == _player.transform || cam.LookAt == _player.transform)
                    {
                        _turboPlayerCam = cam;
                        break;
                    }
                }
            }

            if (_turboPlayerCam == null)
            {
                Debug.LogWarning("[GameManager] Turbo player cam not found. " +
                                 "Assign in inspector or tag a CinemachineCamera as '" + _turboPlayerCamTag + "'.");
            }
        }

        // ─ Trap cam ───────────────────────────────────────
        if (_turboTrapCam == null || !_turboTrapCam.gameObject.scene.IsValid())
        {
            // 1) Try by tag
            if (!string.IsNullOrEmpty(_turboTrapCamTag))
            {
                var go = GameObject.FindGameObjectWithTag(_turboTrapCamTag);
                if (go != null)
                    _turboTrapCam = go.GetComponent<CinemachineCamera>();
            }

            // 2) Fallback: any CinemachineCamera that is NOT the player cam
            if (_turboTrapCam == null)
            {
#if UNITY_6000_0_OR_NEWER
                var cams = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
#else
                var cams = UnityEngine.Object.FindObjectsOfType<CinemachineCamera>();
#endif
                foreach (var cam in cams)
                {
                    if (cam == null || cam == _turboPlayerCam) continue;
                    _turboTrapCam = cam;
                    break;
                }
            }

            if (_turboTrapCam == null)
            {
                Debug.LogWarning("[GameManager] Turbo trap cam not found. " +
                                 "Assign in inspector or tag a CinemachineCamera as '" + _turboTrapCamTag + "'.");
            }
        }
    }
#endif

    public void PauseGame()
    {
        if (_isPaused) return;

        // Switch to UI BEFORE freezing time
        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();
            if (_playerInput.actions.FindActionMap(MAP_UI, false) != null)
            {
                _playerInput.defaultActionMap = MAP_UI;
                _playerInput.SwitchCurrentActionMap(MAP_UI);
            }
        }

        _player?.DisableInput();
        Time.timeScale = 0f;
        _isPaused = true;

        ResolvePauseMenu();
        _pauseMenu?.ShowMenu();  // <-- direct call
        UpdateCursorState();
    }

    public void ResumeGame()
    {
        if (!_isPaused) return;

        Time.timeScale = 1f;

        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();
            if (_playerInput.actions.FindActionMap(MAP_PLAYER, false) != null)
            {
                _playerInput.defaultActionMap = MAP_PLAYER;
                _playerInput.SwitchCurrentActionMap(MAP_PLAYER);
            }
        }

        _player?.EnableInput();
        _isPaused = false;

        ResolvePauseMenu();
        _pauseMenu?.HideMenu();  // <-- direct call
        UpdateCursorState();
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Reset Tutorial (UniTask)
    public void ResetTutorial()
    {
        if (State != GameState.Playing || _player == null) return;

        _player.DisableInput();
        Animator anim = _player.GetComponentInChildren<Animator>();
        float cachedSpeed = anim?.speed ?? 1f;
        if (anim) anim.speed = 0f;

        TutorialProgress.ResetAll();
        UIManager.Instance?.ShowTutorial(TutorialKey.Momentum);

        _ = ResetTutorialUnfreezeAsync(anim, cachedSpeed, this.GetCancellationTokenOnDestroy());
        RestartLevel();
        _hasShownFirstTutorial = false;   // allow Move tutorial again after full reset
        _hasShownMomentumTutorial = false;
        _hasShownTurboTutorial = false;
    }

    private async UniTaskVoid ResetTutorialUnfreezeAsync(Animator anim, float cachedSpeed, CancellationToken ct)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(0.2), DelayType.Realtime, PlayerLoopTiming.Update, ct);
        _player?.EnableInput();
        if (anim) anim.speed = Mathf.Approximately(cachedSpeed, 0f) ? 1f : cachedSpeed;
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // FEEL scene loading wrapper
    private bool IsLoadingScene(string sceneName)
    {
        if (_useAdditive)
            return !string.IsNullOrEmpty(_feelAdditiveLoadingScene) && sceneName == _feelAdditiveLoadingScene;
        else
            return !string.IsNullOrEmpty(_feelLoadingScene) && sceneName == _feelLoadingScene;
    }

    private void LoadWithFeel(string sceneName, GameState targetState)
    {
        DOTween.KillAll();
        DOTween.Clear();

        UIManager.Instance?.ResetAllUI();
        MomentumManager.Instance?.ResetAll();

        if (_useAdditive)
        {
            var settings = new MMAdditiveSceneLoadingManagerSettings
            {
                LoadingSceneName = _feelAdditiveLoadingScene,
                InterpolateProgress = _interpolateProgress,
                EntryFadeDuration = _entryFade,
                ExitFadeDuration = _exitFade
            };
            MMAdditiveSceneLoadingManager.LoadScene(sceneName, settings);
        }
        else
        {
            MMSceneLoadingManager.LoadScene(sceneName, _feelLoadingScene);
        }

        State = targetState;
        ResolvePauseMenu(); // in case canvas lives across scenes
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Scene hooks
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsLoadingScene(scene.name))
        {
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.ShowTitleUI(false);
            return;
        }

        State = scene.name == _titleScene ? GameState.Title : GameState.Playing;

        BindSpawnAndPlayer();

        if (State == GameState.Playing)
        {
            UIManager.Instance?.ShowTitleUI(false);

            var uiCamObj = GameObject.FindWithTag("UICamera");
            if (uiCamObj != null)
            {
                var cam = uiCamObj.GetComponent<Camera>();
                UIManager.Instance?.SetUICamera(cam);
            }
            // Hide first to avoid flicker (BeginGameplayAfterIntroAsync also hides, but this guarantees immediate off)
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.HideAllTutorials();

            _player?.GetComponent<PlayerStats>()?.ResetStats();
           
            // NEW: grab the MMF_Player in this scene
            ResolveMomentumIntroFeedbacks();

            #if CINEMACHINE
            ResolveTurboCameras();
            #endif

            // kick the unified flow (no signals needed)
            BeginGameplayAfterIntroAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        else // Title
        {
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.ShowTitleUI(true);
            FocusTitleFirstSelectedNextFrame().Forget();
        }

        ResolvePauseMenu();
        UpdateCursorState();
    }

    private void BindSpawnAndPlayer()
    {
        var spawnGo = GameObject.FindGameObjectWithTag("Respawn");
        _spawnPoint = spawnGo ? spawnGo.transform : null;

#if UNITY_6000_0_OR_NEWER
        _player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
#else
        _player = UnityEngine.Object.FindObjectOfType<PlayerController>();
#endif

        if (_player == null && _playerPrefab != null && State == GameState.Playing)
        {
            Vector3 pos = _spawnPoint ? _spawnPoint.position : Vector3.zero;
            _player = Instantiate(_playerPrefab, pos, Quaternion.identity);
        }

        _playerInput = _player
            ? (_player.GetComponent<PlayerInput>() ?? _player.GetComponentInChildren<PlayerInput>())
            : null;
        ResolveGauge();

        WireInput();

        // set default map on entry
        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();

            var defaultMap = (State == GameState.Playing) ? MAP_PLAYER : MAP_UI;
            if (_playerInput.actions.FindActionMap(defaultMap, false) != null)
            {
                _playerInput.defaultActionMap = defaultMap;
                _playerInput.SwitchCurrentActionMap(defaultMap);
            }
        }
    }

    private void ResolveGauge()
    {
        if (_gauge == null && _player != null)
            _gauge = _player.GetComponentInChildren<MomentumGaugeUI>(true);
    }

    // find PauseMenu singleton (inspector or auto)
    private void ResolvePauseMenu()
    {
        if (_pauseMenu != null) return;

#if UNITY_6000_0_OR_NEWER
        _pauseMenu = UnityEngine.Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
#else
        _pauseMenu = UnityEngine.Object.FindObjectOfType<PauseMenu>(true);
#endif
    }

    private void UnwireInput()
    {
        if (_pauseAction != null) { _pauseAction.started -= OnPauseStarted; _pauseAction = null; }
        if (_cancelAction != null) { _cancelAction.started -= OnCancelStarted; _cancelAction = null; }
    }

    private void WireInput()
    {
        if (_playerInput == null || _playerInput.actions == null) return;
        UnwireInput();

        var asset = _playerInput.actions;
        _pauseAction = asset.FindAction("Pause", throwIfNotFound: false);
        _cancelAction = asset.FindAction("Cancel", throwIfNotFound: false);

        if (_pauseAction != null) _pauseAction.started += OnPauseStarted;
        if (_cancelAction != null) _cancelAction.started += OnCancelStarted;
    }

    private void UpdateCursorState()
    {
        // Visible on Title/Clear/GameOver OR while paused. Hidden during active gameplay.
        bool show = _isPaused || State != GameState.Playing;

        Cursor.visible = show;
        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public bool IsFirstLevelActive()
    {
        return SceneManager.GetActiveScene().name == _firstLevel;
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid FallRespawnAndDamageAsync()
    {
        if (_isRespawningFromFall) return;
        _isRespawningFromFall = true;

        var stats = _player ? _player.GetComponent<PlayerStats>() : null;
        var recv = _player ? _player.GetComponent<PlayerDamageReceiver>() : null;
        var rb = _player ? _player.GetRigidbody() : null;

        // 0) Hard-gate damage during the whole sequence
        if (recv != null) recv.SetInvulnerable(true);
        stats?.ArmNoDamageFor(_respawnIFrames + 0.05f); // make sure gates ignore TimeScale
                                                        // (Uses Time.unscaledTime inside PlayerStats.) :contentReference[oaicite:3]{index=3}

        // 1) Kill all velocity and SNAP immediately to spawn
        if (rb != null) rb.linearVelocity = Vector3.zero;
        RespawnPlayer(resetStats: false); // moves to spawn and clears motion flags
        if (rb != null) rb.linearVelocity = Vector3.zero;
        Physics.SyncTransforms(); // ensure colliders update this frame

        // 2) (optional micro-freeze ONLY for feel; set to 0 to remove)
        if (_respawnFreeze > 0f)
            await Cysharp.Threading.Tasks.UniTask.Delay(
                System.TimeSpan.FromSeconds(_respawnFreeze),
                DelayType.Realtime
            );

        // 3) Apply fall damage exactly once, no hit-react
        if (stats != null && _fallDamage > 0)
        {
            // uses the overload that bypasses gates and skips hit reaction
            stats.TakeDamage(_fallDamage, ignoreGates: true, triggerHitReact: false);
        } // :contentReference[oaicite:4]{index=4}

        // 4) Short grace window to prevent immediate re-hits on landing
        if (_respawnIFrames > 0f)
            await Cysharp.Threading.Tasks.UniTask.Delay(
                System.TimeSpan.FromSeconds(_respawnIFrames),
                DelayType.Realtime
            );

        if (recv != null) recv.SetInvulnerable(false);
        _isRespawningFromFall = false;
    }
    public void TriggerFallRespawn()
    {
        if (!_enableFallCheck || _player == null || _isRespawningFromFall) return;
        FallRespawnAndDamageAsync().Forget();
    }


    private void EnterResultMode(GameState newState, System.Action showUI)
    {
        // state
        State = newState;

        // make sure we are not considered paused (and hide the pause menu instantly)
        _isPaused = false;
        ResolvePauseMenu();
        _pauseMenu?.HideMenuInstant();

        TurboModeManager.Instance?.ForceReset(clearCooldown: true);
        // lock gameplay
        _player?.DisableInput();

        // put PlayerInput on UI map so Submit/Cancel/Navigate work on result screen
        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();

            var uiMap = _playerInput.actions.FindActionMap(MAP_UI, throwIfNotFound: false);
            if (uiMap != null)
            {
                _playerInput.defaultActionMap = MAP_UI;
                _playerInput.SwitchCurrentActionMap(MAP_UI);
            }
        }

        // optionally freeze gameplay world (UI tweens should use SetUpdate(true))
        if (_freezeTimeOnResults) Time.timeScale = 0f;

        // clear any leftover gameplay UI (tutorials, etc.)
        UIManager.Instance?.ResetAllUI();

        // show the specific result UI
        showUI?.Invoke();

        // show/unlock cursor for results
        UpdateCursorState();
    }

    private async UniTaskVoid FocusTitleFirstSelectedNextFrame()
    {
        await UniTask.NextFrame(); // wait until Title UI is enabled

        var target = _titleFirstSelected;

        // optional fallbacks
        if (target == null && !string.IsNullOrEmpty(_titleFirstSelectedTag))
            target = GameObject.FindGameObjectWithTag(_titleFirstSelectedTag);
        if (target == null && !string.IsNullOrEmpty(_titleFirstSelectedName))
            target = GameObject.Find(_titleFirstSelectedName);

        if (EventSystem.current != null && target != null && target.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
    }

    private async UniTaskVoid FocusFirstTutorialFirstSelectedNextFrame()
    {
        // wait one frame so the tutorial UI is fully active
        await UniTask.NextFrame();

        if (EventSystem.current == null) return;
        if (_firstTutorialFirstSelected == null) return;
        if (!_firstTutorialFirstSelected.activeInHierarchy) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(_firstTutorialFirstSelected);
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Unified flow: hide/lock → wait → show/unlock (no Timeline signals needed)
    private async UniTaskVoid BeginGameplayAfterIntroAsync(CancellationToken ctOuter)
    {
        // cancel any previous schedule
        _hudRevealCts?.Cancel();
        _hudRevealCts?.Dispose();
        _hudRevealCts = new CancellationTokenSource();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ctOuter, _hudRevealCts.Token);
        var ct = linked.Token;

        // 0) Hide HUD & lock gameplay during intro
        SetGameplayUIVisible(false);
        SwitchActionMap(MAP_UI);      // keep UI map so skip/menu can work
        SetInputEnabled(false);       // player controller off

        // 1) Wait for HP / SP intro animation time
        await UniTask.Delay(TimeSpan.FromSeconds(_hudRevealDelay),
                            DelayType.DeltaTime,
                            PlayerLoopTiming.Update,
                            ct);

        if (ct.IsCancellationRequested || State != GameState.Playing)
            return;

        // 2) Reveal HUD (HP/SP bars, gauge, etc.)
        SetGameplayUIVisible(true);

        // 3) Start gameplay normally (Player map + input ON)
        SwitchActionMap(MAP_PLAYER);
        SetInputEnabled(true);
        SetupMomentumGateForFirstLevel();

        // 4) First time only: show Move tutorial overlay (no pause, no input change)
        if (!_hasShownFirstTutorial && IsFirstLevelActive())
        {
            _hasShownFirstTutorial = true;
            UIManager.Instance?.ShowTutorial(TutorialKey.Move);
        }

        // cursor should be hidden during gameplay
        UpdateCursorState();
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Helpers used by the unified flow
    void SetGameplayUIVisible(bool visible)
    {
        UIManager.Instance?.ShowPlayerUI(visible);
        if (!visible) UIManager.Instance?.HideAllTutorials();

        ResolveGauge();  // finds MomentumGaugeUI in scene

        if (_gauge != null)
        {
            bool shouldShowGauge = visible && ShouldGaugeBeVisibleNow();

            if (shouldShowGauge) _gauge.TL_ShowGauge();
            else _gauge.TL_HideGauge();
        }
    }

    private bool ShouldGaugeBeVisibleNow()
    {
        // Non-first levels: always show
        if (!IsFirstLevelActive()) return true;

        // First level: only show after Momentum tutorial is flagged as learned
        return TutorialProgress.IsLearned(TutorialKey.Momentum);
    }

    void SetInputEnabled(bool enabled)
    {
        if (enabled) _player?.EnableInput();
        else _player?.DisableInput();

        if (_playerInput != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (_playerInput.actions != null && !_playerInput.actions.enabled) _playerInput.actions.Enable();
        }
    }

    void SwitchActionMap(string map)
    {
        if (string.IsNullOrEmpty(map) || _playerInput == null || _playerInput.actions == null) return;
        var found = _playerInput.actions.FindActionMap(map, false);
        if (found != null)
        {
            _playerInput.defaultActionMap = map;
            _playerInput.SwitchCurrentActionMap(map);
        }
    }
    private void SetupMomentumGateForFirstLevel()
    {
        bool gateMomentum =
            IsFirstLevelActive() &&                      // only first level
            !TutorialProgress.IsLearned(TutorialKey.Momentum); // only if not learned yet

        MomentumManager.Instance?.SetGainPaused(gateMomentum);  // stops positive AddMomentum

        // (Optional) also hard reset momentum if you want it to always start from 0
        if (gateMomentum)
            MomentumManager.Instance?.ResetAll();
    }
}
