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
/// チュートリアルを統括する永続シングルトン。
/// - チュートリアルに関するロジックはすべてここに集約する（GameManagerには書かない）。
/// - トリガーやUIボタンはRequestShow / RequestHide / CompleteTutorialを呼び出すこと。
/// - 特殊なチュートリアル：
///   - モメンタム：一時停止 + 暗幕 + ゲージハイライト + （任意）イントロフィードバック。
///   - ターボ：カメラツアー（任意でCinemachine使用）後に一時停止 + 暗幕 + UI表示。
/// - 最初のレベル専用のルールを管理：
///   - モメンタムチュートリアル学習済みになるまでモメンタム獲得をゲート（MomentumManagerが対応している場合）。
///   - モメンタムチュートリアル学習済みになるまでゲージを非表示（GameManagerがShouldShowMomentumGauge()を使用）。
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

    // ── HUDの遅延表示（GameManagerから移動）
    [SerializeField] private float _hudRevealDelay = 5f;            // イントロ後にHUDを表示するまでの待機時間（秒）
    // 表示スケジュール用のキャンセルトークンソース
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

    // キャッシュ（シーンごとに変わる）
    private PlayerMotor _player;            // シーン内のプレイヤーモーター参照
    private PlayerInput _playerInput;       // プレイヤーの入力コンポーネント参照
    private MomentumGaugeUI _gauge;         // モメンタムゲージUIの参照

    // セッションごとのフラグ
    private bool _shownMomentumThisSession; // 今セッションでモメンタムチュートリアルを表示したか
    private bool _shownTurboThisSession;    // 今セッションでターボチュートリアルを表示したか

    // 実行中ガード
    private bool _momentumSequenceRunning;  // モメンタムチュートリアルシーケンスが実行中か
    private bool _turboSequenceRunning;     // ターボチュートリアルシーケンスが実行中か

    private CancellationToken _destroyToken;    // オブジェクト破棄時にasyncを中断するトークン

    // ─────────────────────────────────────────────────────────────
    // Unityライフサイクル

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
        // シーン参照をクリア
        _player = null;
        _playerInput = null;
        _gauge = null;

        ResolveSceneReferences();   // 新しいシーンの参照を再取得
    }

    // ─────────────────────────────────────────────────────────────
    // 公開API（GameManager・トリガー・UIから呼び出す）

    /// <summary>イントロ／HUD表示後、実際のゲームプレイ開始時にGameManagerから呼び出す。</summary>
    public void OnGameplayBegan()
    {
        if (!IsPlayingScene()) return;

        ApplyMomentumAndTurboGateIfNeeded(IsTutorialLevelActive()); // チュートリアルレベルならゲートを適用

        // 最初のレベルで移動チュートリアルが未学習の場合は表示
        if (IsTutorialLevelActive() && !TutorialProgress.IsLearned(TutorialKey.Move))
        {
            UIManager.Instance?.ShowTutorial(TutorialKey.Move); // 移動チュートリアルをまだ見ていなければ表示
        }
    }

    /// <summary>HUDゲージを表示するかどうかの判断にGameManagerが使用する。</summary>
    public bool ShouldShowMomentumGauge()
    {
        if (!IsTutorialLevelActive()) return true;          // チュートリアルレベルでなければ常に表示
        return TutorialProgress.IsLearned(TutorialKey.Momentum); // モメンタムチュートリアル済みなら表示
    }

    /// <summary>シーンロードのたびに呼び出す（GameManagerがすでに呼び出している）。</summary>
    public void ResolveSceneReferences()
    {
        ResolvePlayerAndInput();
        ResolveGauge();
        ResolveMomentumIntroFeedbacks();

        // シーン内のReady/Go UIを取得（TutorialManagerは永続だがUIはシーンローカルの場合がある）
        if (_readyGoUI == null)
        {
#if UNITY_6000_0_OR_NEWER
            _readyGoUI = UnityEngine.Object.FindFirstObjectByType<ReadyGoUI_Anim>(FindObjectsInactive.Include);
#else
            _readyGoUI = UnityEngine.Object.FindObjectOfType<ReadyGoUI_Anim>(true);
#endif
        }

#if CINEMACHINE
        // 無効なカメラをクリア（シーン変更時）
        if (_turboPlayerCam != null && !_turboPlayerCam.gameObject.scene.IsValid()) _turboPlayerCam = null;
        if (_turboTrapCam != null && !_turboTrapCam.gameObject.scene.IsValid()) _turboTrapCam = null;
#endif
    }

    /// <summary>チュートリアルを表示する際にトリガーから呼び出す。</summary>
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

    /// <summary>退出時にトリガーから非表示を要求する際に呼び出す（モーダル型のモメンタム・ターボは無視する）。</summary>
    public void RequestHide(TutorialKey key)
    {
        if (key == TutorialKey.Momentum || key == TutorialKey.Turbo) return; // モーダル型チュートリアルは退出では隠さない
        UIManager.Instance?.HideTutorial(key);
    }

    /// <summary>
    /// UIの「続ける」ボタンから呼び出す。
    /// SetLearned・成功アニメーション・特殊な再開処理を一括して行う。
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

    /// <summary>すべての学習フラグをリセットしてレベルを再スタートする（デバッグ・開発メニュー用）。</summary>
    public void ResetAllTutorialsAndRestart()
    {
        // フリーズを解除（ポーズ中だった場合のために）
        Time.timeScale = 1f;
        SetCursorForGameplay();

        HideMaskInstant(_momentumFocusMask);    // 暗幕を即座に非表示
        HideMaskInstant(_turboFocusMask);

        TutorialProgress.ResetAll();    // 全チュートリアルの学習フラグをリセット

        _shownMomentumThisSession = false;
        _shownTurboThisSession = false;
        _momentumSequenceRunning = false;
        _turboSequenceRunning = false;

        // モメンタムをリセットして最初のレベルのゲートを再適用（対応している場合）
        TryResetMomentum();
        ApplyMomentumAndTurboGateIfNeeded(IsTutorialLevelActive());

        // リセット時に移動チュートリアルを表示
        UIManager.Instance?.ShowTutorial(TutorialKey.Move);

        GameManager.Instance?.RestartLevel();   // レベルを再スタート
    }

    // ─────────────────────────────────────────────────────────────
    // モメンタムチュートリアル（特殊）

    private void ShowMomentumTutorial()
    {
        if (_shownMomentumThisSession) return;  // 同一セッションで2回表示しない
        if (_momentumSequenceRunning) return;   // すでに実行中なら何もしない

        _shownMomentumThisSession = true;
        _momentumSequenceRunning = true;

        ResolvePlayerAndInput();
        ResolveGauge();

        // 説明用にゲージを50%に設定（リフレクション安全）
        TrySetMomentumPercent(50f); // 説明のためにゲージを50%に設定

        // UIマップに切り替えてゲームを一時停止
        SwitchToUIMapAndFreezeWorld();

        RunMomentumTutorialSequence().Forget();
    }

    private async UniTaskVoid RunMomentumTutorialSequence()
    {
        var ct = _destroyToken;

        ResolveGauge();

        // ゲージを表示してハイライト（SendMessage経由でオプション呼び出し）
        TryGaugeShow(true);         // ゲージを表示
        TryGaugeHighlight(true);    // ゲージをハイライト表示

        _momentumIntroFeedbacks?.PlayFeedbacks();   // 演出フィードバックを再生

        FadeMaskIn(_momentumFocusMask, _momentumFocusFadeDuration); // 暗幕をフェードイン

        await UniTask.Delay(TimeSpan.FromSeconds(_momentumPreTutorialDelay),
            DelayType.Realtime, PlayerLoopTiming.Update, ct);  // 一定時間待機（リアルタイム）

        if (ct.IsCancellationRequested) return;

        UIManager.Instance?.ShowTutorial(TutorialKey.Momentum);
        // チュートリアル専用の選択オブジェクトを優先し、なければ汎用オブジェクトを使用
        FocusFirstSelectedNextFrame(_momentumFirstSelected ?? _firstTutorialFirstSelected).Forget(); // 次フレームで最初のUI要素にフォーカス

        _momentumSequenceRunning = false;
    }

    private void FinishMomentumTutorial()
    {
        // モメンタム獲得のゲートを解除（リフレクション安全）
        TrySetGainPaused(false);        // モメンタム獲得のゲートを解除

        TryGaugeHighlight(false);       // ゲージのハイライトを解除
        FadeMaskOut(_momentumFocusMask, _momentumFocusFadeDuration);    // 暗幕をフェードアウト

        ResumeWorldToGameplay();        // ゲームプレイを再開
    }

    // ─────────────────────────────────────────────────────────────
    // ターボチュートリアル（特殊）

    private void ShowTurboTutorial()
    {
        if (_shownTurboThisSession) return;     // 同一セッションで2回表示しない
        if (_turboSequenceRunning) return;      // すでに実行中なら何もしない

        _shownTurboThisSession = true;
        _turboSequenceRunning = true;

        ResolvePlayerAndInput();

        // UIマップに切り替えてチュートリアルUIボタンを有効化するが、まだ時間は止めない（カメラブレンドのため）
        SwitchToUIMapNoFreeze();

        RunTurboTutorialSequence().Forget();
    }

    private async UniTaskVoid RunTurboTutorialSequence()
    {
        var ct = _destroyToken;

        // ブレンドのためにゲームを実行状態にしておく
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

            // トラップカメラへブレンド
            _turboTrapCam.Priority = basePriority + 1;  // トラップカメラを優先度で上回る
            await UniTask.Delay(TimeSpan.FromSeconds(_turboToTrapBlendTime),
                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            if (ct.IsCancellationRequested) return;

            // 維持
            await UniTask.Delay(TimeSpan.FromSeconds(_turboTrapHoldTime),
                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);  // トラップを見せる時間を確保
            if (ct.IsCancellationRequested) return;

            // プレイヤーカメラへ戻るブレンド
            _turboPlayerCam.Priority = basePriority + 2;    // プレイヤーカメラを最優先に戻す
            _turboTrapCam.Priority = basePriority - 1;
            await UniTask.Delay(TimeSpan.FromSeconds(_turboBackBlendTime),
                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            if (ct.IsCancellationRequested) return;

            // 優先度を元に戻す
            _turboPlayerCam.Priority = basePriority;        // 優先度を通常に戻す
            _turboTrapCam.Priority = basePriority - 1;
        }
#endif

        // 一時停止してUIを表示
        Time.timeScale = 0f;        // ゲームを一時停止
        SetCursorForUI();

        FadeMaskIn(_turboFocusMask, _turboFocusFadeDuration);   // 暗幕をフェードイン

        await UniTask.Delay(TimeSpan.FromSeconds(_turboPreTutorialDelay),
            DelayType.Realtime, PlayerLoopTiming.Update, ct);  // リアルタイムで遅延（ポーズ中のため）

        if (ct.IsCancellationRequested) return;

        UIManager.Instance?.ShowTutorial(TutorialKey.Turbo);
        // ターボ専用の選択オブジェクトを優先使用
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

        // プレイヤーカメラ
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

        // トラップカメラ
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
    // シーン参照の解決

    private void ResolvePlayerAndInput()
    {
        // GameManagerの参照を優先使用
        if (GameManager.Instance != null)
        {
            _player = GameManager.Instance.Player;
            _playerInput = GameManager.Instance.PlayerInput;    // GameManagerからプレイヤー参照を優先取得
        }

        // フォールバック
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

        // プレイヤー配下を優先検索
        if (_player != null)
            _gauge = _player.GetComponentInChildren<MomentumGaugeUI>(true);    // プレイヤー配下から優先検索

        // フォールバック：シーン全体から検索
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
    // 入力 & ポーズのヘルパー

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

        var toSelect = target ?? _firstTutorialFirstSelected;   // 指定がなければデフォルトの選択オブジェクトを使用
        if (toSelect != null && toSelect.activeInHierarchy)
        {
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(toSelect); // 指定のUIオブジェクトにフォーカスを移動
        }
    }

    /// <summary>
    /// GameManagerに存在していた統一フローを開始する：
    /// - ゲームプレイHUDを非表示にし、UIマップに切り替え、プレイヤー入力を無効化
    /// - HUD表示遅延を待機（DeltaTime）
    /// - HUDを表示し、ゲームプレイを再開してTutorialManagerにゲーム開始を通知
    /// GameManagerから呼び出せるようにpublicにしている。
    /// </summary>
    public async UniTaskVoid BeginGameplayAfterIntroAsync(CancellationToken ctOuter)
    {
        // 前回のスケジュールをキャンセル
        _hudRevealCts?.Cancel();
        _hudRevealCts?.Dispose();
        _hudRevealCts = new CancellationTokenSource();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ctOuter, _hudRevealCts.Token);
        var ct = linked.Token;

        // 0) イントロ中はHUDを非表示にしてゲームプレイをロック
        SetGameplayUIVisible(false);    // イントロ中はHUDを非表示
        // UIマップをアクティブのまま保ってプレイヤー入力を無効化
        SwitchToUIMapNoFreeze();

        // 1) HP / SP イントロアニメーションの時間を待機
        await UniTask.Delay(TimeSpan.FromSeconds(_hudRevealDelay),
                            DelayType.DeltaTime,
                            PlayerLoopTiming.Update,
                            ct);    // HP/SPバーのイントロアニメーション時間を待機

        if (ct.IsCancellationRequested || !IsPlayingScene())
            return;

        // 2) HUDを表示（HP/SPバー・ゲージなど）— まずタイムアタックか確認
        bool isTimeAttack = GameManager.Instance != null && GameManager.Instance.IsTimeAttackStage;

        // タイムアタックの場合はReady/Go後までHUD表示を遅らせる
        if (!isTimeAttack)
        {
            SetGameplayUIVisible(true); // タイムアタック以外は通常通りHUDを表示
        }

        // 2.5) READY? GO!（Level_02のみ）
        bool shouldPlayReadyGo = _readyGoUI != null && isTimeAttack;

        if (shouldPlayReadyGo)
        {
            // 計測開始時にタイマーUIが表示されるようTimeAttackを設定
            TimeAttackManager.Instance?.Configure(true);

            _readyGoUI.Play();  // 「Ready? Go!」アニメーションを再生

            await UniTask.Delay(TimeSpan.FromSeconds(_readyGoUI.TotalDuration),
                                DelayType.Realtime,
                                PlayerLoopTiming.Update,
                                ct);    // アニメーション終了まで待機（リアルタイム）
            if (ct.IsCancellationRequested) return;

            // HUDを表示してタイマーを開始（タイマーUIがアクティブになる）
            SetGameplayUIVisible(true);

            // タイマーUIを確実に表示させる
            var timerUI = FindFirstObjectByType<TimeAttackTimerUI>(FindObjectsInactive.Include);
            if (timerUI != null)
                timerUI.EnsureVisible();    // タイマーUIを確実に表示する

            TimeAttackManager.Instance?.StartRun();     // タイムアタックの計測を開始
        }
        else if (!shouldPlayReadyGo && isTimeAttack)
        {
            // フォールバック：ReadyGoUIが見つからない場合もタイムアタックではHUDを表示
            SetGameplayUIVisible(true); // ReadyGoUIが見つからない場合のフォールバック
        }

        // 3) 通常のゲームプレイを開始（ゲームとプレイヤー入力を再開）
        ResumeWorldToGameplay();

        // 最初のレベルのモメンタムゲートと移動チュートリアルの処理を委譲
        OnGameplayBegan();
        GameManager.Instance?.NotifyGameplayBegan();
        // ゲームプレイ中はカーソルを非表示にする
        SetCursorForGameplay();
    }

    // ─────────────────────────────────────────────────────────────
    // 暗幕のヘルパー

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
    // 最初のレベルのゲート処理

    private void ApplyMomentumAndTurboGateIfNeeded(bool isFirstLevel)
    {
        bool momentumGate = isFirstLevel && !TutorialProgress.IsLearned(TutorialKey.Momentum); // モメンタム未学習なら獲得をゲート

        TrySetGainPaused(momentumGate);
        if (momentumGate)
            TryResetMomentum(); // ゲート中はモメンタムをリセットしておく

        // ターボゲートはモメンタムゲートと同じ方式
        bool turboGate = isFirstLevel && !TutorialProgress.IsLearned(TutorialKey.Turbo);   // ターボ未学習なら使用をゲート
        SetTurboTutorialGate(!turboGate);
    }

    // ─────────────────────────────────────────────────────────────
    // 互換性ヘルパー（リフレクション / SendMessage）

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

        // 1) SetMomentumPercent(float)を優先使用
        var mi = mm.GetType().GetMethod("SetMomentumPercent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null)
        {
            mi.Invoke(mm, new object[] { percent });    // リフレクションで直接パーセント設定
            return;
        }

        // 2) フォールバック：リセット後にAddMomentum(max * percent/100)を呼ぶ
        try
        {
            mm.ResetAll();
            float amount = mm.MaxMomentum * (percent / 100f);  // 最大値に対する割合で量を計算
            mm.AddMomentum(amount);
        }
        catch
        {
            // 無視する
        }
    }

    private void SetTurboTutorialGate(bool unlocked)
    {
        if (TurboModeManager.Instance != null)
            TurboModeManager.Instance.SetTurboUnlocked(unlocked);  // ターボのアンロック状態を設定

        // クールダウンUIのビジュアルにも反映
        var ui = FindFirstObjectByType<TurboCooldownUI>(FindObjectsInactive.Include);
        if (ui != null)
            ui.SetTutorialUnlocked(unlocked);   // クールダウンUIにもアンロック状態を反映
    }


    private void TryResetMomentum()
    {
        var mm = MomentumManager.Instance;
        if (mm == null) return;

        try { mm.ResetAll(); } catch { }    // 例外が発生しても無視してモメンタムをリセット
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
    // 状態確認のヘルパー

    public bool IsTutorialLevelActive()
    {
        // GameManagerのゲートを優先使用
        if (GameManager.Instance != null)
            return GameManager.Instance.IsTutorialLevelActive();

        return SceneManager.GetActiveScene().name == _firstLevelName;   // フォールバック：シーン名で判定
    }

    private bool IsPlayingScene()
    {
        return GameManager.Instance != null && GameManager.Instance.State == GameState.Playing; // ゲームが実際にプレイ中かを確認
    }

    // 統一フローからゲームプレイUIとゲージの表示・非表示を切り替えるヘルパー
    void SetGameplayUIVisible(bool visible)
    {
        UIManager.Instance?.ShowPlayerUI(visible);              // プレイヤーUI全体の表示を切り替え
        if (!visible) UIManager.Instance?.HideAllTutorials();   // 非表示時はすべてのチュートリアルも隠す

        ResolveGauge();  // シーン内のMomentumGaugeUIを取得

        if (_gauge != null)
        {
            bool shouldShowGauge = visible && ShouldGaugeBeVisibleNow();    // 表示条件を確認

            if (shouldShowGauge) _gauge.TL_ShowGauge();     // 条件を満たせばゲージを表示
            else _gauge.TL_HideGauge();                     // 条件を満たさなければゲージを非表示
        }
    }

    private bool ShouldGaugeBeVisibleNow()
    {
        // TutorialManagerの表示ポリシーに委譲
        return ShouldShowMomentumGauge();   // モメンタムゲージの表示ポリシーに委譲
    }
}
