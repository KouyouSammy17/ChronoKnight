using Cysharp.Threading.Tasks;
using DG.Tweening;
using MoreMountains.Tools;
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState { Title, Playing, Clear, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string _titleScene = "Title";
    [SerializeField] private string _firstLevel = "Level_01";

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
    }

    private void Update()
    {
        // …your existing Update logic…

        if (_enableFallCheck && State == GameState.Playing && _player != null && !_isRespawningFromFall)
        {
            if (_player.transform.position.y < _fallKillY)
            {
                FallRespawnAndDamageAsync().Forget();
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
        State = GameState.Clear;
        UIManager.Instance?.ResetAllUI();
        UIManager.Instance?.ShowGameClearUI();
        UpdateCursorState();
    }

    public void GameOver()
    {
        if (State == GameState.GameOver) return;
        State = GameState.GameOver;
        UIManager.Instance?.ResetAllUI();
        UIManager.Instance?.ShowGameOverUI();
        UpdateCursorState();
    }

    public PlayerController GetPlayer() => _player;

    public void RespawnPlayer(bool resetStats = true)
    {
        if (_player == null) return;

        if (_spawnPoint != null)
            _player.transform.position = _spawnPoint.position;

        var rb = _player.GetRigidbody();
        rb.linearVelocity = Vector3.zero;

        _player.ResetPlayerState();

        if (resetStats)
            _player.GetComponent<PlayerStats>()?.ResetStats(); 
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
            if (_playerInput.actions.FindActionMap("UI", false) != null)
            {
                _playerInput.defaultActionMap = "UI";
                _playerInput.SwitchCurrentActionMap("UI");
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
            if (_playerInput.actions.FindActionMap("Player", false) != null)
            {
                _playerInput.defaultActionMap = "Player";
                _playerInput.SwitchCurrentActionMap("Player");
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
       RestartLevel();
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
            UIManager.Instance?.ShowPlayerUI(true);
            _player?.GetComponent<PlayerStats>()?.ResetStats();
        }
        else
        {
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.ShowTitleUI(true);
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

        WireInput();

        // set default map on entry
        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();

            var defaultMap = (State == GameState.Playing) ? "Player" : "UI";
            if (_playerInput.actions.FindActionMap(defaultMap, false) != null)
            {
                _playerInput.defaultActionMap = defaultMap;
                _playerInput.SwitchCurrentActionMap(defaultMap);
            }
        }
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

    private async UniTaskVoid FallRespawnAndDamageAsync()
    {
        _isRespawningFromFall = true;

        // tiny freeze for feedback / safety
        await UniTask.Delay(TimeSpan.FromSeconds(_respawnFreeze), DelayType.Realtime);

        // 1) move to spawn but DON'T refill HP
        RespawnPlayer(resetStats: false);

        // 2) apply fall damage
        var stats = _player.GetComponent<PlayerStats>();
        if (stats != null && _fallDamage > 0)
        {
            stats.TakeDamage(_fallDamage);
        }

        // 3) optional: brief invulnerability window (grace)
        if (_respawnIFrames > 0f)
        {
            // If you have a damage gate / hurtbox toggler, do it here.
            // Example (pseudo):
            // _player.SetInvulnerable(true);
            await UniTask.Delay(TimeSpan.FromSeconds(_respawnIFrames), DelayType.Realtime);
            // _player.SetInvulnerable(false);
        }

        _isRespawningFromFall = false;
    }
}
