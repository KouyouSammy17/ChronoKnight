// チュートリアル全体を統括する永続シングルトン。モメンタム・ターボの特殊演出やゲームプレイ開始フローも管理する
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
    public static TutorialManager Instance { get; private set; }    // シングルトンインスタンス

    private const string MAP_PLAYER = "Player"; // プレイヤー入力マップ名
    private const string MAP_UI = "UI";         // UIナビゲーション入力マップ名

    [Header("Level Gate")]
    [SerializeField] private string _firstLevelName = "Level_01";   // チュートリアルを適用するレベル名

    [Header("First Selected (optional)")]
    [SerializeField] private GameObject _firstTutorialFirstSelected;    // チュートリアルUIで最初に選択するオブジェクト（汎用）
    [SerializeField] private GameObject _momentumFirstSelected;         // モメンタムチュートリアル専用の最初の選択オブジェクト
    [SerializeField] private GameObject _turboFirstSelected;            // ターボチュートリアル専用の最初の選択オブジェクト

    // ── HUD delayed reveal (moved from GameManager)
    [SerializeField] private float _hudRevealDelay = 5f;            // イントロ後にHUDを表示するまでの待機時間（秒）
    // CTS for scheduling reveal
    private CancellationTokenSource _hudRevealCts;  // HUD表示スケジュールのキャンセルトークンソース

    [Header("Momentum Tutorial Reveal")]
    [SerializeField] private CanvasGroup _momentumFocusMask;        // ゲージ周辺にフォーカスさせる暗幕パネル
    [SerializeField] private float _momentumFocusFadeDuration = 0.35f;  // 暗幕のフェード時間
    [SerializeField] private float _momentumPreTutorialDelay = 0.5f;    // 暗幕表示後にUI表示するまでの遅延
    [SerializeField] private MMF_Player _momentumIntroFeedbacks;        // モメンタム演出フィードバック
    [SerializeField] private string _momentumIntroTag = "MomentumIntro"; // 演出フィードバックを見つけるタグ

    [Header("Turbo Tutorial Reveal")]
    [SerializeField] private CanvasGroup _turboFocusMask;               // ターボチュートリアル用の暗幕パネル
    [SerializeField] private float _turboFocusFadeDuration = 0.35f;     // 暗幕のフェード時間
    [SerializeField] private float _turboPreTutorialDelay = 0.5f;       // 暗幕表示後にUI表示するまでの遅延

#if CINEMACHINE
    [Header("Turbo Tutorial Cameras (Manual)")]
    [SerializeField] private CinemachineCamera _turboPlayerCam;     // ゲームプレイ用メインカメラ
    [SerializeField] private CinemachineCamera _turboTrapCam;       // ギミック・トラップ紹介用カメラ
    [SerializeField] private float _turboToTrapBlendTime = 0.8f;    // プレイヤーカメラからトラップカメラへのブレンド時間
    [SerializeField] private float _turboTrapHoldTime = 1.0f;       // トラップカメラを表示し続ける時間
    [SerializeField] private float _turboBackBlendTime = 0.8f;      // プレイヤーカメラへ戻るブレンド時間

    [Header("Turbo Tutorial Camera Auto-Fetch")]
    [SerializeField] private string _turboPlayerCamTag = "TurboPlayerCam";  // プレイヤーカメラを自動検索するタグ
    [SerializeField] private string _turboTrapCamTag = "TurboTrapCam";      // トラップカメラを自動検索するタグ
#endif

    [Header("Ready / Go UI")]
    [SerializeField] private ReadyGoUI_Anim _readyGoUI;     // 「Ready? Go!」UIアニメーション参照

    [Header("Debug")]
    [SerializeField] private bool _log;     // デバッグログを有効にするか

    // Cached (scene-dependent)
    private PlayerMotor _player;            // シーン内のプレイヤーモーター参照
    private PlayerInput _playerInput;       // プレイヤーの入力コンポーネント参照
    private MomentumGaugeUI _gauge;         // モメンタムゲージUIの参照

    // Per-session flags
    private bool _shownMomentumThisSession; // 今セッションでモメンタムチュートリアルを表示したか
    private bool _shownTurboThisSession;    // 今セッションでターボチュートリアルを表示したか

    // Guards
    private bool _momentumSequenceRunning;  // モメンタムチュートリアルシーケンスが実行中か
    private bool _turboSequenceRunning;     // ターボチュートリアルシーケンスが実行中か

    private CancellationToken _destroyToken;    // オブジェクト破棄時にasyncを中断するトークン

    // ─────────────────────────────────────────────────────────────
    // Unity lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);    // 重複インスタンスを破棄
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);  // シーン遷移後も破棄しない

        _destroyToken = this.GetCancellationTokenOnDestroy();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;  // シーンロードイベントを購読
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;  // シーンロードイベントの購読を解除
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

        ResolveSceneReferences();   // 新しいシーンの参照を再取得
    }

    // ─────────────────────────────────────────────────────────────
    // Public API (called by GameManager / triggers / UI)

    /// <summary>GameManager should call this after intro/HUD reveal when actual gameplay starts.</summary>
    public void OnGameplayBegan()
    {
        if (!IsPlayingScene()) return;

        ApplyMomentumAndTurboGateIfNeeded(IsTutorialLevelActive()); // チュートリアルレベルならゲートを適用

        // Show Move tutorial on first level if not already learned
        if (IsTutorialLevelActive() && !TutorialProgress.IsLearned(TutorialKey.Move))
        {
            UIManager.Instance?.ShowTutorial(TutorialKey.Move); // 移動チュートリアルをまだ見ていなければ表示
        }
    }

    /// <summary>GameManager uses this when deciding whether to show the HUD gauge.</summary>
    public bool ShouldShowMomentumGauge()
    {
        if (!IsTutorialLevelActive()) return true;          // チュートリアルレベルでなければ常に表示
        return TutorialProgress.IsLearned(TutorialKey.Momentum); // モメンタムチュートリアル済みなら表示
    }

    /// <summary>Call this on every scene load (GameManager already calls it).</summary>
    public void ResolveSceneReferences()
    {
        ResolvePlayerAndInput();
        ResolveGauge();
        ResolveMomentumIntroFeedbacks();

        // Resolve Ready/Go UI in scene (TutorialManager is persistent, UI may be scene-local)
        if (_readyGoUI == null)
        {
#if UNITY_6000_0_OR_NEWER
            _readyGoUI = UnityEngine.Object.FindFirstObjectByType<ReadyGoUI_Anim>(FindObjectsInactive.Include);
#else
            _readyGoUI = UnityEngine.Object.FindObjectOfType<ReadyGoUI_Anim>(true);
#endif
        }

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
        if (TutorialProgress.IsLearned(key)) return;    // 学習済みなら表示しない

        switch (key)
        {
            case TutorialKey.Momentum:
                ShowMomentumTutorial(); // モメンタムは専用の演出シーケンスで表示
                break;

            case TutorialKey.Turbo:
                ShowTurboTutorial();    // ターボはカメラツアー付きの専用シーケンスで表示
                break;

            default:
                UIManager.Instance?.ShowTutorial(key); // 通常チュートリアルはUIManagerに委譲
                break;
        }
    }

    /// <summary>Triggers should call this on exit to hide (Momentum/Turbo ignore hides because modal).</summary>
    public void RequestHide(TutorialKey key)
    {
        if (key == TutorialKey.Momentum || key == TutorialKey.Turbo) return; // モーダル型チュートリアルは退出では隠さない
        UIManager.Instance?.HideTutorial(key);
    }

    /// <summary>
    /// UI Continue buttons should call this.
    /// Handles: SetLearned + success animation + special resume behavior.
    /// </summary>
    public void CompleteTutorial(TutorialKey key)
    {
        if (!TutorialProgress.IsLearned(key))
            TutorialProgress.SetLearned(key);   // 未学習なら学習済みとして保存

        UIManager.Instance?.TutorialSuccess(key);   // 成功アニメーションを再生

        switch (key)
        {
            case TutorialKey.Momentum:
                FinishMomentumTutorial();   // モメンタム専用の終了処理
                break;

            case TutorialKey.Turbo:
                FinishTurboTutorial();      // ターボ専用の終了処理
                break;
        }
    }

    /// <summary>Clear all learned flags and restart level (debug / dev menu).</summary>
    public void ResetAllTutorialsAndRestart()
    {
        // Unfreeze (just in case we were paused)
        Time.timeScale = 1f;
        SetCursorForGameplay();

        HideMaskInstant(_momentumFocusMask);    // 暗幕を即座に非表示
        HideMaskInstant(_turboFocusMask);

        TutorialProgress.ResetAll();    // 全チュートリアルの学習フラグをリセット

        _shownMomentumThisSession = false;
        _shownTurboThisSession = false;
        _momentumSequenceRunning = false;
        _turboSequenceRunning = false;

        // Reset momentum and re-apply gate on first level (if supported)
        TryResetMomentum();
        ApplyMomentumAndTurboGateIfNeeded(IsTutorialLevelActive());

        // Show Move tutorial when reset is triggered
        UIManager.Instance?.ShowTutorial(TutorialKey.Move);

        GameManager.Instance?.RestartLevel();   // レベルを再スタート
    }

    // ─────────────────────────────────────────────────────────────
    // Momentum Tutorial (special)

    private void ShowMomentumTutorial()
    {
        if (_shownMomentumThisSession) return;  // 同一セッションで2回表示しない
        if (_momentumSequenceRunning) return;   // すでに実行中なら何もしない

        _shownMomentumThisSession = true;
        _momentumSequenceRunning = true;

        ResolvePlayerAndInput();
        ResolveGauge();

        // Seed gauge to 50% for explanation (reflection-safe)
        TrySetMomentumPercent(50f); // 説明のためにゲージを50%に設定

        // Switch to UI + freeze world
        SwitchToUIMapAndFreezeWorld();

        RunMomentumTutorialSequence().Forget();
    }

    private async UniTaskVoid RunMomentumTutorialSequence()
    {
        var ct = _destroyToken;

        ResolveGauge();

        // Make sure gauge is visible + highlighted (calls are optional via SendMessage)
        TryGaugeShow(true);         // ゲージを表示
        TryGaugeHighlight(true);    // ゲージをハイライト表示

        _momentumIntroFeedbacks?.PlayFeedbacks();   // 演出フィードバックを再生

        FadeMaskIn(_momentumFocusMask, _momentumFocusFadeDuration); // 暗幕をフェードイン

        await UniTask.Delay(TimeSpan.FromSeconds(_momentumPreTutorialDelay),
            DelayType.Realtime, PlayerLoopTiming.Update, ct);  // 一定時間待機（リアルタイム）

        if (ct.IsCancellationRequested) return;

        UIManager.Instance?.ShowTutorial(TutorialKey.Momentum);
        // prefer explicit per-tutorial first selected, fallback to generic
        FocusFirstSelectedNextFrame(_momentumFirstSelected ?? _firstTutorialFirstSelected).Forget(); // 次フレームで最初のUI要素にフォーカス

        _momentumSequenceRunning = false;
    }

    private void FinishMomentumTutorial()
    {
        // Un-gate momentum gain (reflection-safe)
        TrySetGainPaused(false);        // モメンタム獲得のゲートを解除

        TryGaugeHighlight(false);       // ゲージのハイライトを解除
        FadeMaskOut(_momentumFocusMask, _momentumFocusFadeDuration);    // 暗幕をフェードアウト

        ResumeWorldToGameplay();        // ゲームプレイを再開
    }

    // ─────────────────────────────────────────────────────────────
    // Turbo Tutorial (special)

    private void ShowTurboTutorial()
    {
        if (_shownTurboThisSession) return;     // 同一セッションで2回表示しない
        if (_turboSequenceRunning) return;      // すでに実行中なら何もしない

        _shownTurboThisSession = true;
        _turboSequenceRunning = true;

        ResolvePlayerAndInput();

        // UI map (so tutorial UI buttons work) but DON'T freeze yet (camera blends)
        SwitchToUIMapNoFreeze();    // UIマップに切り替えるが時間は止めない（カメラブレンドのため）

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
            _turboTrapCam.Priority = basePriority - 1;  // 最初はプレイヤーカメラが優先

            // Blend to trap
            _turboTrapCam.Priority = basePriority + 1;  // トラップカメラを優先度で上回る
            await UniTask.Delay(TimeSpan.FromSeconds(_turboToTrapBlendTime),
                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            if (ct.IsCancellationRequested) return;

            // Hold
            await UniTask.Delay(TimeSpan.FromSeconds(_turboTrapHoldTime),
                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);  // トラップを見せる時間を確保
            if (ct.IsCancellationRequested) return;

            // Blend back
            _turboPlayerCam.Priority = basePriority + 2;    // プレイヤーカメラを最優先に戻す
            _turboTrapCam.Priority = basePriority - 1;
            await UniTask.Delay(TimeSpan.FromSeconds(_turboBackBlendTime),
                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            if (ct.IsCancellationRequested) return;

            // Restore
            _turboPlayerCam.Priority = basePriority;        // 優先度を通常に戻す
            _turboTrapCam.Priority = basePriority - 1;
        }
#endif

        // Now pause + show UI
        Time.timeScale = 0f;        // ゲームを一時停止
        SetCursorForUI();

        FadeMaskIn(_turboFocusMask, _turboFocusFadeDuration);   // 暗幕をフェードイン

        await UniTask.Delay(TimeSpan.FromSeconds(_turboPreTutorialDelay),
            DelayType.Realtime, PlayerLoopTiming.Update, ct);  // リアルタイムで遅延（ポーズ中のため）

        if (ct.IsCancellationRequested) return;

        UIManager.Instance?.ShowTutorial(TutorialKey.Turbo);
        // prefer explicit per-tutorial first selected
        FocusFirstSelectedNextFrame(_turboFirstSelected).Forget();  // ターボUI専用の最初の選択にフォーカス

        _turboSequenceRunning = false;
    }

    private void FinishTurboTutorial()
    {
        FadeMaskOut(_turboFocusMask, _turboFocusFadeDuration);  // 暗幕をフェードアウト
        SetTurboTutorialGate(true);     // ターボ機能のゲートを解除
        ResumeWorldToGameplay();        // ゲームプレイを再開
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
                var go = GameObject.FindGameObjectWithTag(_turboPlayerCamTag); // タグでプレイヤーカメラを検索
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
                        _turboPlayerCam = cam;  // プレイヤーを追従・注視しているカメラをプレイヤーカメラとみなす
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
                var go = GameObject.FindGameObjectWithTag(_turboTrapCamTag); // タグでトラップカメラを検索
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
                    _turboTrapCam = cam;    // プレイヤーカメラ以外の最初のカメラをトラップカメラとして使用
                    break;
                }
            }

            if (_turboTrapCam == null && _log)
                Debug.LogWarning("[TutorialManager] Turbo trap cam not found. Assign or tag it.");
        }
    }
#endif

    // ─────────────────────────────────────────────────────────────
    // Scene resolving

    private void ResolvePlayerAndInput()
    {
        // Prefer GameManager refs if available
        if (GameManager.Instance != null)
        {
            _player = GameManager.Instance.Player;
            _playerInput = GameManager.Instance.PlayerInput;    // GameManagerからプレイヤー参照を優先取得
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
            _playerInput = _player.GetComponent<PlayerInput>() ?? _player.GetComponentInChildren<PlayerInput>(); // PlayerInputをプレイヤー配下から取得
    }

    private void ResolveGauge()
    {
        if (_gauge != null) return;     // すでに取得済みならスキップ

        // Prefer under player
        if (_player != null)
            _gauge = _player.GetComponentInChildren<MomentumGaugeUI>(true);    // プレイヤー配下から優先検索

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
        if (!IsTutorialLevelActive()) return;                       // チュートリアルレベルでなければスキップ
        if (string.IsNullOrEmpty(_momentumIntroTag)) return;

        var obj = GameObject.FindWithTag(_momentumIntroTag);
        if (obj != null) _momentumIntroFeedbacks = obj.GetComponent<MMF_Player>(); // タグからフィードバックを取得
    }

    // ─────────────────────────────────────────────────────────────
    // Input + pause helpers

    private void SwitchToUIMapAndFreezeWorld()
    {
        SwitchToUIMapNoFreeze();
        Time.timeScale = 0f;    // ゲームを一時停止
        SetCursorForUI();
    }

    private void SwitchToUIMapNoFreeze()
    {
        ResolvePlayerAndInput();

        _player?.DisableInput();    // プレイヤーの入力を無効化

        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();

            var uiMap = _playerInput.actions.FindActionMap(MAP_UI, false);
            if (uiMap != null)
            {
                _playerInput.defaultActionMap = MAP_UI;
                _playerInput.SwitchCurrentActionMap(MAP_UI);    // 入力マップをUIモードに切り替え
            }
        }
        _player?.StopHorizontalInstant();   // 水平移動を即時停止
        _player?.GetComponent<PlayerInputRouter>()?.ClearAll(); // 入力ルーターをリセット
    }

    private void ResumeWorldToGameplay()
    {
        ResolvePlayerAndInput();

        Time.timeScale = 1f;    // ゲームを再開

        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();

            var playerMap = _playerInput.actions.FindActionMap(MAP_PLAYER, false);
            if (playerMap != null)
            {
                _playerInput.defaultActionMap = MAP_PLAYER;
                _playerInput.SwitchCurrentActionMap(MAP_PLAYER);    // 入力マップをプレイヤーモードに戻す
            }
        }

        _player?.EnableInput();             // プレイヤーの入力を再有効化
        SetCursorForGameplay();
        _player?.SetFrozen(false);          // プレイヤーのフリーズを解除
        _player?.GetComponent<PlayerInputRouter>()?.ClearAll();
    }

    private static void SetCursorForUI()
    {
        Cursor.visible = true;              // UI操作中はカーソルを表示
        Cursor.lockState = CursorLockMode.None;
    }

    private static void SetCursorForGameplay()
    {
        Cursor.visible = false;             // ゲームプレイ中はカーソルを非表示
        Cursor.lockState = CursorLockMode.Locked;
    }

    private async UniTaskVoid FocusFirstSelectedNextFrame(GameObject target = null)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, _destroyToken);    // 1フレーム待ってからフォーカスを設定

        var es = EventSystem.current;
        if (es == null) return;

        var toSelect = target ?? _firstTutorialFirstSelected;   // 指定がなければデフォルトのオブジェクトを使用
        if (toSelect != null && toSelect.activeInHierarchy)
        {
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(toSelect); // 指定のUIオブジェクトにフォーカスを移動
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
        SetGameplayUIVisible(false);    // イントロ中はHUDを非表示
        // Keep UI map active and disable player input
        SwitchToUIMapNoFreeze();

        // 1) Wait for HP / SP intro animation time
        await UniTask.Delay(TimeSpan.FromSeconds(_hudRevealDelay),
                            DelayType.DeltaTime,
                            PlayerLoopTiming.Update,
                            ct);    // HP/SPバーのイントロアニメーション時間を待機

        if (ct.IsCancellationRequested || !IsPlayingScene())
            return;

        // 2) Reveal HUD (HP/SP bars, gauge, etc.) — but check if Time Attack first
        bool isTimeAttack = GameManager.Instance != null && GameManager.Instance.IsTimeAttackStage;

        // For Time Attack, delay HUD reveal until after Ready/Go
        if (!isTimeAttack)
        {
            SetGameplayUIVisible(true); // タイムアタック以外は通常通りHUDを表示
        }

        // 2.5) READY? GO! (ONLY on Level_02)
        bool shouldPlayReadyGo = _readyGoUI != null && isTimeAttack;

        if (shouldPlayReadyGo)
        {
            // Ensure TimeAttack is configured so the timer UI will show when we start the run
            TimeAttackManager.Instance?.Configure(true);

            _readyGoUI.Play();  // 「Ready? Go!」アニメーションを再生

            await UniTask.Delay(TimeSpan.FromSeconds(_readyGoUI.TotalDuration),
                                DelayType.Realtime,
                                PlayerLoopTiming.Update,
                                ct);    // アニメーション終了まで待機（リアルタイム）
            if (ct.IsCancellationRequested) return;

            // NOW reveal HUD (time UI will activate) and start the timer
            SetGameplayUIVisible(true);

            // Explicitly ensure timer UI is visible
            var timerUI = FindFirstObjectByType<TimeAttackTimerUI>(FindObjectsInactive.Include);
            if (timerUI != null)
                timerUI.EnsureVisible();    // タイマーUIを確実に表示する

            TimeAttackManager.Instance?.StartRun();     // タイムアタックの計測を開始
        }
        else if (!shouldPlayReadyGo && isTimeAttack)
        {
            // Fallback: if Ready/Go UI not found, still show HUD on Time Attack
            SetGameplayUIVisible(true); // ReadyGoUIが見つからない場合のフォールバック
        }

        // 3) Start gameplay normally (resume world + player input)
        ResumeWorldToGameplay();

        // Delegate first-level momentum gating & first-move tutorial
        OnGameplayBegan();
        GameManager.Instance?.NotifyGameplayBegan();
        // cursor should be hidden during gameplay
        SetCursorForGameplay();
    }

    // ─────────────────────────────────────────────────────────────
    // Mask helpers

    private static void FadeMaskIn(CanvasGroup cg, float duration)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.gameObject.SetActive(true);
        cg.alpha = 0f;
        cg.DOFade(1f, duration).SetUpdate(true);    // 暗幕をフェードイン（ポーズ中でも動作）
    }

    private static void FadeMaskOut(CanvasGroup cg, float duration)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.DOFade(0f, duration)
          .SetUpdate(true)
          .OnComplete(() =>
          {
              if (cg != null) cg.gameObject.SetActive(false); // フェードアウト完了後に非表示
          });
    }

    private static void HideMaskInstant(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.alpha = 0f;
        cg.gameObject.SetActive(false); // アニメーションなしで即座に非表示
    }

    // ─────────────────────────────────────────────────────────────
    // First-level gating

    private void ApplyMomentumAndTurboGateIfNeeded(bool isFirstLevel)
    {
        bool momentumGate = isFirstLevel && !TutorialProgress.IsLearned(TutorialKey.Momentum); // モメンタム未学習なら獲得をゲート

        TrySetGainPaused(momentumGate);
        if (momentumGate)
            TryResetMomentum(); // ゲート中はモメンタムをリセットしておく

        // NEW: Turbo gate mirrors Momentum gate style
        bool turboGate = isFirstLevel && !TutorialProgress.IsLearned(TutorialKey.Turbo);   // ターボ未学習なら使用をゲート
        SetTurboTutorialGate(!turboGate);
    }

    // ─────────────────────────────────────────────────────────────
    // Compatibility helpers (reflection / SendMessage)

    private void TrySetGainPaused(bool paused)
    {
        var mm = MomentumManager.Instance;
        if (mm == null) return;

        // リフレクションでSetGainPausedメソッドを安全に呼び出す
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
            mi.Invoke(mm, new object[] { percent });    // リフレクションで直接パーセント設定
            return;
        }

        // 2) Fallback: Reset + AddMomentum(max * percent/100)
        try
        {
            mm.ResetAll();
            float amount = mm.MaxMomentum * (percent / 100f);  // 最大値に対する割合で量を計算
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
            TurboModeManager.Instance.SetTurboUnlocked(unlocked);  // ターボのアンロック状態を設定

        // Update the cooldown UI visuals too
        var ui = FindFirstObjectByType<TurboCooldownUI>(FindObjectsInactive.Include);
        if (ui != null)
            ui.SetTutorialUnlocked(unlocked);   // クールダウンUIにもアンロック状態を反映
    }


    private void TryResetMomentum()
    {
        var mm = MomentumManager.Instance;
        if (mm == null) return;

        try { mm.ResetAll(); } catch { }    // 失敗しても無視してモメンタムをリセット
    }

    private void TryGaugeHighlight(bool on)
    {
        if (_gauge == null) return;
        _gauge.gameObject.SendMessage("ShowTutorialHighlight", on, SendMessageOptions.DontRequireReceiver); // ゲージのハイライト表示をSendMessageで切り替え
    }

    private void TryGaugeShow(bool show)
    {
        if (_gauge == null) return;

        // SendMessageでゲージの表示・非表示を制御（メソッドが存在しなくても無視）
        if (show) _gauge.gameObject.SendMessage("TL_ShowGauge", SendMessageOptions.DontRequireReceiver);
        else _gauge.gameObject.SendMessage("TL_HideGauge", SendMessageOptions.DontRequireReceiver);
    }

    // ─────────────────────────────────────────────────────────────
    // State helpers

    public bool IsTutorialLevelActive()
    {
        // Prefer GameManager gate if available
        if (GameManager.Instance != null)
            return GameManager.Instance.IsTutorialLevelActive();

        return SceneManager.GetActiveScene().name == _firstLevelName;   // フォールバック：シーン名で判定
    }

    private bool IsPlayingScene()
    {
        return GameManager.Instance != null && GameManager.Instance.State == GameState.Playing; // ゲームが実際にプレイ中かを確認
    }

    // Helper used by the unified flow to show/hide gameplay UI and gauge
    void SetGameplayUIVisible(bool visible)
    {
        UIManager.Instance?.ShowPlayerUI(visible);              // プレイヤーUI全体の表示を切り替え
        if (!visible) UIManager.Instance?.HideAllTutorials();   // 非表示時はすべてのチュートリアルも隠す

        ResolveGauge();  // finds MomentumGaugeUI in scene

        if (_gauge != null)
        {
            bool shouldShowGauge = visible && ShouldGaugeBeVisibleNow();    // 表示条件を確認

            if (shouldShowGauge) _gauge.TL_ShowGauge();     // 条件を満たせばゲージを表示
            else _gauge.TL_HideGauge();                     // 条件を満たさなければゲージを非表示
        }
    }

    private bool ShouldGaugeBeVisibleNow()
    {
        // Defer to TutorialManager policy
        return ShouldShowMomentumGauge();   // モメンタムゲージの表示ポリシーに委譲
    }
}
