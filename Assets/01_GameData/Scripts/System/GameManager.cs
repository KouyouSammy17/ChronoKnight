// ゲーム全体の進行（タイトル・プレイ・クリア・ゲームオーバー）を管理するシングルトン
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

// ゲームの大まかな状態を表す列挙型
public enum GameState { Title, Playing, Clear, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ───────────────────────────────────────────────────────────────────────────────
    // ACTION MAP CONSTANTS (avoid string typos)
    const string MAP_PLAYER = "Player"; // プレイヤー操作マップ名
    const string MAP_UI = "UI";         // UIナビゲーションマップ名

    [Header("Scene Names (3-scene flow)")]
    [SerializeField] private string _titleScene = "Title";       // タイトルシーン名
    [SerializeField] private string _tutorialLevel = "Level_01"; // Tutorial Stage
    [SerializeField] private string _level02 = "Level_02";       // タイムアタックステージ名

    [Header("Title UI")]
    [SerializeField] private GameObject _titleFirstSelected;         // assign your Title button root
    [SerializeField] private string _titleFirstSelectedTag = "FirstSelected"; // optional fallback
    [SerializeField] private string _titleFirstSelectedName = "Btn_Title";    // optional fallback

    [Header("Game Over")]
    [SerializeField] private float _gameOverDelay = 2f;  // ゲームオーバーUIを表示するまでの遅延時間（秒）

    [Header("Win Result Delay")]
    [SerializeField] private float _winClearDelay = 2f;   // クリア演出後にリザルトUIを表示するまでの遅延時間
    private GameClearUI_Anim _gameClearAnim;              // ゲームクリアUIのアニメーションコンポーネント

    [Header("Win Flow Refs")]
    private WinCinematicController _winCine;   // クリア演出コントローラー
    private TeleportFadeSamplePlayer _teleFade; // ゴール地点のフェードエフェクト

    [Header("Win Camera Zoom")]
    private MMFeedbacks _winZoomFeedback; // クリア時のカメラズームフィードバック

    [Header("Feel MMSceneLoading")]
    [SerializeField] private bool _useAdditive = false;                        // 加算ロードを使うかどうか
    [SerializeField] private string _feelLoadingScene = "LoadingScreen";       // 通常ロード時のローディングシーン名
    [SerializeField] private string _feelAdditiveLoadingScene = "MMAdditiveLoadingScreen"; // 加算ロード時のローディングシーン名
    [SerializeField, Range(0f, 1f)] private float _entryFade = 0.35f;         // 遷移入りフェード時間
    [SerializeField, Range(0f, 1f)] private float _exitFade = 0.35f;          // 遷移出フェード時間
    [SerializeField] private bool _interpolateProgress = true;                 // ロード進捗を補間するかどうか

    [Header("Fall Respawn")]
    [SerializeField] private bool _enableFallCheck = true;          // 落下死判定を有効にするかどうか
    [SerializeField] private float _fallKillY = -20f;               // when player.y < this → respawn
    [SerializeField] private int _fallDamage = 20;                  // 落下時に受けるダメージ量
    [SerializeField] private float _respawnFreeze = 0.1f;           // short freeze before snap (sec, realtime)
    [SerializeField] private float _respawnIFrames = 1.0f;          // optional: post-respawn grace (sec)
    [SerializeField, Tooltip("Prevents double-triggering fall respawn in quick succession.")]
    private float _fallRearmDelay = 0.35f;                           // 連続落下リスポーンを防ぐ最小間隔
    private float _nextFallAllowedUnscaled = 0f;                    // 次に落下判定を受け付けるリアル時間

    [Header("Player Spawn")]
    [SerializeField] private PlayerMotor _playerPrefab; // プレイヤーが存在しない場合にスポーンするプレハブ

    // NEW: reference to PauseMenu (assign in Inspector or auto-resolve)
    [SerializeField] private PauseMenu _pauseMenu;              // ポーズメニューへの参照
    [SerializeField] private float _pauseInputBuffer = 0.35f;  // ポーズ連続入力を防ぐバッファ時間
    [SerializeField] private bool _freezeTimeOnResults = true; // リザルト画面でtimeScale=0にするかどうか

    [Header("Tutorial Stage Flag")]
    private TutorialClearUI_Anim _tutorialClearAnim; // drag if you want, else auto-find
    [SerializeField] private bool _forceTutorialStage = false; // for testing

    [Header("Result UI First Selected")]
    [SerializeField] private GameObject _tutorialClearFirstSelected; // Restart button GO
    [SerializeField] private GameObject _gameClearFirstSelected;     // (optional) Next/Restart on normal clear
    [SerializeField] private GameObject _gameOverFirstSelected;      // (optional)

    // チュートリアルステージ判定（強制フラグまたはシーン名で確認する）
    public bool IsTutorialStage => _forceTutorialStage || SceneManager.GetActiveScene().name == _tutorialLevel;


    // HUD reveal is now owned by TutorialManager

    [Header("Debug")]
    [SerializeField] private bool _allowStartFromAnyScene = true; // エディタでどのシーンからでも開始できるようにする

    public GameState State { get; private set; } = GameState.Title; // 現在のゲーム状態

    // runtime refs（実行時に解決される参照）
    private PlayerMotor _player;                        // 現在シーン上のプレイヤー
    private PlayerInput _playerInput;                   // プレイヤーのInputコンポーネント
    private InputAction _pauseAction;                   // from Player map
    private InputAction _cancelAction;                  // from UI map
    private bool _isRespawningFromFall = false;         // 落下リスポーン処理中かどうか
    private Transform _spawnPoint;                      // Tag: Respawn
    private MomentumGaugeUI _gauge;                     // auto-fetched from the Player
    private Goal goal;                                  // set by Goal when player wins
    private Transform _checkpoint;                      // 現在有効なチェックポイントの座標
    private CancellationTokenSource _gameOverCts;       // ゲームオーバー処理のキャンセルトークン
    private bool _gameOverSequenceRunning;              // ゲームオーバーシーケンス実行中フラグ
    private bool _winRunning;                           // クリアシーケンス実行中フラグ

    private bool _pauseBlocked = false;                 // ポーズ入力バッファ中かどうか
    private bool _isPaused = false;                     // 現在ポーズ中かどうか
    private CancellationTokenSource _fallRespawnCts;    // 落下リスポーン処理のキャンセルトークン

    private GameClearTimeText _gameClearTimeText;          // クリアタイム表示UIコンポーネント
    private const string TA_BEST_TIME_KEY = "TA_BestTime_Level02";  // ベストタイム保存キー
    private const string TA_BEST_STARS_KEY = "TA_BestStars_Level02"; // ベストスター数保存キー
    public bool IsPaused => _isPaused;                              // ポーズ中かどうかを外部に公開する
    public bool IsTimeAttackStage =>
    SceneManager.GetActiveScene().name == _level02; // タイムアタックステージかどうか
    public PlayerMotor Player => _player;           // プレイヤーへの参照を外部に公開する
    public PlayerInput PlayerInput => _playerInput; // PlayerInputへの参照を外部に公開する

    void Awake()
    {
        // シングルトン初期化：シーン遷移をまたいで唯一の存在であり続ける
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded; // シーン読み込み完了時のコールバックを登録
            ResolvePauseMenu(); // try resolve at boot
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // エディタ上で任意のシーンから起動できるよう初期状態を設定する
        if (_allowStartFromAnyScene)
        {
            State = SceneManager.GetActiveScene().name == _titleScene ? GameState.Title : GameState.Playing;
            BindSpawnAndPlayer();
        }
    }

    void OnDestroy()
    {
        // 破棄時にシーン読み込みコールバックを解除して多重登録を防ぐ
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (!_enableFallCheck) return;
        if (State != GameState.Playing) return;
        if (_player == null) return;
        if (_isRespawningFromFall) return;

        // prevent spam triggers（連続落下トリガーを防ぐため時間チェックを行う）
        if (Time.unscaledTime < _nextFallAllowedUnscaled) return;

        // プレイヤーがキルラインより下に落ちたらリスポーンを開始する
        if (_player.transform.position.y <= _fallKillY)
        {
            TriggerFallRespawn();
        }
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Input callbacks (wired from WireInput)
    public void OnPauseStarted(InputAction.CallbackContext ctx)
    {
        // プレイ中でなければポーズを受け付けない
        if (State != GameState.Playing) return;
        TogglePause();
    }

    public void OnCancelStarted(InputAction.CallbackContext ctx)
    {
        // ポーズ中にキャンセルが押されたらゲームを再開する
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

        // ターボ・モメンタム・UI・チュートリアルをすべてリセットしてクリーンな状態で開始する
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
        LoadWithFeel(current, GameState.Playing); // 現在のシーンを再ロードする
        _player?.EnableInput();
        _isPaused = false;
        _checkpoint = null; // チェックポイントをリセットする

        ResolvePauseMenu();
        _pauseMenu?.HideMenu();  // <-- direct call
        UpdateCursorState();
        CancelGameOverSequence();
    }

    public void LoadNextLevel()
    {
        string current = SceneManager.GetActiveScene().name;

        // タイトルからはチュートリアルへ進む
        if (current == _titleScene)
        {
            LoadWithFeel(_tutorialLevel, GameState.Playing);
            return;
        }

        // チュートリアルからはLevel_02へ進む
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
        // プレイ中でなければクリア処理を行わない
        if (State != GameState.Playing) return;
        // 二重実行を防ぐフラグチェック
        if (_winRunning) return;

        // lock immediately so we can't re-enter
        _winRunning = true;
        State = GameState.Clear;
        // remember the goal that triggered the win
        this.goal = goal;
        // プレイヤーUIとモメンタムゲージを非表示にする
        UIManager.Instance?.ShowPlayerUI(false);
        _gauge?.TL_HideGauge();

        // Stop the timer when goal is touched
        TimeAttackManager.Instance?.StopRun();

        // Goal-specific refs: fetch EVERY time (not only when null)
        if (goal != null)
        {
            // ゴールオブジェクト配下からフェードとズームフィードバックを取得する
            _teleFade = goal.GetComponentInChildren<TeleportFadeSamplePlayer>(true);

            var tag = goal.GetComponentInChildren<WinZoomFeedbackTag>(true);
            _winZoomFeedback = tag != null ? tag.GetComponentInChildren<MMFeedbacks>(true) : null;
        }

        // Scene-wide: fetch once（シーン全体からWinCinematicControllerを一度だけ探す）
        if (_winCine == null)
        {
#if UNITY_6000_0_OR_NEWER
            _winCine = UnityEngine.Object.FindFirstObjectByType<WinCinematicController>(FindObjectsInactive.Include);
#else
        _winCine = UnityEngine.Object.FindObjectOfType<WinCinematicController>(true);
#endif
        }

        // クリアシーケンスを非同期で開始する（GameObjectが破棄されたら自動でキャンセルされる）
        WinSequenceAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid WinSequenceAsync(CancellationToken ct)
    {
        try
        {
            // ターボが動作中なら即座に停止する
            TurboModeManager.Instance?.ForceReset(clearCooldown: true);

            var player = _player;
            if (player == null) return;

            // プレイヤーをWinステートに移行して操作不能・静止・フリーズさせる
            var brain = player.GetComponent<PlayerStateMachineBrain>();
            if (brain != null)
            {
                brain.ChangeState(PlayerStateID.Win, force: true);
                brain.Motor?.DisableInput();
                brain.Motor?.StopHorizontalInstant();
                brain.Motor?.SetFrozen(true);
            }

            // camera zoom (goal-tagged)（ゴールに紐付いたカメラズームを再生する）
            if (_winZoomFeedback != null)
            {
                _winZoomFeedback.PlayFeedbacks();
            }

            // win cinematic (turn + win anim)（振り返り回転と勝利アニメーションを再生する）
            if (_winCine != null && brain != null)
            {
                Transform modelRoot = brain.Anim != null ? brain.Anim.transform : player.transform;
                await _winCine.PlayWinCinematicAsync(brain, modelRoot, ct);
            }

            // goal fade (optional)（ゴール地点のフェードアウト演出を再生する）
            if (_teleFade != null)
            {
                _teleFade.SetFadeParams(speed: 1.2f, rise: 0.25f, twist: 3.5f, spread: 0.7f);
                _teleFade.StartFadeOut();

                // フェード完了まで待機する
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_teleFade.GetTotalFadeSeconds()),
                    DelayType.Realtime,
                    cancellationToken: ct
                );
            }

            try
            {
                // リザルト表示前の待機時間
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
                    // チュートリアルクリアUIを表示し、固定の星数（3）で演出する
                    UIManager.Instance?.ShowTutorialClearUI();
                    ResolveTutorialClearUI();

                    int starsAchieved = 3;      // tutorial fixed
                    bool toggleAchieved = true; // tutorial fixed
                    _tutorialClearAnim?.Show(starsAchieved, toggleAchieved);
                    return;
                }

                // Level_02 = Time Attack clear UI（タイムアタックのクリアUIを表示する）
                UIManager.Instance?.ShowGameClearUI();

                if (IsTimeAttackStage)
                {
                    ResolveGameClearUI();
                    float elapsed = TimeAttackManager.Instance != null ? TimeAttackManager.Instance.Elapsed : 0f;

                    bool reachedGoal = this.goal != null;
                    bool clear120 = elapsed <= 120f; // 120秒以内クリアで星1つ
                    bool clear90 = elapsed <= 90f;   // 90秒以内クリアで星1つ追加

                    bool[] checks = { reachedGoal, clear120, clear90 };
                    int stars = (reachedGoal ? 1 : 0) + (clear120 ? 1 : 0) + (clear90 ? 1 : 0);

                    // compute stars/checks...
                    SaveTimeAttackResult(elapsed, stars); // ベスト記録を保存する

                    // read best AFTER saving（保存後にベストタイムを読み込む）
                    float best = PlayerPrefs.GetFloat(TA_BEST_TIME_KEY, float.MaxValue);

                    // update text（タイム表示UIを更新する）
                    _gameClearTimeText?.OnGameClearUIShown(elapsed, best);

                    // animate checks/stars（星・チェックのアニメーションを再生する）
                    _gameClearAnim?.Show(stars, checks);
                }
            });

        }
        finally
        {
            // 成功・失敗どちらでも実行中フラグを解除する
            _winRunning = false;
        }
    }


    public void GameOver()
    {
        // プレイ中でなければゲームオーバー処理を行わない
        if (State != GameState.Playing) return;
        // 二重実行を防ぐフラグチェック
        if (_gameOverSequenceRunning) return;

        _gameOverSequenceRunning = true;

        // 前回のキャンセルトークンを破棄して新しいものを作成する
        _gameOverCts?.Cancel();
        _gameOverCts?.Dispose();
        _gameOverCts = new CancellationTokenSource();

        GameOverSequenceAsync(_gameOverCts.Token).Forget();
        _gauge?.TL_HideGauge(); // モメンタムゲージを非表示にする
    }

    private void CancelGameOverSequence()
    {
        // ゲームオーバーシーケンスをキャンセルして後片付けをする
        _gameOverCts?.Cancel();
        _gameOverCts?.Dispose();
        _gameOverCts = null;
        _gameOverSequenceRunning = false;
    }

    private async UniTaskVoid GameOverSequenceAsync(CancellationToken ct)
    {
        // Make sure turbo timescale doesn't make death feel weird
        TurboModeManager.Instance?.ForceReset(clearCooldown: true);

        // Lock player（プレイヤーの操作を無効にする）
        _player?.DisableInput();

        // Stop sliding（リジッドボディの速度をゼロにして滑りを防ぐ）
        if (_player != null)
        {
            var rb = _player.GetRigidbody();
            if (rb != null) rb.linearVelocity = Vector3.zero;

        }

        try
        {
            // ゲームオーバーUIを表示するまで指定秒数待機する
            await UniTask.Delay(TimeSpan.FromSeconds(_gameOverDelay),
                DelayType.Realtime, PlayerLoopTiming.Update, ct);
        }
        catch (OperationCanceledException) { return; }

        if (ct.IsCancellationRequested || this == null) return;

        // ゲームオーバーのリザルトモードに移行してUIを表示する
        EnterResultMode(GameState.GameOver, () => UIManager.Instance?.ShowGameOverUI_Animated());
        _gameOverSequenceRunning = false;
    }

    public PlayerMotor GetPlayer() => _player; // 現在のプレイヤーインスタンスを返す

    public void RespawnPlayer(bool resetStats = true)
    {
        if (_player == null) return;

        // チェックポイントがあればそこへ、なければ初期スポーン地点へ移動する
        Transform target = _checkpoint != null ? _checkpoint : _spawnPoint;

        if (target != null)
            _player.transform.position = target.position;

        var rb = _player.GetRigidbody();
        if (rb != null) rb.linearVelocity = Vector3.zero; // 速度をリセットする

        // 2) make the new transform "real" for raycasts this frame
        Physics.SyncTransforms(); // 物理エンジンにトランスフォームの変更を即時反映させる

        // 3) use a respawn-aware reset (does not kill jump buffer, seeds coyote)
        _player.OnRespawnSnap();

        if (resetStats)
        {
            // HPや状態をリセットし、ステートマシンも初期化する
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
        // バッファ中またはプレイ中でなければポーズを切り替えない
        if (_pauseBlocked || State != GameState.Playing) return;
        PauseBufferAsync(this.GetCancellationTokenOnDestroy()).Forget();
        if (_isPaused) ResumeGame(); else PauseGame();
    }

    // ポーズ入力の連打を防ぐための短いバッファ処理
    private async UniTaskVoid PauseBufferAsync(CancellationToken ct)
    {
        _pauseBlocked = true;
        await UniTask.Delay(TimeSpan.FromSeconds(_pauseInputBuffer), DelayType.Realtime, PlayerLoopTiming.Update, ct);
        _pauseBlocked = false;
    }

    public void PauseGame()
    {
        if (_isPaused) return;

        // Switch to UI BEFORE freezing time（timeScaleを0にする前にUIマップに切り替える）
        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();
            if (_playerInput.actions.FindActionMap(MAP_UI, false) != null)
            {
                // UIマップに切り替えてUI操作を有効にする
                _playerInput.defaultActionMap = MAP_UI;
                _playerInput.SwitchCurrentActionMap(MAP_UI);
                if (IsTimeAttackStage)
                    TimeAttackManager.Instance?.PauseRun(); // タイムアタック計測を一時停止する
            }
        }

        _player?.DisableInput();   // プレイヤーの操作を無効にする
        Time.timeScale = 0f;       // 時間を停止する
        _isPaused = true;

        ResolvePauseMenu();
        _pauseMenu?.ShowMenu();  // <-- direct call
        UpdateCursorState();
    }

    public void ResumeGame()
    {
        if (!_isPaused) return;

        Time.timeScale = 1f; // 時間を再開する

        if (_playerInput != null && _playerInput.actions != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true;
            if (!_playerInput.actions.enabled) _playerInput.actions.Enable();
            if (_playerInput.actions.FindActionMap(MAP_PLAYER, false) != null)
            {
                // プレイヤー操作マップに戻す
                _playerInput.defaultActionMap = MAP_PLAYER;
                _playerInput.SwitchCurrentActionMap(MAP_PLAYER);
                if (IsTimeAttackStage)
                    TimeAttackManager.Instance?.ResumeRun(); // タイムアタック計測を再開する
            }
        }

        _player?.EnableInput(); // プレイヤーの操作を再開する
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

        // リセット中は入力を無効化し、アニメーターも一時停止する
        _player.DisableInput();
        Animator anim = _player.GetComponentInChildren<Animator>();
        float cachedSpeed = anim?.speed ?? 1f;
        if (anim) anim.speed = 0f; // アニメーターを止めてチュートリアルUI表示中に動かないようにする

        TutorialProgress.ResetAll();
        UIManager.Instance?.ShowTutorial(TutorialKey.Move); // 最初のチュートリアルから再表示する

        _ = ResetTutorialUnfreezeAsync(anim, cachedSpeed, this.GetCancellationTokenOnDestroy());
        RestartFromClearUI();
    }

    // 短い遅延後にアニメーターと入力を復元する
    private async UniTaskVoid ResetTutorialUnfreezeAsync(Animator anim, float cachedSpeed, CancellationToken ct)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(0.2), DelayType.Realtime, PlayerLoopTiming.Update, ct);
        _player?.EnableInput();
        // アニメーター速度を元の値（0なら1）に復元する
        if (anim) anim.speed = Mathf.Approximately(cachedSpeed, 0f) ? 1f : cachedSpeed;
    }

    // Called by the Restart button on Tutorial Clear / Clear UI
    public void RestartFromClearUI()
    {
        // Results often run at timeScale 0（リザルト中はtimeScale=0になっているので元に戻す）
        Time.timeScale = 1f;
        _isPaused = false;

        // Hide pause UI if it exists
        ResolvePauseMenu();
        _pauseMenu?.HideMenuInstant();

        // Stop turbo/time effects
        TurboModeManager.Instance?.ForceReset(clearCooldown: true);

        // チュートリアルステージの場合はチュートリアル全体もリセットする
        if (IsTutorialStage)
        {
            // This method already exists in your project (you call it elsewhere)
            TutorialManager.Instance?.ResetAllTutorialsAndRestart();
            // NOTE: If that method itself reloads the scene, you can early return.
            // If it doesn't reload, we continue and reload below.
        }

        // Clear UI state before reloading（シーン再ロード前にUIをすべてリセットする）
        UIManager.Instance?.ResetAllUI();

        // Reload current scene
        string current = SceneManager.GetActiveScene().name;
        LoadWithFeel(current, GameState.Playing);

        UpdateCursorState();
        CancelGameOverSequence();
    }


    // ───────────────────────────────────────────────────────────────────────────────
    // FEEL scene loading wrapper
    // 指定したシーン名がロード中シーン自体かどうかを判定する
    private bool IsLoadingScene(string sceneName)
    {
        if (_useAdditive)
            return !string.IsNullOrEmpty(_feelAdditiveLoadingScene) && sceneName == _feelAdditiveLoadingScene;
        else
            return !string.IsNullOrEmpty(_feelLoadingScene) && sceneName == _feelLoadingScene;
    }

    // DOTweenをクリアしてUIをリセットしたうえでFeelのローダーでシーン遷移する
    private void LoadWithFeel(string sceneName, GameState targetState)
    {
        DOTween.KillAll();
        DOTween.Clear();

        UIManager.Instance?.ResetAllUI();
        MomentumManager.Instance?.ResetAll();

        // 加算ロードか通常ロードかで使用するローダーを切り替える
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
        // ロード中シーン自体が読み込まれた場合はUIを非表示にして処理を抜ける
        if (IsLoadingScene(scene.name))
        {
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.ShowTitleUI(false);
            return;
        }

        // シーン名に応じてゲーム状態を設定する
        State = scene.name == _titleScene ? GameState.Title : GameState.Playing;

        BindSpawnAndPlayer();
        // 落下リスポーン関連の状態をリセットする
        _isRespawningFromFall = false;
        _fallRespawnCts?.Cancel();
        _fallRespawnCts?.Dispose();
        _fallRespawnCts = null;
        if (State == GameState.Playing)
        {
            UIManager.Instance?.ShowTitleUI(false);

            // UIカメラをUIManagerに設定する
            var uiCamObj = GameObject.FindWithTag("UICamera");
            if (uiCamObj != null)
            {
                var cam = uiCamObj.GetComponent<Camera>();
                UIManager.Instance?.SetUICamera(cam);
            }
            // Hide first to avoid flicker (BeginGameplayAfterIntroAsync also hides, but this guarantees immediate off)
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.HideAllTutorials();

            _player?.GetComponent<PlayerStats>()?.ResetStats(); // プレイヤーのステータスをリセットする

            // Ensure TutorialManager knows about scene refs
            TutorialManager.Instance?.ResolveSceneReferences();

            // kick the unified flow (no signals needed) — owned by TutorialManager
            TutorialManager.Instance?.BeginGameplayAfterIntroAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        else // Title
        {
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.ShowTitleUI(true);
            FocusTitleFirstSelectedNextFrame().Forget(); // 1フレーム後にタイトルボタンにフォーカスする
        }

        ResolvePauseMenu();
        ResolveTutorialClearUI();
        UpdateCursorState();

        // タイムアタックステージかどうかに応じてTimeAttackManagerを設定する
        bool timeAttack = SceneManager.GetActiveScene().name == _level02;
        TimeAttackManager.Instance?.Configure(timeAttack);

    }

    private void BindSpawnAndPlayer()
    {
        // "Respawn"タグのオブジェクトを初期スポーン地点として取得する
        var spawnGo = GameObject.FindGameObjectWithTag("Respawn");
        _spawnPoint = spawnGo ? spawnGo.transform : null;

#if UNITY_6000_0_OR_NEWER
        _player = UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
#else
    _player = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
#endif
        // シーンにプレイヤーがいなければプレハブからスポーンする
        if (_player == null && _playerPrefab != null && State == GameState.Playing)
        {
            Vector3 pos = _spawnPoint ? _spawnPoint.position : Vector3.zero;
            _player = Instantiate(_playerPrefab, pos, Quaternion.identity);
        }

        // PlayerInputコンポーネントを取得する（子オブジェクトも探索する）
        _playerInput = _player
            ? (_player.GetComponent<PlayerInput>() ?? _player.GetComponentInChildren<PlayerInput>())
            : null;
        ResolveGauge();

        WireInput(); // 入力アクションとコールバックを紐付ける

        // set default map on entry（シーン状態に応じてデフォルトの入力マップを設定する）
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

    // プレイヤー配下からMomentumGaugeUIを取得してキャッシュする
    private void ResolveGauge()
    {
        if (_gauge == null && _player != null)
            _gauge = _player.GetComponentInChildren<MomentumGaugeUI>(true);
    }

    // find PauseMenu singleton (inspector or auto)
    private void ResolvePauseMenu()
    {
        if (_pauseMenu != null) return; // すでに取得済みなら何もしない

#if UNITY_6000_0_OR_NEWER
        _pauseMenu = UnityEngine.Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
#else
        _pauseMenu = UnityEngine.Object.FindObjectOfType<PauseMenu>(true);
#endif
    }

    // シーン上のTutorialClearUI_Animを取得してキャッシュする
    private void ResolveTutorialClearUI()
    {
        if (_tutorialClearAnim != null) return;

#if UNITY_6000_0_OR_NEWER
        _tutorialClearAnim = UnityEngine.Object.FindFirstObjectByType<TutorialClearUI_Anim>(FindObjectsInactive.Include);
#else
    _tutorialClearAnim = UnityEngine.Object.FindObjectOfType<TutorialClearUI_Anim>(true);
#endif
    }

    // シーン上のGameClearUI_AnimとGameClearTimeTextを取得する
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

    // ポーズアクションのコールバック登録を解除する
    private void UnwireInput()
    {
        if (_pauseAction != null) { _pauseAction.started -= OnPauseStarted; _pauseAction = null; }
        if (_cancelAction != null) { _cancelAction.started -= OnCancelStarted; _cancelAction = null; }
    }

    // PlayerInputのアクションアセットからPause・Cancelアクションを探してコールバックを登録する
    private void WireInput()
    {
        if (_playerInput == null || _playerInput.actions == null) return;
        UnwireInput(); // 二重登録を防ぐために先に解除する

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
        // ゲームプレイ中はカーソルを固定・非表示、それ以外は表示・自由移動にする
        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public bool IsTutorialLevelActive()
    {
        // 現在チュートリアルシーンがアクティブかどうかを返す
        return SceneManager.GetActiveScene().name == _tutorialLevel;
    }

    private async UniTaskVoid FallRespawnAndDamageAsync()
    {
        if (_isRespawningFromFall) return;
        if (_player == null) return;

        _isRespawningFromFall = true;

        // re-arm delay (so we can't double-trigger immediately)
        _nextFallAllowedUnscaled = Time.unscaledTime + _fallRearmDelay;

        // Cancel any previous fall routine（前回の落下処理が残っている場合はキャンセルする）
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

            // Stop combat / queued stuff that can lock you（コンボをキャンセルしてロック状態を解除する）
            combat?.CancelCombo();

            // Block incoming damage during the sequence + shortly after（リスポーン中は無敵状態にする）
            recv?.SetInvulnerable(true);
            stats?.ArmNoDamageFor(_respawnIFrames + 0.1f);

            // Lock input + stop motion（操作を止め、リジッドボディを静止させる）
            player.DisableInput();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Snap to spawn (your RespawnPlayer already calls OnRespawnSnap + stops Turbo)
            RespawnPlayer(resetStats: false); // HPリセットなしでスポーン地点に瞬時移動する

            if (rb != null) rb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();

            // Tiny freeze for feel（スポーン直後の短い硬直でフィードバックを演出する）
            if (_respawnFreeze > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_respawnFreeze), DelayType.Realtime, PlayerLoopTiming.Update, ct);
                if (ct.IsCancellationRequested) return;
            }

            // Apply fall damage ONCE (skip hit react)（落下ダメージを一度だけ与える）
            if (stats != null && _fallDamage > 0)
            {
                stats.TakeHazardDamage(_fallDamage, ignoreGates: true, triggerHitReact: false);
            }

            // Force state machine back to locomotion (prevents "stuck can't jump/attack")
            // ステートマシンを接地・空中ステートに強制遷移させて操作不能を防ぐ
            if (brain != null)
            {
                var next = player.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne;
                brain.ChangeState(next, force: true);
            }

            // Restore control（プレイヤーの操作を再開する）
            player.EnableInput();

            // リスポーン後の無敵時間を設定する
            if (_respawnIFrames > 0f)
            {
                recv?.SetInvulnerableFor(_respawnIFrames).Forget();
            }
        }
        finally
        {
            // 正常終了・例外どちらでもフラグを解除する
            _isRespawningFromFall = false;
        }
    }

    public void TriggerFallRespawn()
    {
        if (!_enableFallCheck) return;
        if (_player == null) return;
        if (_isRespawningFromFall) return;
        // 連続発動を防ぐインターバルチェック
        if (Time.unscaledTime < _nextFallAllowedUnscaled) return;

        FallRespawnAndDamageAsync().Forget();
    }


    public void SetCheckpoint(Transform checkpoint)
    {
        // 現在のリスポーン地点を更新する
        _checkpoint = checkpoint;
    }

    // スポーン地点が見つからない場合に原点を仮の地点として設定する
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

    // リザルト画面に移行する共通処理（状態設定・UI表示・入力マップ切替を行う）
    private void EnterResultMode(GameState newState, System.Action showUI)
    {
        // state
        State = newState;

        // make sure we are not considered paused (and hide the pause menu instantly)
        _isPaused = false;
        ResolvePauseMenu();
        _pauseMenu?.HideMenuInstant();

        TurboModeManager.Instance?.ForceReset(clearCooldown: true);
        // lock gameplay（プレイヤーの操作を無効にする）
        _player?.DisableInput();

        // put PlayerInput on UI map so Submit/Cancel/Navigate work on result screen
        // リザルト画面でUI操作を受け付けるようにUIマップに切り替える
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
        // リザルト画面中はゲームワールドを停止する
        if (_freezeTimeOnResults) Time.timeScale = 0f;


        // clear any leftover gameplay UI (tutorials, etc.)
        UIManager.Instance?.ResetAllUI();

        // show the specific result UI（クリアまたはゲームオーバーのUIを表示する）
        showUI?.Invoke();

        // 表示するリザルト種別に応じて最初に選択するUI要素を決定する
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

        // show/unlock cursor for results（リザルト画面ではカーソルを表示する）
        UpdateCursorState();
    }

    // 1フレーム待ってからタイトルUIの最初のボタンにフォーカスする
    private async UniTaskVoid FocusTitleFirstSelectedNextFrame()
    {
        await UniTask.NextFrame(); // wait until Title UI is enabled

        var target = _titleFirstSelected;

        // optional fallbacks（Inspectorに未設定の場合はタグや名前で検索する）
        if (target == null && !string.IsNullOrEmpty(_titleFirstSelectedTag))
            target = GameObject.FindGameObjectWithTag(_titleFirstSelectedTag);
        if (target == null && !string.IsNullOrEmpty(_titleFirstSelectedName))
            target = GameObject.Find(_titleFirstSelectedName);

        // EventSystemに対象オブジェクトを選択状態としてセットする
        if (EventSystem.current != null && target != null && target.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
    }

    // UIが完全に有効になるまで複数フレーム待ってから最初の選択要素にフォーカスする
    private async UniTaskVoid FocusUIFirstSelectedAsync(GameObject target, CancellationToken ct)
    {
        if (target == null) return;

        // Let UI enable, rebuild layout, and InputSystemUIInputModule settle
        // UIが有効化されてレイアウトが確定するまで複数フレーム待機する
        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
        await UniTask.NextFrame(ct);
        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

        if (ct.IsCancellationRequested) return;
        if (EventSystem.current == null) return;
        if (!target.activeInHierarchy) return;

        // Must be a Selectable (Button). If it's a child Text, selection won't show.
        var sel = target.GetComponent<UnityEngine.UI.Selectable>();
        if (sel != null && !sel.IsInteractable()) return; // インタラクト不可ならフォーカスしない

        // 一度nullにしてからセットすることで確実に選択イベントが発火する
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
        sel?.Select();
    }

    public void NotifyGameplayBegan()
    {
        if (State != GameState.Playing) return;

        // タイムアタックステージなら計測を開始する
        if (IsTimeAttackStage)
        {
            TimeAttackManager.Instance?.StartRun();
        }
    }

    // タイムアタックの結果を保存する（スター数優先、同スターならタイムが良い方を保存する）
    private void SaveTimeAttackResult(float elapsed, int stars)
    {
        float best = PlayerPrefs.GetFloat(TA_BEST_TIME_KEY, float.MaxValue);
        int bestStars = PlayerPrefs.GetInt(TA_BEST_STARS_KEY, 0);

        // Save better stars first, then time（より多くのスターを獲得した場合は無条件で更新する）
        if (stars > bestStars)
        {
            PlayerPrefs.SetInt(TA_BEST_STARS_KEY, stars);
            PlayerPrefs.SetFloat(TA_BEST_TIME_KEY, elapsed);
        }
        else if (stars == bestStars && elapsed < best)
        {
            // 同じスター数ならタイムが短い方を保存する
            PlayerPrefs.SetFloat(TA_BEST_TIME_KEY, elapsed);
        }

        PlayerPrefs.Save(); // 変更をディスクに即時書き込む
    }
}
