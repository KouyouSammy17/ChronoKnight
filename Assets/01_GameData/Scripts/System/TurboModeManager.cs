// ターボモードの起動・維持・終了を管理し、時間スケールとプレイヤーパラメータを制御するシングルトン
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TurboModeManager : MonoBehaviour
{
    public static TurboModeManager Instance { get; private set; }

    [Header("Design")]
    [SerializeField, Tooltip("How slow the world gets (0.35 = 65% slower).")]
    private float _slowFactor = 0.35f;          // 世界時間のスロー倍率（小さいほど遅くなる）

    [Header("Real-time Multipliers")]
    [SerializeField, Tooltip("Move + Attack are 1.5x in REAL TIME during Turbo.")]
    private float _moveAttackRealMult = 1.5f;   // ターボ中の移動・攻撃リアルタイム倍率

    [SerializeField, Tooltip("Everything else is 1.1x in REAL TIME during Turbo.")]
    private float _otherRealMult = 1.1f;         // ターボ中のその他アクションのリアルタイム倍率

    [SerializeField, Tooltip("Extra multiplier for fall speed during Turbo (1 = normal).")]
    private float _fallTurboScale = 1f;          // ターボ中の落下速度追加倍率

    [SerializeField, Tooltip("Turbo duration (seconds, in real time).")]
    private float _duration = 10f;               // ターボモードの持続時間（リアル秒）

    [SerializeField, Tooltip("Scale applied to JumpCutMultiplier during Turbo (<1 = stronger cut).")]
    private float _jumpCutTurboScale = 0.75f;    // ターボ中のジャンプ入力カット強度の倍率

    [SerializeField, Tooltip("Cooldown after Turbo ends (real-time seconds).")]
    private float _cooldown = 6f;                // ターボ終了後のクールダウン時間（リアル秒）

    [Header("Momentum Cost")]
    [SerializeField, Tooltip("How much momentum to spend to start Turbo (0-1 of Max).")]
    private float _momentumCostPct = 0.25f;      // ターボ起動に必要なモメンタムの割合

    [Header("Tutorial Gate")]
    [SerializeField] private bool _turboUnlocked = false; // ターボモードが解放済みかどうか

    [Header("Events")]
    public UnityEvent onTurboStart; // ターボ開始時に発火するイベント
    public UnityEvent onTurboEnd;   // ターボ終了時に発火するイベント

    // runtime
    private bool _isActive;               // ターボが現在アクティブかどうか
    private bool _onCooldown;             // クールダウン中かどうか
    private float _originalFixedDelta;    // 元のfixedDeltaTimeを保存しておく
    private float _cooldownTimer;         // クールダウンの残り時間

    private PlayerMotor _player;          // ターボ適用対象のプレイヤー
    private PlayerAnimator _anim;         // アニメーション速度を制御するアニメーター

    // cached originals（ターボ前のプレイヤーパラメータを保存する）
    private float _originalMoveSpeed;
    private float _origAcc, _origDec;
    private float _originalRotateSpeed;
    private float _originalDashForce;
    private float _originalDashJumpForce;
    private float _origDashJumpBonusUpVel;
    private float _origJumpForce;
    private float _origWallJumpForce;
    private float _origWallJumpHForce;
    private float _origJumpCutMultiplier;
    private float _origMaxHoldJumpHeight;

    private float _origFallMultiplier;
    private float _origMaxFallSpeed;
    private float _origWallSlideSpeed;

    // stored comp (mainly for other systems)
    private float _comp; // 他システムが参照するターボ時の総合倍率

    // Expose multipliers for other scripts
    public float AttackComp => _moveAttackRealMult;         // CombatController uses this (real-time)
    public float MoveComp => _moveAttackRealMult;           // if you need it elsewhere (real-time)
    public float OtherAnimComp => _otherRealMult;           // CombatTurboManager uses this (real-time)
    public float TurboComp => _comp;                        // (1/slowFactor) * 1.5
    public float SlowFactor => _slowFactor;

    // Cancel slow-mo ONLY (no boost). Good for damage impulses.
    public float KnockbackComp => 1f / Mathf.Max(0.0001f, _slowFactor); // スロー補正のみ（ノックバック用）
    public float RealTimeComp => 1f / Mathf.Max(0.0001f, _slowFactor);  // timeScaleを打ち消す係数

    public bool IsActive => _isActive;           // ターボが動作中かどうか
    public bool IsOnCooldown => _onCooldown;     // クールダウン中かどうか
    public bool TurboUnlocked => _turboUnlocked; // ターボが解放済みかどうか

    private void Awake()
    {
        // シングルトン初期化と元のfixedDeltaTimeの保存
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        _originalFixedDelta = Time.fixedDeltaTime;
    }

    private void OnEnable()
    {
        // シーン読み込み時にターボをリセットするためのイベント登録
        SceneManager.sceneLoaded += OnSceneLoaded_ResetTurbo;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_ResetTurbo;
    }

    private void OnSceneLoaded_ResetTurbo(Scene s, LoadSceneMode mode)
    {
        // シーン遷移のたびにターボ状態とクールダウンをリセットする
        ForceReset(clearCooldown: true);
    }

    private void OnDestroy()
    {
        // オブジェクト破棄時にtimeScaleが残らないよう確実に元に戻す
        if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDelta;
        }
        MomentumManager.Instance?.SetGainPaused(false);
    }

    private void Update()
    {
        // クールダウン中はリアル時間でカウントダウンする
        if (_onCooldown)
        {
            _cooldownTimer -= Time.unscaledDeltaTime;
            if (_cooldownTimer <= 0f) _onCooldown = false;
        }

        // ターボ中はフレームごとにスケール済みの値を再適用する
        if (_isActive && _player != null)
        {
            ApplyTurboScaledValues();
        }
    }

    public bool TryStartTurbo(PlayerMotor player, PlayerAnimator anim)
    {
        // ターボが解放されていない場合は起動不可
        if (!_turboUnlocked) return false;
        // すでにアクティブまたはクールダウン中は起動不可
        if (_isActive || _onCooldown) return false;

        var mm = MomentumManager.Instance;
        if (mm == null) return false;

        // 必要コスト分のモメンタムが不足している場合は起動不可
        float cost = mm.MaxMomentum * _momentumCostPct;
        if (mm.CurrentMomentum < cost) return false;

        // Spend momentum and pause gain
        mm.AddMomentum(-cost);      // コスト分を消費する
        mm.SetGainPaused(true);     // ターボ中はモメンタム加算を停止する

        _player = player;
        _anim = anim;

        // IMPORTANT: Animator should ignore timeScale during Turbo
        if (_anim != null)
            _anim.SetTurboAnimMode(true); // アニメーターをUnscaledTimeモードに切り替える

        // Cache originals（ターボ前の各パラメータを保存する）
        _originalMoveSpeed = _player.MoveSpeed;

        _origAcc = _player.Acceleration;
        _origDec = _player.Deceleration;
        _originalRotateSpeed = _player.RotateSpeed;
        _originalDashForce = _player.DashForce;
        _originalDashJumpForce = _player.DashJumpForce;
        _origDashJumpBonusUpVel = _player.DashJumpBonusUpVelocity;

        _origJumpForce = _player.JumpForce;
        _origWallJumpForce = _player.WallJumpForce;
        _origWallJumpHForce = _player.WallJumpHorizontalForce;

        _origJumpCutMultiplier = _player.JumpCutMultiplier;
        _origMaxHoldJumpHeight = _player.MaxHoldJumpHeight;

        _origFallMultiplier = _player.FallMultiplier;
        _origMaxFallSpeed = _player.MaxFallSpeed;
        _origWallSlideSpeed = _player.WallSlideSpeed;

        // World slowdown（世界の時間をスローにする）
        Time.timeScale = _slowFactor;
        Time.fixedDeltaTime = _originalFixedDelta * _slowFactor;

        // Apply scaling once immediately（即座にスケール済み値を反映する）
        ApplyTurboScaledValues();

        // snap to new move speed if holding input（入力保持中は新しい速度をすぐに反映する）
        if (_player.IsHoldingMove)
            _player.ApplyBufferedMovement(_player.GetLastMoveInput());

        _isActive = true;
        onTurboStart?.Invoke();
        _player.StartCoroutine(Co_TurboTimer()); // 持続時間を計測するコルーチンを開始する
        return true;
    }

    private void ApplyTurboScaledValues()
    {
        // timeScaleの逆数で「リアルタイム相当の速さ」を算出する
        float worldComp = 1f / Mathf.Max(0.0001f, _slowFactor);

        // MAIN COMP for other systems (move/attack 1.5 in real time)
        _comp = worldComp * _moveAttackRealMult;

        // Per-second values (affected by timeScale) need worldComp to become "real-time"
        float movePerSecondComp = worldComp * _moveAttackRealMult; // 1.5x REAL TIME
        float otherPerSecondComp = worldComp * _otherRealMult;      // 1.1x REAL TIME
        float dashPerSecondComp = worldComp * _jumpCutTurboScale;    // for dashjump

        // Instant takeoff values should NOT multiply by worldComp (or you jump to the moon)
        float otherInstantComp = _otherRealMult;                  // 1.1x

        // --- Move speed (REAL TIME * 1.5)
        _player.SetMoveSpeed(_originalMoveSpeed * movePerSecondComp);

        // --- Others (REAL TIME * 1.1)
        _player.SetAccelDecel(_origAcc * otherPerSecondComp, _origDec * otherPerSecondComp);
        _player.RotateSpeed = _originalRotateSpeed * otherPerSecondComp;

        // dash is "per second" feel（ダッシュも毎秒換算で補正する）
        _player.DashForce = _originalDashForce * otherPerSecondComp;

        // jump takeoff (instant)（ジャンプは瞬間的な力なのでworldCompを掛けない）
        _player.DashJumpForce = _originalDashJumpForce *dashPerSecondComp;
        _player.DashJumpBonusUpVelocity = _origDashJumpBonusUpVel * dashPerSecondComp; // contributes to velocity, so compensate too
        _player.JumpForce = _origJumpForce * otherPerSecondComp;
        _player.WallJumpForce = _origWallJumpForce * otherPerSecondComp;
        _player.WallJumpHorizontalForce = _origWallJumpHForce * otherPerSecondComp;

        // keep your cut tuning（ジャンプカットのチューニングを維持する）
        _player.JumpCutMultiplier = _origJumpCutMultiplier * _jumpCutTurboScale;

        // falling/gravity needs real-time compensation (per-second)（落下も毎秒換算で補正する）
        float fallComp = otherPerSecondComp * _fallTurboScale;
        _player.FallMultiplier = _origFallMultiplier * fallComp;
        _player.MaxFallSpeed = _origMaxFallSpeed * fallComp;
        _player.WallSlideSpeed = _origWallSlideSpeed * fallComp;
    }

    // ターボの持続時間をリアル時間で計測し、終了したらStopTurboを呼ぶコルーチン
    private System.Collections.IEnumerator Co_TurboTimer()
    {
        float t = 0f;
        while (t < _duration)
        {
            t += Time.unscaledDeltaTime; // リアル時間で加算することでスローに影響されない
            yield return null;
        }
        StopTurbo();
    }

    public void StopTurbo()
    {
        if (!_isActive) return;

        // Restore time（時間スケールを元に戻す）
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _originalFixedDelta;

        // Restore player（保存しておいたプレイヤーパラメータをすべて復元する）
        if (_player != null)
        {
            _player.SetMoveSpeed(_originalMoveSpeed);
            _player.SetAccelDecel(_origAcc, _origDec);
            _player.RotateSpeed = _originalRotateSpeed;

            _player.DashForce = _originalDashForce;
            _player.DashJumpForce = _originalDashJumpForce;
            _player.DashJumpBonusUpVelocity = _origDashJumpBonusUpVel;

            _player.JumpForce = _origJumpForce;
            _player.WallJumpForce = _origWallJumpForce;
            _player.WallJumpHorizontalForce = _origWallJumpHForce;

            _player.JumpCutMultiplier = _origJumpCutMultiplier;
            _player.MaxHoldJumpHeight = _origMaxHoldJumpHeight;

            _player.FallMultiplier = _origFallMultiplier;
            _player.MaxFallSpeed = _origMaxFallSpeed;
            _player.WallSlideSpeed = _origWallSlideSpeed;
        }

        // Restore animator（アニメーターを通常モードに戻す）
        if (_anim != null)
        {
            _anim.SetTurboAnimMode(false);
            _anim.SetAttackSpeed(1f);
        }

        // Resume momentum gain（モメンタムの加算停止を解除する）
        MomentumManager.Instance?.SetGainPaused(false);

        _isActive = false;
        _onCooldown = true;              // クールダウン開始
        _cooldownTimer = _cooldown;      // クールダウンタイマーをセットする

        onTurboEnd?.Invoke();

        _player = null;
        _anim = null;
    }

    public void SetTurboUnlocked(bool unlocked) => _turboUnlocked = unlocked; // ターボ解放フラグを設定する

    public void ForceReset(bool clearCooldown = true)
    {
        // 強制リセット：timeScaleとプレイヤーパラメータを即座に元に戻す
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _originalFixedDelta;

        MomentumManager.Instance?.SetGainPaused(false);

        if (_player != null)
        {
            _player.SetMoveSpeed(_originalMoveSpeed);
            _player.SetAccelDecel(_origAcc, _origDec);
            _player.RotateSpeed = _originalRotateSpeed;

            _player.DashForce = _originalDashForce;
            _player.DashJumpForce = _originalDashJumpForce;
            _player.DashJumpBonusUpVelocity = _origDashJumpBonusUpVel;

            _player.JumpForce = _origJumpForce;
            _player.WallJumpForce = _origWallJumpForce;
            _player.WallJumpHorizontalForce = _origWallJumpHForce;

            _player.JumpCutMultiplier = _origJumpCutMultiplier;
            _player.MaxHoldJumpHeight = _origMaxHoldJumpHeight;

            _player.FallMultiplier = _origFallMultiplier;
            _player.MaxFallSpeed = _origMaxFallSpeed;
            _player.WallSlideSpeed = _origWallSlideSpeed;
        }

        if (_anim != null)
        {
            _anim.SetTurboAnimMode(false);
            _anim.SetAttackSpeed(1f);
        }

        _isActive = false;
        // clearCooldownがtrueならクールダウンも即座にリセットする
        if (clearCooldown)
        {
            _onCooldown = false;
            _cooldownTimer = 0f;
        }

        _player = null;
        _anim = null;
    }

    // ターボが今すぐ起動可能かどうかを判定する（解放済み・非アクティブ・非クールダウン・モメンタム充足）
    public bool CanStartTurbo()
    {
        if (!_turboUnlocked) return false;
        if (_isActive || _onCooldown) return false;

        var mm = MomentumManager.Instance;
        if (mm == null) return false;

        float cost = mm.MaxMomentum * _momentumCostPct;
        return mm.CurrentMomentum >= cost;
    }
}
