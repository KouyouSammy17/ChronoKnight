using Cysharp.Threading.Tasks;
using DG.Tweening;
using MoreMountains.Feedbacks;
using System;
using System.Reflection;
using System.Threading;
#if CINEMACHINE
using Unity.Cinemachine;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Tutorial orchestration (persistent singleton).
/// - All tutorial logic lives here (NOT in GameManager).
/// - Triggers / UI buttons should call RequestShow / RequestHide / CompleteTutorial.
/// - Special tutorials:
///   - Momentum: pause + mask + gauge highlight + (optional) intro feedbacks.
///   - Turbo: camera tour (optional Cinemachine) then pause + mask + UI.
/// - Owns first-level rules:
///   - Momentum gain gated until Momentum tutorial learned (if your MomentumManager supports it).
///   - Momentum gauge hidden until Momentum tutorial learned (GameManager uses ShouldShowMomentumGauge()).
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    private const string MAP_PLAYER = "Player";
    private const string MAP_UI = "UI";

    [Header("Level Gate")]
    [SerializeField] private string _firstLevelName = "Level_01";

    [Header("First Selected (optional)")]
    [SerializeField] private GameObject _firstTutorialFirstSelected; // Continue button etc. (legacy / generic)
    [SerializeField] private GameObject _momentumFirstSelected; // optional: explicit first selected for Momentum tutorial
    [SerializeField] private GameObject _turboFirstSelected; // optional: explicit first selected for Turbo tutorial

    // „Ÿ„Ÿ HUD delayed reveal (moved from GameManager)
    [SerializeField] private float _hudRevealDelay = 5f;              // tweak as needed
    // CTS for scheduling reveal    
    private CancellationTokenSource _hudRevealCts;

    [Header("Momentum Tutorial Reveal")]
    [SerializeField] private CanvasGroup _momentumFocusMask;   // black fullscreen panel with hole around gauge
    [SerializeField] private float _momentumFocusFadeDuration = 0.35f;
    [SerializeField] private float _momentumPreTutorialDelay = 0.5f;
    [SerializeField] private MMF_Player _momentumIntroFeedbacks;
    [SerializeField] private string _momentumIntroTag = "MomentumIntro";

    [Header("Turbo Tutorial Reveal")]
    [SerializeField] private CanvasGroup _turboFocusMask;
    [SerializeField] private float _turboFocusFadeDuration = 0.35f;
    [SerializeField] private float _turboPreTutorialDelay = 0.5f;

#if CINEMACHINE
    [Header("Turbo Tutorial Cameras (Manual)")]
    [SerializeField] private CinemachineCamera _turboPlayerCam;   // main gameplay cam
    [SerializeField] private CinemachineCamera _turboTrapCam;     // gimmick/trap cam
    [SerializeField] private float _turboToTrapBlendTime = 0.8f;
    [SerializeField] private float _turboTrapHoldTime = 1.0f;
    [SerializeField] private float _turboBackBlendTime = 0.8f;

    [Header("Turbo Tutorial Camera Auto-Fetch")]
    [SerializeField] private string _turboPlayerCamTag = "TurboPlayerCam";
    [SerializeField] private string _turboTrapCamTag = "TurboTrapCam";
#endif

    [Header("Debug")]
    [SerializeField] private bool _log;

    // Cached (scene-dependent)
    private PlayerMotor _player;
    private PlayerInput _playerInput;
    private MomentumGaugeUI _gauge;

    // Per-session flags
    private bool _shownMoveThisSession;
    private bool _shownMomentumThisSession;
    private bool _shownTurboThisSession;

    // Guards
    private bool _momentumSequenceRunning;
    private bool _turboSequenceRunning;

    private CancellationToken _destroyToken;

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Unity lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _destroyToken = this.GetCancellationTokenOnDestroy();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        _hudRevealCts?.Cancel();
        _hudRevealCts?.Dispose();
        _hudRevealCts = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Clear scene refs
        _player = null;
        _playerInput = null;
        _gauge = null;

        ResolveSceneReferences();
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Public API (called by GameManager / triggers / UI)

    /// <summary>GameManager should call this after intro/HUD reveal when actual gameplay starts.</summary>
    public void OnGameplayBegan()
    {
        if (!IsPlayingScene()) return;

        ApplyMomentumAndTurboGateIfNeeded(IsTutorialLevelActive());


        // Match old behavior: show Move once per session on first level.
        if (IsTutorialLevelActive() && !_shownMoveThisSession)
        {
            _shownMoveThisSession = true;
            UIManager.Instance?.ShowTutorial(TutorialKey.Move);
        }
    }

    /// <summary>GameManager uses this when deciding whether to show the HUD gauge.</summary>
    public bool ShouldShowMomentumGauge()
    {
        if (!IsTutorialLevelActive()) return true;
        return TutorialProgress.IsLearned(TutorialKey.Momentum);
    }

    /// <summary>Call this on every scene load (GameManager already calls it).</summary>
    public void ResolveSceneReferences()
    {
        ResolvePlayerAndInput();
        ResolveGauge();
        ResolveMomentumIntroFeedbacks();

#if CINEMACHINE
        // Clear invalid cams (scene changed)
        if (_turboPlayerCam != null && !_turboPlayerCam.gameObject.scene.IsValid()) _turboPlayerCam = null;
        if (_turboTrapCam != null && !_turboTrapCam.gameObject.scene.IsValid()) _turboTrapCam = null;
#endif
    }

    /// <summary>Triggers should call this to show a tutorial.</summary>
    public void RequestShow(TutorialKey key)
    {
        if (!IsPlayingScene()) return;
        if (TutorialProgress.IsLearned(key)) return;

        switch (key)
        {
            case TutorialKey.Momentum:
                ShowMomentumTutorial();
                break;

            case TutorialKey.Turbo:
                ShowTurboTutorial();
                break;

            default:
                UIManager.Instance?.ShowTutorial(key);
                break;
        }
    }

    /// <summary>Triggers should call this on exit to hide (Momentum/Turbo ignore hides because modal).</summary>
    public void RequestHide(TutorialKey key)
    {
        if (key == TutorialKey.Momentum || key == TutorialKey.Turbo) return;
        UIManager.Instance?.HideTutorial(key);
    }

    /// <summary>
    /// UI Continue buttons should call this.
    /// Handles: SetLearned + success animation + special resume behavior.
    /// </summary>
    public void CompleteTutorial(TutorialKey key)
    {
        if (!TutorialProgress.IsLearned(key))
            TutorialProgress.SetLearned(key);

        UIManager.Instance?.TutorialSuccess(key);

        switch (key)
        {
            case TutorialKey.Momentum:
                FinishMomentumTutorial();
                break;

            case TutorialKey.Turbo:
                FinishTurboTutorial();
                break;
        }
    }

    /// <summary>Clear all learned flags and restart level (debug / dev menu).</summary>
    public void ResetAllTutorialsAndRestart()
    {
        // Unfreeze (just in case we were paused)
        Time.timeScale = 1f;
        SetCursorForGameplay();

        HideMaskInstant(_momentumFocusMask);
        HideMaskInstant(_turboFocusMask);

        TutorialProgress.ResetAll();

        _shownMoveThisSession = false;
        _shownMomentumThisSession = false;
        _shownTurboThisSession = false;
        _momentumSequenceRunning = false;
        _turboSequenceRunning = false;

        // Reset momentum and re-apply gate on first level (if supported)
        TryResetMomentum();
        ApplyMomentumAndTurboGateIfNeeded(IsTutorialLevelActive());

        GameManager.Instance?.RestartLevel();
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Momentum Tutorial (special)

    private void ShowMomentumTutorial()
    {
        if (_shownMomentumThisSession) return;
        if (_momentumSequenceRunning) return;

        _shownMomentumThisSession = true;
        _momentumSequenceRunning = true;

        ResolvePlayerAndInput();
        ResolveGauge();

        // Seed gauge to 50% for explanation (reflection-safe)
        TrySetMomentumPercent(50f);

        // Switch to UI + freeze world
        SwitchToUIMapAndFreezeWorld();

        RunMomentumTutorialSequence().Forget();
    }

    private async UniTaskVoid RunMomentumTutorialSequence()
    {
        var ct = _destroyToken;

        ResolveGauge();

        // Make sure gauge is visible + highlighted (calls are optional via SendMessage)
        TryGaugeShow(true);
        TryGaugeHighlight(true);

        _momentumIntroFeedbacks?.PlayFeedbacks();

        FadeMaskIn(_momentumFocusMask, _momentumFocusFadeDuration);

        await UniTask.Delay(TimeSpan.FromSeconds(_momentumPreTutorialDelay),
            DelayType.Realtime, PlayerLoopTiming.Update, ct);

        if (ct.IsCancellationRequested) return;

        UIManager.Instance?.ShowTutorial(TutorialKey.Momentum);
        // prefer explicit per-tutorial first selected, fallback to generic
        FocusFirstSelectedNextFrame(_momentumFirstSelected ?? _firstTutorialFirstSelected).Forget();

        _momentumSequenceRunning = false;
    }

    private void FinishMomentumTutorial()
    {
        // Un-gate momentum gain (reflection-safe)
        TrySetGainPaused(false);

        TryGaugeHighlight(false);
        FadeMaskOut(_momentumFocusMask, _momentumFocusFadeDuration);

        ResumeWorldToGameplay();
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Turbo Tutorial (special)

    private void ShowTurboTutorial()
    {
        if (_shownTurboThisSession) return;
        if (_turboSequenceRunning) return;

        _shownTurboThisSession = true;
        _turboSequenceRunning = true;

        ResolvePlayerAndInput();

        // UI map (so tutorial UI buttons work) but DONfT freeze yet (camera blends)
        SwitchToUIMapNoFreeze();

        RunTurboTutorialSequence().Forget();
    }

    private async UniTaskVoid RunTurboTutorialSequence()
    {
        var ct = _destroyToken;

        // Ensure world running for blends
        Time.timeScale = 1f;
        SetCursorForGameplay();

#if CINEMACHINE
        ResolveTurboCameras();

        if (_turboPlayerCam != null && _turboTrapCam != null)
        {
            _turboPlayerCam.gameObject.SetActive(true);
            _turboTrapCam.gameObject.SetActive(true);

            int basePriority = 10;
            _turboPlayerCam.Priority = basePriority;
            _turboTrapCam.Priority = basePriority - 1;

            // Blend to trap
            _turboTrapCam.Priority = basePriority + 1;
            await UniTask.Delay(TimeSpan.FromSeconds(_turboToTrapBlendTime),
                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            if (ct.IsCancellationRequested) return;

            // Hold
            await UniTask.Delay(TimeSpan.FromSeconds(_turboTrapHoldTime),
                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            if (ct.IsCancellationRequested) return;

            // Blend back
            _turboPlayerCam.Priority = basePriority + 2;
            _turboTrapCam.Priority = basePriority - 1;
            await UniTask.Delay(TimeSpan.FromSeconds(_turboBackBlendTime),
                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            if (ct.IsCancellationRequested) return;

            // Restore
            _turboPlayerCam.Priority = basePriority;
            _turboTrapCam.Priority = basePriority - 1;
        }
#endif

        // Now pause + show UI
        Time.timeScale = 0f;
        SetCursorForUI();

        FadeMaskIn(_turboFocusMask, _turboFocusFadeDuration);

        await UniTask.Delay(TimeSpan.FromSeconds(_turboPreTutorialDelay),
            DelayType.Realtime, PlayerLoopTiming.Update, ct);

        if (ct.IsCancellationRequested) return;

        UIManager.Instance?.ShowTutorial(TutorialKey.Turbo);
        // prefer explicit per-tutorial first selected
        FocusFirstSelectedNextFrame(_turboFirstSelected).Forget();

        _turboSequenceRunning = false;
    }

    private void FinishTurboTutorial()
    {
        FadeMaskOut(_turboFocusMask, _turboFocusFadeDuration);
        SetTurboTutorialGate(true);
        ResumeWorldToGameplay();
    }

#if CINEMACHINE
    private void ResolveTurboCameras()
    {
        if (!IsPlayingScene()) return;

        ResolvePlayerAndInput();
        var player = _player;

        // Player cam
        if (_turboPlayerCam == null || !_turboPlayerCam.gameObject.scene.IsValid())
        {
            if (!string.IsNullOrEmpty(_turboPlayerCamTag))
            {
                var go = GameObject.FindGameObjectWithTag(_turboPlayerCamTag);
                if (go != null) _turboPlayerCam = go.GetComponent<CinemachineCamera>();
            }

            if (_turboPlayerCam == null && player != null)
            {
#if UNITY_6000_0_OR_NEWER
                var cams = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
#else
                var cams = UnityEngine.Object.FindObjectsOfType<CinemachineCamera>();
#endif
                foreach (var cam in cams)
                {
                    if (cam == null) continue;
                    if (cam.Follow == player.transform || cam.LookAt == player.transform)
                    {
                        _turboPlayerCam = cam;
                        break;
                    }
                }
            }

            if (_turboPlayerCam == null && _log)
                Debug.LogWarning("[TutorialManager] Turbo player cam not found. Assign or tag it.");
        }

        // Trap cam
        if (_turboTrapCam == null || !_turboTrapCam.gameObject.scene.IsValid())
        {
            if (!string.IsNullOrEmpty(_turboTrapCamTag))
            {
                var go = GameObject.FindGameObjectWithTag(_turboTrapCamTag);
                if (go != null) _turboTrapCam = go.GetComponent<CinemachineCamera>();
            }

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

            if (_turboTrapCam == null && _log)
                Debug.LogWarning("[TutorialManager] Turbo trap cam not found. Assign or tag it.");
        }
    }
#endif

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Scene resolving

    private void ResolvePlayerAndInput()
    {
        // Prefer GameManager refs if available
        if (GameManager.Instance != null)
        {
            _player = GameManager.Instance.Player;
            _playerInput = GameManager.Instance.PlayerInput;
        }

        // Fallbacks
        if (_player == null)
        {
#if UNITY_6000_0_OR_NEWER
            _player = UnityEngine.Object.FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Exclude);
#else
            _player = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
#endif
        }

        if (_playerInput == null && _player != null)
            _playerInput = _player.GetComponent<PlayerInput>() ?? _player.GetComponentInChildren<PlayerInput>();
    }

    private void ResolveGauge()
    {
        if (_gauge != null) return;

        // Prefer under player
        if (_player != null)
            _gauge = _player.GetComponentInChildren<MomentumGaugeUI>(true);

        // Fallback: anywhere in scene
        if (_gauge == null)
        {
#if UNITY_6000_0_OR_NEWER
            _gauge = UnityEngine.Object.FindFirstObjectByType<MomentumGaugeUI>(FindObjectsInactive.Include);
#else
            _gauge = UnityEngine.Object.FindObjectOfType<MomentumGaugeUI>(true);
#endif
        }
    }

    private void ResolveMomentumIntroFeedbacks()
    {
        _momentumIntroFeedbacks = null;
        if (!IsTutorialLevelActive()) return;
        if (string.IsNullOrEmpty(_momentumIntroTag)) return;

        var obj = GameObject.FindWithTag(_momentumIntroTag);
        if (obj != null) _momentumIntroFeedbacks = obj.GetComponent<MMF_Player>();
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Input + pause helpers

    private void SwitchToUIMapAndFreezeWorld()
    {
        SwitchToUIMapNoFreeze();
        Time.timeScale = 0f;
        SetCursorForUI();
    }

    private void SwitchToUIMapNoFreeze()
    {
        ResolvePlayerAndInput();

        _player?.DisableInput();

        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();

            var uiMap = _playerInput.actions.FindActionMap(MAP_UI, false);
            if (uiMap != null)
            {
                _playerInput.defaultActionMap = MAP_UI;
                _playerInput.SwitchCurrentActionMap(MAP_UI);
            }
        }
        _player?.StopHorizontalInstant();
        _player?.GetComponent<PlayerInputRouter>()?.ClearAll();
    }

    private void ResumeWorldToGameplay()
    {
        ResolvePlayerAndInput();

        Time.timeScale = 1f;

        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();

            var playerMap = _playerInput.actions.FindActionMap(MAP_PLAYER, false);
            if (playerMap != null)
            {
                _playerInput.defaultActionMap = MAP_PLAYER;
                _playerInput.SwitchCurrentActionMap(MAP_PLAYER);
            }
        }

        _player?.EnableInput();
        SetCursorForGameplay();
        _player?.SetFrozen(false);
        _player?.GetComponent<PlayerInputRouter>()?.ClearAll();
    }

    private static void SetCursorForUI()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private static void SetCursorForGameplay()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private async UniTaskVoid FocusFirstSelectedNextFrame(GameObject target = null)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, _destroyToken);

        var es = EventSystem.current;
        if (es == null) return; 

        var toSelect = target ?? _firstTutorialFirstSelected;
        if (toSelect != null && toSelect.activeInHierarchy)
        {
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(toSelect);
        }
    }

    /// <summary>
    /// Begin the unified flow that used to live on GameManager:
    /// - hide gameplay HUD, switch to UI map, disable player input
    /// - wait for hud reveal delay (DeltaTime)
    /// - reveal HUD, resume gameplay and notify TutorialManager that gameplay began
    /// Public so GameManager can call it.
    /// </summary>
    public async UniTaskVoid BeginGameplayAfterIntroAsync(CancellationToken ctOuter)
    {
        // cancel any previous schedule
        _hudRevealCts?.Cancel();
        _hudRevealCts?.Dispose();
        _hudRevealCts = new CancellationTokenSource();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ctOuter, _hudRevealCts.Token);
        var ct = linked.Token;

        // 0) Hide HUD & lock gameplay during intro
        SetGameplayUIVisible(false);
        // Keep UI map active and disable player input
        SwitchToUIMapNoFreeze();

        // 1) Wait for HP / SP intro animation time
        await UniTask.Delay(TimeSpan.FromSeconds(_hudRevealDelay),
                            DelayType.DeltaTime,
                            PlayerLoopTiming.Update,
                            ct);

        if (ct.IsCancellationRequested || !IsPlayingScene())
            return;

        // 2) Reveal HUD (HP/SP bars, gauge, etc.)
        SetGameplayUIVisible(true);

        // 3) Start gameplay normally (resume world + player input)
        ResumeWorldToGameplay();

        // Delegate first-level momentum gating & first-move tutorial
        OnGameplayBegan();

        // cursor should be hidden during gameplay
        SetCursorForGameplay();
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Mask helpers

    private static void FadeMaskIn(CanvasGroup cg, float duration)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.gameObject.SetActive(true);
        cg.alpha = 0f;
        cg.DOFade(1f, duration).SetUpdate(true);
    }

    private static void FadeMaskOut(CanvasGroup cg, float duration)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.DOFade(0f, duration)
          .SetUpdate(true)
          .OnComplete(() =>
          {
              if (cg != null) cg.gameObject.SetActive(false);
          });
    }

    private static void HideMaskInstant(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.alpha = 0f;
        cg.gameObject.SetActive(false);
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // First-level gating

    private void ApplyMomentumAndTurboGateIfNeeded(bool isFirstLevel)
    {
        bool momentumGate = isFirstLevel && !TutorialProgress.IsLearned(TutorialKey.Momentum);

        TrySetGainPaused(momentumGate);
        if (momentumGate)
            TryResetMomentum();

        // NEW: Turbo gate mirrors Momentum gate style
        bool turboGate = isFirstLevel && !TutorialProgress.IsLearned(TutorialKey.Turbo);
        SetTurboTutorialGate(!turboGate);
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Compatibility helpers (reflection / SendMessage)

    private void TrySetGainPaused(bool paused)
    {
        var mm = MomentumManager.Instance;
        if (mm == null) return;

        var mi = mm.GetType().GetMethod("SetGainPaused", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null)
        {
            mi.Invoke(mm, new object[] { paused });
            return;
        }

        if (_log)
            Debug.LogWarning("[TutorialManager] MomentumManager.SetGainPaused(bool) not found. (No compile error; just skipping gate.)");
    }

    private void TrySetMomentumPercent(float percent)
    {
        var mm = MomentumManager.Instance;
        if (mm == null) return;

        // 1) Prefer SetMomentumPercent(float)
        var mi = mm.GetType().GetMethod("SetMomentumPercent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null)
        {
            mi.Invoke(mm, new object[] { percent });
            return;
        }

        // 2) Fallback: Reset + AddMomentum(max * percent/100)
        try
        {
            mm.ResetAll();
            float amount = mm.MaxMomentum * (percent / 100f);
            mm.AddMomentum(amount);
        }
        catch
        {
            // ignore
        }
    }

    private void SetTurboTutorialGate(bool unlocked)
    {
        if (TurboModeManager.Instance != null)
            TurboModeManager.Instance.SetTurboUnlocked(unlocked);

        // Update the cooldown UI visuals too
        var ui = FindFirstObjectByType<TurboCooldownUI>(FindObjectsInactive.Include);
        if (ui != null)
            ui.SetTutorialUnlocked(unlocked);
    }


    private void TryResetMomentum()
    {
        var mm = MomentumManager.Instance;
        if (mm == null) return;

        try { mm.ResetAll(); } catch { }
    }

    private void TryGaugeHighlight(bool on)
    {
        if (_gauge == null) return;
        _gauge.gameObject.SendMessage("ShowTutorialHighlight", on, SendMessageOptions.DontRequireReceiver);
    }

    private void TryGaugeShow(bool show)
    {
        if (_gauge == null) return;

        if (show) _gauge.gameObject.SendMessage("TL_ShowGauge", SendMessageOptions.DontRequireReceiver);
        else _gauge.gameObject.SendMessage("TL_HideGauge", SendMessageOptions.DontRequireReceiver);
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // State helpers

    public bool IsTutorialLevelActive()
    {
        // Prefer GameManager gate if available
        if (GameManager.Instance != null)
            return GameManager.Instance.IsTutorialLevelActive();

        return SceneManager.GetActiveScene().name == _firstLevelName;
    }

    private bool IsPlayingScene()
    {
        return GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
    }

    // Helper used by the unified flow to show/hide gameplay UI and gauge
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
        // Defer to TutorialManager policy
        return ShouldShowMomentumGauge();
    }
}
