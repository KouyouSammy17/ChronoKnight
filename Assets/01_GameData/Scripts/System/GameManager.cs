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

    [Header("Scene Names (3-scene flow)")]
    [SerializeField] private string _titleScene = "Title";
    [SerializeField] private string _tutorialLevel = "Level_01"; // Tutorial Stage
    [SerializeField] private string _level02 = "Level_02";

    [Header("Title UI")]
    [SerializeField] private GameObject _titleFirstSelected;         // assign your Title button root
    [SerializeField] private string _titleFirstSelectedTag = "FirstSelected"; // optional fallback
    [SerializeField] private string _titleFirstSelectedName = "Btn_Title";    // optional fallback

    [Header("Game Over")]
    [SerializeField] private float _gameOverDelay = 2f;

    [Header("Win Result Delay")]
    [SerializeField] private float _winClearDelay = 2f;
    private GameClearUI_Anim _gameClearAnim;

    [Header("Win Flow Refs")]
    private WinCinematicController _winCine;
    private TeleportFadeSamplePlayer _teleFade;

    [Header("Win Camera Zoom")]
    private MMFeedbacks _winZoomFeedback;

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
    [SerializeField, Tooltip("Prevents double-triggering fall respawn in quick succession.")]
    private float _fallRearmDelay = 0.35f;
    private float _nextFallAllowedUnscaled = 0f;

    [Header("Player Spawn")]
    [SerializeField] private PlayerMotor _playerPrefab;

    // NEW: reference to PauseMenu (assign in Inspector or auto-resolve)
    [SerializeField] private PauseMenu _pauseMenu;
    [SerializeField] private float _pauseInputBuffer = 0.35f;
    [SerializeField] private bool _freezeTimeOnResults = true;

    [Header("Tutorial Stage Flag")]
    private TutorialClearUI_Anim _tutorialClearAnim; // drag if you want, else auto-find
    [SerializeField] private bool _forceTutorialStage = false; // for testing

    [Header("Result UI First Selected")]
    [SerializeField] private GameObject _tutorialClearFirstSelected; // Restart button GO
    [SerializeField] private GameObject _gameClearFirstSelected;     // (optional) Next/Restart on normal clear
    [SerializeField] private GameObject _gameOverFirstSelected;      // (optional)

    public bool IsTutorialStage => _forceTutorialStage || SceneManager.GetActiveScene().name == _tutorialLevel;


    // HUD reveal is now owned by TutorialManager

    [Header("Debug")]
    [SerializeField] private bool _allowStartFromAnyScene = true;

    public GameState State { get; private set; } = GameState.Title;

    // runtime refs
    private PlayerMotor _player;
    private PlayerInput _playerInput;
    private InputAction _pauseAction;   // from Player map
    private InputAction _cancelAction;  // from UI map
    private bool _isRespawningFromFall = false;
    private Transform _spawnPoint;      // Tag: Respawn
    private MomentumGaugeUI _gauge; // auto-fetched from the Player
    private Goal goal;                   // set by Goal when player wins
    private Transform _checkpoint; // runtime current checkpoint
    private CancellationTokenSource _gameOverCts;
    private bool _gameOverSequenceRunning;
    private bool _winRunning;

    private bool _pauseBlocked = false;
    private bool _isPaused = false;
    private CancellationTokenSource _fallRespawnCts;

    private GameClearTimeText _gameClearTimeText;
    private const string TA_BEST_TIME_KEY = "TA_BestTime_Level02";
    private const string TA_BEST_STARS_KEY = "TA_BestStars_Level02";
    public bool IsPaused => _isPaused;
    public bool IsTimeAttackStage =>
    SceneManager.GetActiveScene().name == _level02;
    public PlayerMotor Player => _player;
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
    }

    private void Update()
    {
        if (!_enableFallCheck) return;
        if (State != GameState.Playing) return;
        if (_player == null) return;
        if (_isRespawningFromFall) return;

        // prevent spam triggers
        if (Time.unscaledTime < _nextFallAllowedUnscaled) return;

        if (_player.transform.position.y <= _fallKillY)
        {
            TriggerFallRespawn();
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
        CancelGameOverSequence();
    }

    public void StartNewGame()
    {
        // make sure we start "clean"
        Time.timeScale = 1f;
        _isPaused = false;

        ResolvePauseMenu();
        _pauseMenu?.HideMenuInstant();

        TurboModeManager.Instance?.ForceReset(clearCooldown: true);
        MomentumManager.Instance?.ResetAll();
        UIManager.Instance?.ResetAllUI();

        // RESET ALL TUTORIALS (new game = fresh tutorial state)
        TutorialProgress.ResetAll();

        // load tutorial stage
        LoadWithFeel(_tutorialLevel, GameState.Playing);

        UpdateCursorState();
        CancelGameOverSequence();
    }

    public void RestartLevel()
    {
        TurboModeManager.Instance?.ForceReset(clearCooldown: true);
        string current = SceneManager.GetActiveScene().name;
        LoadWithFeel(current, GameState.Playing);
        _player?.EnableInput();
        _isPaused = false;
        _checkpoint = null;

        ResolvePauseMenu();
        _pauseMenu?.HideMenu();  // <-- direct call
        UpdateCursorState();
        CancelGameOverSequence();
    }

    public void LoadNextLevel()
    {
        string current = SceneManager.GetActiveScene().name;

        if (current == _titleScene)
        {
            LoadWithFeel(_tutorialLevel, GameState.Playing);
            return;
        }

        if (current == _tutorialLevel)
        {
            LoadWithFeel(_level02, GameState.Playing);
            return;
        }

        // Level_02 or anything else -> back to title
        LoadTitle();
    }


    public void WinLevel(Goal goal)
    {
        if (State != GameState.Playing) return;
        if (_winRunning) return;

        // lock immediately so we can't re-enter
        _winRunning = true;
        State = GameState.Clear;
        // remember the goal that triggered the win
        this.goal = goal;
        UIManager.Instance?.ShowPlayerUI(false);
        _gauge?.TL_HideGauge();

        // Stop the timer when goal is touched
        TimeAttackManager.Instance?.StopRun();

        // Goal-specific refs: fetch EVERY time (not only when null)
        if (goal != null)
        {
            _teleFade = goal.GetComponentInChildren<TeleportFadeSamplePlayer>(true);

            var tag = goal.GetComponentInChildren<WinZoomFeedbackTag>(true);
            _winZoomFeedback = tag != null ? tag.GetComponentInChildren<MMFeedbacks>(true) : null;
        }

        // Scene-wide: fetch once
        if (_winCine == null)
        {
#if UNITY_6000_0_OR_NEWER
            _winCine = UnityEngine.Object.FindFirstObjectByType<WinCinematicController>(FindObjectsInactive.Include);
#else
        _winCine = UnityEngine.Object.FindObjectOfType<WinCinematicController>(true);
#endif
        }

        WinSequenceAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid WinSequenceAsync(CancellationToken ct)
    {
        try
        {
            TurboModeManager.Instance?.ForceReset(clearCooldown: true);

            var player = _player;
            if (player == null) return;

            var brain = player.GetComponent<PlayerStateMachineBrain>();
            if (brain != null)
            {
                brain.ChangeState(PlayerStateID.Win, force: true);
                brain.Motor?.DisableInput();
                brain.Motor?.StopHorizontalInstant();
                brain.Motor?.SetFrozen(true);
            }

            // camera zoom (goal-tagged)
            if (_winZoomFeedback != null)
            {
                _winZoomFeedback.PlayFeedbacks();
            }

            // win cinematic (turn + win anim)
            if (_winCine != null && brain != null)
            {
                Transform modelRoot = brain.Anim != null ? brain.Anim.transform : player.transform;
                await _winCine.PlayWinCinematicAsync(brain, modelRoot, ct);
            }

            // goal fade (optional)
            if (_teleFade != null)
            {
                _teleFade.SetFadeParams(speed: 1.2f, rise: 0.25f, twist: 3.5f, spread: 0.7f);
                _teleFade.StartFadeOut();

                await UniTask.Delay(
                    TimeSpan.FromSeconds(_teleFade.GetTotalFadeSeconds()),
                    DelayType.Realtime,
                    cancellationToken: ct
                );
            }

            try
            {
                if (_winClearDelay > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_winClearDelay),
                        DelayType.Realtime, PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException) { return; }

            EnterResultMode(GameState.Clear, () =>
            {
                if (IsTutorialStage)
                {
                    UIManager.Instance?.ShowTutorialClearUI();
                    ResolveTutorialClearUI();

                    int starsAchieved = 3;      // tutorial fixed
                    bool toggleAchieved = true; // tutorial fixed
                    _tutorialClearAnim?.Show(starsAchieved, toggleAchieved);
                    return;
                }

                // Level_02 = Time Attack clear UI
                UIManager.Instance?.ShowGameClearUI();

                if (IsTimeAttackStage)
                {
                    ResolveGameClearUI();
                    float elapsed = TimeAttackManager.Instance != null ? TimeAttackManager.Instance.Elapsed : 0f;

                    bool reachedGoal = this.goal != null;
                    bool clear120 = elapsed <= 120f;
                    bool clear90 = elapsed <= 90f;

                    bool[] checks = { reachedGoal, clear120, clear90 };
                    int stars = (reachedGoal ? 1 : 0) + (clear120 ? 1 : 0) + (clear90 ? 1 : 0);

                    // compute stars/checks...
                    SaveTimeAttackResult(elapsed, stars);

                    // read best AFTER saving
                    float best = PlayerPrefs.GetFloat(TA_BEST_TIME_KEY, float.MaxValue);

                    // update text
                    _gameClearTimeText?.OnGameClearUIShown(elapsed, best);

                    // animate checks/stars
                    _gameClearAnim?.Show(stars, checks);
                }
            });

        }
        finally
        {
            _winRunning = false;
        }
    }


    public void GameOver()
    {
        if (State != GameState.Playing) return;
        if (_gameOverSequenceRunning) return;

        _gameOverSequenceRunning = true;

        _gameOverCts?.Cancel();
        _gameOverCts?.Dispose();
        _gameOverCts = new CancellationTokenSource();

        GameOverSequenceAsync(_gameOverCts.Token).Forget();
        _gauge?.TL_HideGauge();
    }

    private void CancelGameOverSequence()
    {
        _gameOverCts?.Cancel();
        _gameOverCts?.Dispose();
        _gameOverCts = null;
        _gameOverSequenceRunning = false;
    }

    private async UniTaskVoid GameOverSequenceAsync(CancellationToken ct)
    {
        // Make sure turbo timescale doesn’t make death feel weird
        TurboModeManager.Instance?.ForceReset(clearCooldown: true);

        // Lock player
        _player?.DisableInput();

        // Stop sliding
        if (_player != null)
        {
            var rb = _player.GetRigidbody();
            if (rb != null) rb.linearVelocity = Vector3.zero;

        }

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_gameOverDelay),
                DelayType.Realtime, PlayerLoopTiming.Update, ct);
        }
        catch (OperationCanceledException) { return; }

        if (ct.IsCancellationRequested || this == null) return;


        EnterResultMode(GameState.GameOver, () => UIManager.Instance?.ShowGameOverUI_Animated());
        _gameOverSequenceRunning = false;
    }

    public PlayerMotor GetPlayer() => _player;

    public void RespawnPlayer(bool resetStats = true)
    {
        if (_player == null) return;

        Transform target = _checkpoint != null ? _checkpoint : _spawnPoint;

        if (target != null)
            _player.transform.position = target.position;

        var rb = _player.GetRigidbody();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        // 2) make the new transform "real" for raycasts this frame
        Physics.SyncTransforms();

        // 3) use a respawn-aware reset (does not kill jump buffer, seeds coyote)
        _player.OnRespawnSnap();

        if (resetStats)
        {
            _player.GetComponent<PlayerStats>()?.ResetStats();
            _player.GetComponent<PlayerStateMachineBrain>().ResetAfterRespawn();
        }


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
                if (IsTimeAttackStage)
                    TimeAttackManager.Instance?.PauseRun();
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
                if (IsTimeAttackStage)
                    TimeAttackManager.Instance?.ResumeRun();
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
        UIManager.Instance?.ShowTutorial(TutorialKey.Move);

        _ = ResetTutorialUnfreezeAsync(anim, cachedSpeed, this.GetCancellationTokenOnDestroy());
        RestartFromClearUI();
    }

    private async UniTaskVoid ResetTutorialUnfreezeAsync(Animator anim, float cachedSpeed, CancellationToken ct)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(0.2), DelayType.Realtime, PlayerLoopTiming.Update, ct);
        _player?.EnableInput();
        if (anim) anim.speed = Mathf.Approximately(cachedSpeed, 0f) ? 1f : cachedSpeed;
    }

    // Called by the Restart button on Tutorial Clear / Clear UI
    public void RestartFromClearUI()
    {
        // Results often run at timeScale 0
        Time.timeScale = 1f;
        _isPaused = false;

        // Hide pause UI if it exists
        ResolvePauseMenu();
        _pauseMenu?.HideMenuInstant();

        // Stop turbo/time effects
        TurboModeManager.Instance?.ForceReset(clearCooldown: true);

        // If we're restarting the tutorial stage, also reset tutorial session/progress
        if (IsTutorialStage)
        {
            // This method already exists in your project (you call it elsewhere)
            TutorialManager.Instance?.ResetAllTutorialsAndRestart();
            // NOTE: If that method itself reloads the scene, you can early return.
            // If it doesn't reload, we continue and reload below.
        }

        // Clear UI state before reloading
        UIManager.Instance?.ResetAllUI();

        // Reload current scene
        string current = SceneManager.GetActiveScene().name;
        LoadWithFeel(current, GameState.Playing);

        UpdateCursorState();
        CancelGameOverSequence();
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
        _isRespawningFromFall = false;
        _fallRespawnCts?.Cancel();
        _fallRespawnCts?.Dispose();
        _fallRespawnCts = null;
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

            // Ensure TutorialManager knows about scene refs
            TutorialManager.Instance?.ResolveSceneReferences();

            // kick the unified flow (no signals needed) — owned by TutorialManager
            TutorialManager.Instance?.BeginGameplayAfterIntroAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        else // Title
        {
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.ShowTitleUI(true);
            FocusTitleFirstSelectedNextFrame().Forget();
        }

        ResolvePauseMenu();
        ResolveTutorialClearUI();
        UpdateCursorState();

        bool timeAttack = SceneManager.GetActiveScene().name == _level02;
        TimeAttackManager.Instance?.Configure(timeAttack);

    }

    private void BindSpawnAndPlayer()
    {
        var spawnGo = GameObject.FindGameObjectWithTag("Respawn");
        _spawnPoint = spawnGo ? spawnGo.transform : null;

#if UNITY_6000_0_OR_NEWER
        _player = UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
#else
    _player = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
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

    private void ResolveTutorialClearUI()
    {
        if (_tutorialClearAnim != null) return;

#if UNITY_6000_0_OR_NEWER
        _tutorialClearAnim = UnityEngine.Object.FindFirstObjectByType<TutorialClearUI_Anim>(FindObjectsInactive.Include);
#else
    _tutorialClearAnim = UnityEngine.Object.FindObjectOfType<TutorialClearUI_Anim>(true);
#endif
    }

    private void ResolveGameClearUI()
    {
#if UNITY_6000_0_OR_NEWER
        _gameClearAnim = UnityEngine.Object.FindFirstObjectByType<GameClearUI_Anim>(FindObjectsInactive.Include);
        _gameClearTimeText = UnityEngine.Object.FindFirstObjectByType<GameClearTimeText>(FindObjectsInactive.Include);
#else
    _gameClearAnim = UnityEngine.Object.FindObjectOfType<GameClearUI_Anim>(true);
    _gameClearTimeText = UnityEngine.Object.FindObjectOfType<GameClearTimeText>(true);
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

    public bool IsTutorialLevelActive()
    {
        return SceneManager.GetActiveScene().name == _tutorialLevel;
    }

    private async UniTaskVoid FallRespawnAndDamageAsync()
    {
        if (_isRespawningFromFall) return;
        if (_player == null) return;

        _isRespawningFromFall = true;

        // re-arm delay (so we can't double-trigger immediately)
        _nextFallAllowedUnscaled = Time.unscaledTime + _fallRearmDelay;

        // Cancel any previous fall routine
        _fallRespawnCts?.Cancel();
        _fallRespawnCts?.Dispose();
        _fallRespawnCts = new CancellationTokenSource();
        var ct = _fallRespawnCts.Token;

        var player = _player;
        var stats = player.GetComponent<PlayerStats>();
        var recv = player.GetComponent<PlayerDamageReceiver>();
        var combat = player.GetComponent<CombatController>();
        var brain = player.GetComponent<PlayerStateMachineBrain>();
        var rb = player.GetRigidbody();

        try
        {
            // Make sure we have a spawn point (prevents "didn't respawn" loops)
            EnsureSpawnPoint();

            // Stop combat / queued stuff that can lock you
            combat?.CancelCombo();

            // Block incoming damage during the sequence + shortly after
            recv?.SetInvulnerable(true);
            stats?.ArmNoDamageFor(_respawnIFrames + 0.1f);

            // Lock input + stop motion
            player.DisableInput();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Snap to spawn (your RespawnPlayer already calls OnRespawnSnap + stops Turbo)
            RespawnPlayer(resetStats: false);

            if (rb != null) rb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();

            // Tiny freeze for feel
            if (_respawnFreeze > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_respawnFreeze), DelayType.Realtime, PlayerLoopTiming.Update, ct);
                if (ct.IsCancellationRequested) return;
            }

            // Apply fall damage ONCE (skip hit react)
            if (stats != null && _fallDamage > 0)
            {
                stats.TakeHazardDamage(_fallDamage, ignoreGates: true, triggerHitReact: false);
            }

            // Force state machine back to locomotion (prevents “stuck can’t jump/attack”)
            if (brain != null)
            {
                var next = player.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne;
                brain.ChangeState(next, force: true);
            }

            // Restore control
            player.EnableInput();

            if (_respawnIFrames > 0f)
            {
                recv?.SetInvulnerableFor(_respawnIFrames).Forget();
            }
        }
        finally
        {
            _isRespawningFromFall = false;
        }
    }

    public void TriggerFallRespawn()
    {
        if (!_enableFallCheck) return;
        if (_player == null) return;
        if (_isRespawningFromFall) return;
        if (Time.unscaledTime < _nextFallAllowedUnscaled) return;

        FallRespawnAndDamageAsync().Forget();
    }


    public void SetCheckpoint(Transform checkpoint)
    {
        _checkpoint = checkpoint;
    }

    private void EnsureSpawnPoint()
    {
        if (_spawnPoint != null) return;

        var spawnGo = GameObject.FindGameObjectWithTag("Respawn");
        _spawnPoint = spawnGo ? spawnGo.transform : null;

        // If still missing, fall back to world origin so we don't loop under killY forever.
        if (_spawnPoint == null)
        {
            Debug.LogWarning("[GameManager] Respawn tag not found. Falling back to (0,0,0).");
            var tmp = new GameObject("TEMP_RESPAWN_POINT");
            tmp.transform.position = Vector3.zero;
            _spawnPoint = tmp.transform;
        }
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

        GameObject first = null;

        if (newState == GameState.Clear)
        {
            first = IsTutorialStage ? _tutorialClearFirstSelected : _gameClearFirstSelected;
        }
        else if (newState == GameState.GameOver)
        {
            first = _gameOverFirstSelected;
        }

        FocusUIFirstSelectedAsync(first, this.GetCancellationTokenOnDestroy()).Forget();

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

    private async UniTaskVoid FocusUIFirstSelectedAsync(GameObject target, CancellationToken ct)
    {
        if (target == null) return;

        // Let UI enable, rebuild layout, and InputSystemUIInputModule settle
        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
        await UniTask.NextFrame(ct);
        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

        if (ct.IsCancellationRequested) return;
        if (EventSystem.current == null) return;
        if (!target.activeInHierarchy) return;

        // Must be a Selectable (Button). If it's a child Text, selection won't show.
        var sel = target.GetComponent<UnityEngine.UI.Selectable>();
        if (sel != null && !sel.IsInteractable()) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
        sel?.Select();
    }

    public void NotifyGameplayBegan()
    {
        if (State != GameState.Playing) return;

        if (IsTimeAttackStage)
        {
            TimeAttackManager.Instance?.StartRun();
        }
    }

    private void SaveTimeAttackResult(float elapsed, int stars)
    {
        float best = PlayerPrefs.GetFloat(TA_BEST_TIME_KEY, float.MaxValue);
        int bestStars = PlayerPrefs.GetInt(TA_BEST_STARS_KEY, 0);

        // Save better stars first, then time
        if (stars > bestStars)
        {
            PlayerPrefs.SetInt(TA_BEST_STARS_KEY, stars);
            PlayerPrefs.SetFloat(TA_BEST_TIME_KEY, elapsed);
        }
        else if (stars == bestStars && elapsed < best)
        {
            PlayerPrefs.SetFloat(TA_BEST_TIME_KEY, elapsed);
        }

        PlayerPrefs.Save();
    }
}
