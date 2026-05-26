// プレイヤーの物理演算・移動・ジャンプ・ダッシュ・壁スライドなどを統括するコアモータースクリプト
using UnityEngine;
using System;

/// <summary>
/// Core physics and movement controller for the player.
/// Handles ground/air movement, jumping, wall sliding, dashing, and momentum accumulation.
/// Integrates with PlayerStateMachineBrain for state transitions and PlayerAnimator for animation feedback.
/// Uses Rigidbody for physics and performs velocity calculations with turbo mode scaling.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMotor : MonoBehaviour
{

    /// <summary>Base movement speed (units/second)</summary>
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 6f; // 基本移動速度（単位/秒）
    /// <summary>Acceleration rate for smooth speed-up</summary>
    [SerializeField] private float _acceleration = 10f; // 加速率（移動開始時の速度上昇）
    /// <summary>Deceleration rate for smooth slow-down</summary>
    [SerializeField] private float _deceleration = 10f; // 減速率（移動停止時の速度減少）

    /// <summary>Rotation speed toward movement direction</summary>
    [Header("Rotation")]
    [SerializeField] private float _rotateSpeed = 10f; // 移動方向への回転速度
    /// <summary>Target rotation quaternion for smooth rotation</summary>
    private Quaternion _targetRotation; // スムーズ回転の目標クォータニオン

    /// <summary>Forward distance for edge detection (prevents walking off cliffs)</summary>
    [Header("Edge Detection")]
    [SerializeField] private float _edgeCheckForward = 0.5f; // 崖検出用の前方レイキャスト距離
    /// <summary>Down distance for ground raycast</summary>
    [SerializeField] private float _edgeCheckDown = 1.0f; // 崖検出用の下方レイキャスト距離
    /// <summary>Layer mask for ground detection</summary>
    [SerializeField] private LayerMask _groundLayer; // 接地判定に使用するレイヤーマスク

    [Header("Jump Settings")]
    [SerializeField] private float _jumpForce = 18f;
    [SerializeField] private float _coyoteTime = 0.15f;
    [SerializeField] private float _jumpBufferTime = 0.05f;
    [SerializeField] private float _fallMultiplier = 7f;
    [SerializeField] private float _jumpCutMultiplier = 0.8f;
    [SerializeField] private float _maxFallSpeed = 40f;

    [Header("Dash Jump")]
    [SerializeField] private float _dashJumpForce = 16f;
    [SerializeField] private float _dashJumpUpMultiplier = 1.2f;     // dash jump higher than normal
    [SerializeField] private float _dashJumpBonusUpVelocity = 5f;    // optional extra pop (VelocityChange)
    [SerializeField] private float _dashJumpAntiPopAccel = 20f; // was hardcoded

    [Header("Ground Ignore After Jump")]
    [SerializeField] private float _groundIgnoreAfterJump = 0.08f; // 80ms feels good
   

    [Header("Jump Hold Limit")]
    [SerializeField] private float _maxHoldJumpHeight = 5f;

    [Header("Wall Slide & Wall Jump")]
    [SerializeField] private float _wallCheckDistance = 1.2f;
    [SerializeField] private LayerMask _wallLayer;
    [SerializeField] private float _wallSlideSpeed = 3f;
    [SerializeField] private float _wallJumpForce = 22f;
    [SerializeField] private float _wallJumpHorizontalForce = 18f;
    [SerializeField] private float _postWallJumpLockTime = 0.2f;

    [Header("Dash Settings")]
    [SerializeField] private float _dashForce = 30f;
    [SerializeField] private float _dashDuration = 0.2f;
    [SerializeField] private float _dashCooldown = 1f;

    [Header("Air Combo Hang")]
    [SerializeField] private bool _useAirComboHang = true;

    [Header("Momentum Gain")]
    [SerializeField] private float _momentumGainRatePerSecond = 10f;

    [Header("References")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private PlayerAnimator _playerAnim;



    // inputs (fed by brain)
    private Vector2 _moveInput = Vector2.zero; // ステートマシンから渡される移動入力
    private bool _inputEnabled = true; // 入力が有効か（攻撃・ダメージ中はfalse）
    private RigidbodyConstraints _savedConstraints; // フリーズ前のRigidbody制約を保存
    private bool _frozen; // 完全フリーズ状態か

    // buffering (needed for combat buffering)
    private Vector2 _moveInputBuffer = Vector2.zero; // コンボ中の移動入力をバッファ
    private float _moveBufferTime = 0.2f; // 移動バッファの有効時間
    private float _moveBufferCounter = 0f; // 移動バッファのカウントダウン
    private bool _isHoldingMove = false; // 移動入力を押し続けているか

    private Vector2 _lastMoveInput = Vector2.right; // 最後の移動入力（向き計算に使用）

    private Vector3 _currentVelocity; // 現在の制御速度（スムーズ補間用）
    private Vector3 _dashDirection; // ダッシュの方向ベクトル

    private float _movementMomentumTimer = 0f; // 移動によるモメンタム蓄積タイマー

    private bool _isDashing = false; // ダッシュ中か
    private float _dashTimer = 0f; // ダッシュ残り時間
    private float _dashCooldownTimer = 0f; // ダッシュクールダウン残り時間

    private bool _isDashJump = false; // ダッシュジャンプ中か
    private float _dashJumpBufferTime = 0.15f; // ダッシュジャンプバッファの有効時間
    private float _dashJumpBufferCounter = 0f; // ダッシュジャンプバッファのカウントダウン

    private bool _isGrounded = false; // コヨーテタイムを含む接地フラグ
    private float _groundIgnoreTimer = 0f; // ジャンプ直後に接地を無視するタイマー

    private float _coyoteTimeCounter = 0f; // コヨーテタイムのカウントダウン（崖端での猶予ジャンプ）
    private float _jumpBufferCounter = 0f; // ジャンプ入力バッファのカウントダウン
    private bool _isJumpHeld = false; // ジャンプボタンを押し続けているか
    private float _jumpStartY = 0f; // ジャンプ開始時のY座標（最大高さ制限に使用）
    private bool _hasUsedCoyoteJump = false; // コヨーテジャンプを使用済みか
    private int _extraJumpsUsed = 0; // 使用した追加ジャンプ回数

    private bool _isTouchingWall = false; // 壁に触れているか
    private Vector3 _wallNormal = Vector3.zero; // 接触している壁の法線ベクトル
    private bool _isWallSliding = false; // 壁スライド中か
    private bool _allowWallSlide = true; // 壁スライドを許可するか

    private bool _isWallJumping = false; // 壁ジャンプの免疫時間中か
    private bool _hasWallJumped = false; // 現在の壁で壁ジャンプ済みか
    private readonly float _wallJumpDisableTime = 0.2f; // 壁ジャンプ後の入力免疫時間
    private float _wallJumpTimer = 0f; // 壁ジャンプ免疫タイマー

    private bool _isWallJumpLerping = false; // 壁ジャンプ後の速度補間中か
    private readonly float _wallJumpLerpTime = 0.15f; // 壁ジャンプ速度補間の時間
    private float _wallJumpLerpTimer = 0f; // 壁ジャンプ速度補間のタイマー
    private Vector3 _wallJumpStartVelocity = Vector3.zero; // 壁ジャンプ補間の開始速度
    private Vector3 _wallJumpTargetVelocity = Vector3.zero; // 壁ジャンプ補間の目標速度

    private float _postWallJumpTimer = 0f; // 壁ジャンプ後に入力をロックするタイマー

    private bool _airComboHang; // 空中コンボ中に重力を無効化するフラグ
    private bool _savedUseGravity; // 空中コンボハング前の重力設定を保存
    // --- Hit React Lock (Smash/Kirby style) ---
    private bool _hitReactLock = false; // ヒットリアクション中に物理制御をロックするフラグ

    // grounded (raw vs coyote)
    private bool _isGroundedRaw = false; // コヨーテタイムを含まない純粋な接地判定
    public bool IsGroundedRaw => _isGroundedRaw;



    // ターボ中はUnscaledDeltaTimeを使用してスロー影響を受けないようにする
    private float PlayerDT =>
      (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
          ? Time.unscaledDeltaTime // ターボ中：実時間ベース
          : Time.deltaTime;        // 通常時：TimeScale依存

    private float PlayerFixedDT =>
        (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
            ? Time.fixedUnscaledDeltaTime // ターボ中：実時間ベース（物理）
            : Time.fixedDeltaTime;        // 通常時：TimeScale依存（物理）
    // buffs
    public bool CanAirDash { get; private set; } = false;
    public int ExtraJumpCount { get; private set; } = 0;

    // animator-facing
    public float VerticalSpeed => _rb.linearVelocity.y;
    public bool IsGrounded => _isGrounded;
    public bool IsWallSliding => _isWallSliding;

    // state machine-facing

    public bool IsTouchingWall => _isTouchingWall;
    public bool IsDashing => _isDashing;
    public bool IsInputEnabled => _inputEnabled;

    public bool IsMoving => _inputEnabled && !_isDashing && _moveInput.sqrMagnitude > 0.01f;
    // helpers

    // “Should we be in WallSlide state right now?”
    public bool ShouldWallSlide => _allowWallSlide && _isTouchingWall && !_isGrounded && _rb.linearVelocity.y < 0f;
    private bool isTapWallJump => _isWallJumpLerping || _isWallJumping;


    // ───────────────────────────────────────────────────────────────
    // Platform carry (for super-fast moving platforms)
    // ───────────────────────────────────────────────────────────────
    private Vector3 _platformCarryVel = Vector3.zero; // world-space velocity to add (x/z)
    public void SetPlatformCarryVelocity(Vector3 vel) => _platformCarryVel = vel;
    public void ClearPlatformCarryVelocity() => _platformCarryVel = Vector3.zero;

    public float GetAnimMovementSpeedNormalized()
    {
        // Ignore platform carry so standing on moving platforms stays Idle
        Vector3 v = _rb.linearVelocity - _platformCarryVel; // 足場の速度を除いた実際の移動速度を計算
        v.y = 0f; // 垂直成分を除く
        float speed = v.magnitude;
        return Mathf.Clamp01(speed / _moveSpeed); // 最大速度で正規化して0〜1に収める
    }


    public Rigidbody GetRigidbody() => _rb;

    public Vector3 GetFacingDirection()
    {
        return new Vector3(Mathf.Sign(_lastMoveInput.x), 0, 0);
    }

    public Vector2 GetBufferedMovement() => _moveInputBuffer;

    public void ClearBufferedMovement()
    {
        _moveInputBuffer = Vector2.zero;
        _moveBufferCounter = 0f;
        // Re-sync _currentVelocity from rigidbody when buffer is cleared
        _currentVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
    }

    public void PreloadMovementBufferFromHold()
    {
        if (_isHoldingMove && _lastMoveInput.sqrMagnitude > 0.01f)
        {
            _moveInputBuffer = _lastMoveInput;
            _moveBufferCounter = float.MaxValue;
        }
    }

    // UI hooks
    public event Action<float> DashStarted; // passes cooldown seconds
    public float DashCooldownRemaining => Mathf.Max(0f, _dashCooldownTimer);
    public float DashCooldownDuration => _dashCooldown;

    public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = value; }
    public float Acceleration { get => _acceleration; set => _acceleration = value; }
    public float Deceleration { get => _deceleration; set => _deceleration = value; }

    public float RotateSpeed { get => _rotateSpeed; set => _rotateSpeed = value; }
    public float DashForce { get => _dashForce; set => _dashForce = value; }

    public float DashJumpForce { get => _dashJumpForce; set => _dashJumpForce = value; }
    public float DashJumpUpMultiplier { get => _dashJumpUpMultiplier; }
    public float DashJumpBonusUpVelocity
    {
        get => _dashJumpBonusUpVelocity;
        set => _dashJumpBonusUpVelocity = value;
    }

    public float JumpForce { get => _jumpForce; set => _jumpForce = value; }
    public float WallJumpForce { get => _wallJumpForce; set => _wallJumpForce = value; }
    public float WallJumpHorizontalForce { get => _wallJumpHorizontalForce; set => _wallJumpHorizontalForce = value; }

    public float JumpCutMultiplier { get => _jumpCutMultiplier; set => _jumpCutMultiplier = value; }
    public float MaxHoldJumpHeight { get => _maxHoldJumpHeight; set => _maxHoldJumpHeight = value; }

    public float FallMultiplier { get => _fallMultiplier; set => _fallMultiplier = value; }
    public float MaxFallSpeed { get => _maxFallSpeed; set => _maxFallSpeed = value; }
    public float WallSlideSpeed { get => _wallSlideSpeed; set => _wallSlideSpeed = value; }

    public bool IsHoldingMove => _isHoldingMove;
    public Vector2 GetLastMoveInput() => _lastMoveInput;

    public void SetMoveSpeed(float speed) => _moveSpeed = speed;
    public void SetAccelDecel(float accel, float decel) { _acceleration = accel; _deceleration = decel; }

    private void Awake()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (_playerAnim == null) _playerAnim = GetComponentInChildren<PlayerAnimator>();

        _isGroundedRaw = IsGroundedCheck();
        _isGrounded = _isGroundedRaw;
        if (_isGrounded) _coyoteTimeCounter = _coyoteTime;

        if (_lastMoveInput == Vector2.zero) _lastMoveInput = Vector2.right;

        _targetRotation = transform.rotation;
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // INPUT FEED (called by state machine)
    // ───────────────────────────────────────────────────────────────────────────────

    public void InputMove(Vector2 input)
    {
        // holding flag
        _isHoldingMove = input.sqrMagnitude > 0.01f;

        // always capture raw input into buffer (even if input locked)
        _moveInputBuffer = input;
        _moveBufferCounter = _moveBufferTime;

        // only apply to live movement when input enabled
        if (_inputEnabled)
        {
            _moveInput = input;
            if (input.sqrMagnitude > 0.01f)
                _lastMoveInput = input;
        }
    }

    public void InputJumpPressed()
    {
        if (!_inputEnabled) return;
        _jumpBufferCounter = _jumpBufferTime;
        _isJumpHeld = true;
    }

    public void InputJumpReleased()
    {
        if (!_inputEnabled) return;
        _isJumpHeld = false;
    }

    // Dash start is decided by states/brain
    public bool TryStartDash(Vector2 moveForDirection)
    {
        if (!_inputEnabled) return false; // 入力が無効なら失敗
        if (_isDashing) return false; // 既にダッシュ中なら失敗
        if (_dashCooldownTimer > 0f) return false; // クールダウン中なら失敗

        // only allow if grounded or air-dash unlocked
        if (!IsGroundedCheck() && !CanAirDash) return false; // 空中かつエアダッシュ未解放なら失敗

        const int dashCost = 20; // ダッシュに必要なスタミナコスト
        var stats = GetComponent<PlayerStats>();
        if (stats != null && !stats.SpendStamina(dashCost)) return false; // スタミナ不足なら失敗

        _isDashing = true;
        _dashTimer = _dashDuration; // ダッシュ持続時間をセット
        _dashCooldownTimer = _dashCooldown; // クールダウンタイマーをセット

        Vector2 dashInput = (moveForDirection.sqrMagnitude > 0.01f) ? moveForDirection : _lastMoveInput; // 入力がなければ最後の向きを使用
        _dashDirection = new Vector3(dashInput.x, 0, dashInput.y).normalized; // ダッシュ方向を正規化

        _playerAnim?.TriggerDash(); // ダッシュアニメーションをトリガー
        DashStarted?.Invoke(_dashCooldown); // ダッシュ開始イベントを発火（UI用）
        MomentumManager.Instance?.AddMomentum(20); // ダッシュによるモメンタム獲得

        return true;
    }

    // Combat / Damage lock
    public void DisableInput()
    {
        _inputEnabled = false;
        _moveInput = Vector2.zero;
        _jumpBufferCounter = 0f;
    }

    public void EnableInput()
    {
        _inputEnabled = true;
    }

    // Buff hooks
    public void EnableAirDash() => CanAirDash = true;
    public void DisableAirDash() => CanAirDash = false;
    public void EnableExtraJump(int count) => ExtraJumpCount = count;

    // called by respawn systems
    public void ResetPlayerState()
    {
        _coyoteTimeCounter = 0f;
        _jumpBufferCounter = 0f;
        _isJumpHeld = false;
        _jumpStartY = transform.position.y;

        _isWallJumping = false;
        _isWallJumpLerping = false;
        _wallJumpTimer = 0f;
        _postWallJumpTimer = 0f;

        _isWallSliding = false;

        _isDashing = false;
        _isDashJump = false;
        _dashTimer = 0f;
        _dashCooldownTimer = 0f;

        _isGrounded = IsGroundedCheck();
        transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        _targetRotation = transform.rotation;

        EnableInput();
    }

    public void OnRespawnSnap()
    {
        // Clear motion/lock states but DO NOT wipe the player's jump buffer:
        _isWallJumping = false;
        _isWallJumpLerping = false;
        _wallJumpTimer = 0f;
        _postWallJumpTimer = 0f;
        _isWallSliding = false;

        _isDashing = false;
        _isDashJump = false;
        _dashTimer = 0f;
        _dashCooldownTimer = 0f;

        // Make sure vertical speed is neutral
        if (_rb != null)
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        }

        // After SNAP + Physics.SyncTransforms(), trust we’re on the ground now.
        // We also *seed* coyote so a jump pressed right away will work.
        _isGrounded = IsGroundedCheck();
        if (!_isGrounded) _isGrounded = true;     // defensive: kill tiny air gaps
        _coyoteTimeCounter = _coyoteTime;
        _hasUsedCoyoteJump = false;

        // Do NOT clear _jumpBufferCounter here – allow buffered jump to fire.
        // Also don’t force _isJumpHeld = false: if player kept holding jump,
        // let your normal “short hop cut” logic handle it.
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // MOTOR TICKS (called by states)
    // ───────────────────────────────────────────────────────────────────────────────

    public void MotorUpdate(bool allowJump, bool allowMomentumGain, bool allowWallSlide)
    {
        _allowWallSlide = allowWallSlide; // 壁スライドの許可状態を更新
        // dash-jump buffer tracking
        if (_isDashing) _dashJumpBufferCounter = _dashJumpBufferTime; // ダッシュ中はバッファをリセット
        else _dashJumpBufferCounter -= PlayerDT; // ダッシュ停止後にカウントダウン

        // wall detection
        CheckWall();

        if (_postWallJumpTimer > 0f)
            _postWallJumpTimer -= PlayerDT;

        // cooldown timers
        float dt = (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
        ? Time.unscaledDeltaTime
        : Time.deltaTime;

        if (_groundIgnoreTimer > 0f)
            _groundIgnoreTimer -= PlayerDT;

        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= dt;

        // buffer decay (only while input is live)
        if (_moveBufferCounter > 0f)
        {
            if (_inputEnabled)
                _moveBufferCounter -= PlayerDT;
        }
        else
        {
            _moveInputBuffer = Vector2.zero;
        }

        // grounded + coyote + reset on land
        bool rawGrounded = IsGroundedCheck(); // レイキャストによる純粋な接地確認
        _isGroundedRaw = rawGrounded;

        if (rawGrounded)
        {
            if (!_isGrounded) // 着地した瞬間のみ実行
            {
                _coyoteTimeCounter = _coyoteTime; // コヨーテタイムをリセット
                _hasUsedCoyoteJump = false; // コヨーテジャンプフラグをリセット
                _extraJumpsUsed = 0; // 追加ジャンプ使用回数をリセット
                _hasWallJumped = false; // 壁ジャンプフラグをリセット
                _isDashJump = false; // ダッシュジャンプフラグをリセット
            }
            _isGrounded = true;
        }
        else
        {
            _coyoteTimeCounter -= PlayerDT;     // IMPORTANT: use PlayerDT コヨーテタイムをカウントダウン
            if (_coyoteTimeCounter <= 0f)
                _isGrounded = false; // コヨーテタイム切れで空中扱いに移行
        }

        if (allowJump) HandleJump();
        if (allowMomentumGain) HandleContinuousMovementMomentum();
    }

    public void MotorFixedUpdate(bool allowHorizontalMovement)
    {
        float fdt = PlayerFixedDT;

        // update target rotation FIRST (so this frame uses it)
        if (allowHorizontalMovement)
            HandleMovementRotation();

        Quaternion newRot = Quaternion.Slerp(
            _rb.rotation,
            _targetRotation,
            _rotateSpeed * fdt
        );
        _rb.MoveRotation(newRot);

        if (allowHorizontalMovement)
        {
            HandleMovement();
        }
        else
        {
            // NEW: kill horizontal drift while attacking/hurt/etc.
            var v = _rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            _rb.linearVelocity = v;

            _currentVelocity = new Vector3(0f, _currentVelocity.y, 0f);
        }

        HandleDash();
        HandleVerticalMotion();

        if (_platformCarryVel != Vector3.zero)
        {
            var v = _rb.linearVelocity;
            v.x += _platformCarryVel.x;
            v.z += _platformCarryVel.z;
            _rb.linearVelocity = v;
        }
    }


    // ───────────────────────────────────────────────────────────────────────────────
    // Core logic (mostly unchanged from your PlayerController)
    // ───────────────────────────────────────────────────────────────────────────────

    private void HandleMovement()
    {
        if (_isDashing || !_inputEnabled) return; // ダッシュ中または入力無効時はスキップ
        if (_postWallJumpTimer > 0f) return; // 壁ジャンプ後の入力ロック中はスキップ

        Vector3 moveDir = new Vector3(_moveInput.x, 0,0); // 移動方向（X軸のみ使用）
        float sign = Mathf.Sign(_moveInput.x); // 移動の左右方向を取得

        bool shouldBlock = _isTouchingWall && _moveInput.x != 0f && _isGrounded; // 崖端で壁に当たっているか確認

        if (shouldBlock)
        {
            Vector3 foot = transform.position
                         + Vector3.down * 0.1f
                         + transform.right * (_edgeCheckForward * sign);

            bool groundAhead = Physics.Raycast(foot, Vector3.down, _edgeCheckDown, _groundLayer); // 前方に地面があるか確認
            if (!groundAhead) shouldBlock = false; // 崖端でなければブロックしない
        }

        Vector3 targetVel;
        if (shouldBlock)
        {
            targetVel = new Vector3(0f, _rb.linearVelocity.y, 0f); // 崖端でブロック：水平速度をゼロに
        }
        else
        {
            targetVel = new Vector3(
                moveDir.x * _moveSpeed, // 目標速度を計算
                _rb.linearVelocity.y,
                moveDir.z * _moveSpeed
            );
        }

        float fdt = PlayerFixedDT;
        float rate = (_moveInput == Vector2.zero ? _deceleration : _acceleration); // 入力なしなら減速、入力ありなら加速
        float t = Mathf.Clamp01(rate * fdt); // 補間係数を計算

        _currentVelocity = Vector3.Lerp(_currentVelocity, targetVel, t); // 現在の速度を目標速度に向けて補間

        _rb.linearVelocity = new Vector3(
            _currentVelocity.x,
            _rb.linearVelocity.y, // Y速度は維持（重力に委ねる）
            _currentVelocity.z
        );
    }

    private void HandleDash()
    {
        if (_hitReactLock) return;
        if (!_isDashing) return;

        float dt = (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
      ? Time.fixedUnscaledDeltaTime
      : Time.fixedDeltaTime;

        _dashTimer -= dt;

        _rb.linearVelocity = _dashDirection * _dashForce
                           + new Vector3(0, _rb.linearVelocity.y, 0);

        if (_dashTimer <= 0f)
            _isDashing = false;
    }

    private void CheckWall()
    {
        Vector3 previousWallNormal = _wallNormal;
        _isTouchingWall = false;
        _wallNormal = Vector3.zero;

        Vector3 origin = transform.position;
        RaycastHit hit;

        if (Physics.Raycast(origin, transform.forward, out hit, _wallCheckDistance, _wallLayer))
        {
            _isTouchingWall = true;
            _wallNormal = hit.normal;
        }
        else if (Physics.Raycast(origin, -transform.forward, out hit, _wallCheckDistance, _wallLayer))
        {
            _isTouchingWall = true;
            _wallNormal = hit.normal;
        }

        if (_isTouchingWall && _wallNormal != previousWallNormal)
        {
            _hasWallJumped = false;
        }
    }

    private void HandleJump()
    {
        bool buffered = _jumpBufferCounter > 0f; // ジャンプ入力がバッファされているか
        bool canWallJump = _isTouchingWall && !_isGrounded && !_hasWallJumped; // 壁ジャンプが可能か
        bool canFirstJump = !_hasUsedCoyoteJump && (_isGrounded || _coyoteTimeCounter > 0f); // 通常ジャンプが可能か
        bool canExtraJump = _extraJumpsUsed < ExtraJumpCount; // 追加ジャンプが可能か

        if (_jumpBufferCounter > 0f)
            _jumpBufferCounter -= PlayerDT; // ジャンプバッファをカウントダウン

        // Wall Jump
        if (buffered && canWallJump)
        {
            Vector3 wallJumpDir = (_wallNormal * _wallJumpHorizontalForce) + (Vector3.up * _wallJumpForce); // 壁ジャンプの方向ベクトルを計算
            RotateOnWallJump(wallJumpDir); // 壁ジャンプ方向に向きを変える


            _rb.linearVelocity = Vector3.zero;
            _rb.AddForce(wallJumpDir, ForceMode.VelocityChange);
            ForceUngroundNow();

            _isJumpHeld = false;
            _jumpBufferCounter = 0f;
            _jumpStartY = transform.position.y;

            _isWallJumping = true;
            _wallJumpTimer = _wallJumpDisableTime;
            _postWallJumpTimer = _postWallJumpLockTime;
            _hasWallJumped = true;

            _playerAnim?.TriggerWallJump();
            MomentumManager.Instance?.AddMomentum(15f);
            return;
        }

        // First Jump
        if (buffered && canFirstJump)
        {
            Jump(isAirJump: false);
            _hasUsedCoyoteJump = true;
            return;
        }

        // Extra Jump
        if (buffered && canExtraJump)
        {
            Jump(isAirJump: true);
            _extraJumpsUsed++;
            return;
        }
    }

    private void HandleVerticalMotion()
    {
        float vY = _rb.linearVelocity.y;
        float heldHeight = transform.position.y - _jumpStartY;
        float fdt = PlayerFixedDT;

        ApplyTurboGravityCompensation();

        if (_hitReactLock)
        {
            _isWallSliding = false;

            // optional: still clamp max fall speed
            if (_rb.linearVelocity.y < -_maxFallSpeed)
            {
                Vector3 clamped = _rb.linearVelocity;
                clamped.y = -_maxFallSpeed;
                _rb.linearVelocity = clamped;
            }

            return; // IMPORTANT: do NOT run jump-cut, airComboHang, wall-slide, extra gravity, etc.
        }

        // wall jump lerp
        if (_isWallJumpLerping)
        {
            _wallJumpLerpTimer -= fdt;
            float t = 1f - (_wallJumpLerpTimer / _wallJumpLerpTime);
            _rb.linearVelocity = Vector3.Lerp(_wallJumpStartVelocity, _wallJumpTargetVelocity, t);
            if (_wallJumpLerpTimer <= 0f) _isWallJumpLerping = false;
            return;
        }

        // wall jump immunity
        if (_isWallJumping)
        {
            _wallJumpTimer -= fdt;
            if (_wallJumpTimer <= 0f) _isWallJumping = false;
            return;
        }

        // wall slide
        _isWallSliding = false;
        if (_allowWallSlide && vY < 0f && _isTouchingWall && !_isGrounded)
        {
            _isWallSliding = true;
            vY = Mathf.Max(vY, -_wallSlideSpeed);
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, vY, _rb.linearVelocity.z);
            return;
        }

        // hold-jump height cap
        if (!isTapWallJump && vY > 0f && _isJumpHeld && heldHeight >= _maxHoldJumpHeight)
        {
            _isJumpHeld = false;
            _jumpBufferCounter = 0f;
            _coyoteTimeCounter = 0f;

            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, vY * _jumpCutMultiplier, _rb.linearVelocity.z);
            return;
        }

        // short hop cut
        if (!_isDashJump && !isTapWallJump && vY > 0f && !_isJumpHeld)
        {
            _jumpBufferCounter = 0f;
            _coyoteTimeCounter = 0f;
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, vY * _jumpCutMultiplier, _rb.linearVelocity.z);
            return;
        }

        if (_isDashJump && vY > 0f && (_rb.position.y - _jumpStartY) > 1.2f)
        {
            float accel = _dashJumpAntiPopAccel;

            var turbo = TurboModeManager.Instance;
            if (turbo != null && turbo.IsActive)
            {
                // Cancel slow-mo so this limiter stays the same in REAL TIME
                accel *= turbo.RealTimeComp;
            }

            _rb.AddForce(Vector3.down * accel, ForceMode.Acceleration);
        }

        // clamp fall speed
        if (_rb.linearVelocity.y < -_maxFallSpeed)
        {
            Vector3 clamped = _rb.linearVelocity;
            clamped.y = -_maxFallSpeed;
            _rb.linearVelocity = clamped;
        }

        // air combo hang(freeze Y while attacking in air)
        if (_airComboHang && !_isGrounded)
        {
            _isWallSliding = false;

            var v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v;
            return;
        }

        // fast fall
        if (vY < 0f)
        {
            _rb.linearVelocity += Vector3.up * Physics.gravity.y * (_fallMultiplier - 1f) * fdt;
        }

    }

    private void Jump(bool isAirJump)
    {
        _jumpStartY = transform.position.y;
        Vector3 vel = _rb.linearVelocity;

        _isDashJump = false;

        if (_dashJumpBufferCounter > 0f)
        {
            float minDashX = _dashForce * 0.9f;

            // keep dash-jump horizontal consistent (TurboModeManager already scaled _dashForce if needed)
            if (Mathf.Abs(vel.x) < minDashX)
                vel.x = _dashDirection.x * _dashForce;

            // dash-jump vertical: DO NOT use RealTimeComp here (instant velocity)
            vel.y = _dashJumpForce * _dashJumpUpMultiplier;

            _rb.linearVelocity = vel;

            // optional extra pop (also instant)
            if (_dashJumpBonusUpVelocity > 0f)
                _rb.AddForce(Vector3.up * _dashJumpBonusUpVelocity, ForceMode.VelocityChange);

            _isDashing = false;
            _isDashJump = true;

            _playerAnim?.TriggerDashJump();
        }
        else
        {
            vel.y = _jumpForce;
            _rb.linearVelocity = vel;

            if (isAirJump) _playerAnim?.TriggerAirJump();
            else _playerAnim?.TriggerJump();
        }

        _isJumpHeld = true;
        _jumpBufferCounter = 0f;
        _coyoteTimeCounter = 0f;
        _groundIgnoreTimer = _groundIgnoreAfterJump;
        ForceUngroundNow();
    }

    private void HandleMovementRotation()
    {
        // don't let held input override wall-jump facing during lock/lerp
        if (isTapWallJump || _postWallJumpTimer > 0f) return;

        if (Mathf.Abs(_moveInput.x) > 0.01f)
        {
            float yaw = _moveInput.x > 0 ? 90f : -90f;
            _targetRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    private void RotateOnWallJump(Vector3 wallJumpDirection)
    {
        // 2.5D: face only left/right. Ignore any Z.
        float x = wallJumpDirection.x;

        // fallback if x is near zero
        if (Mathf.Abs(x) < 0.001f)
            x = _lastMoveInput.x != 0f ? _lastMoveInput.x : 1f;

        float yaw = (x >= 0f) ? 90f : -90f;
        _targetRotation = Quaternion.Euler(0f, yaw, 0f);

        // optional: snap immediately on the wall-jump frame
        _rb.MoveRotation(_targetRotation);
    }

    private bool IsGroundedCheck()
    {
        if (_groundIgnoreTimer > 0f) return false;

        return Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            1.5f,
            _groundLayer
        );
    }


    private void ForceUngroundNow()
    {
        _isGroundedRaw = false;
        _isGrounded = false;
        _coyoteTimeCounter = 0f;
    }

    private void ApplyTurboGravityCompensation()
    {
        var turbo = TurboModeManager.Instance;
        if (turbo == null || !turbo.IsActive) return; // ターボが無効なら何もしない
        if (IsGrounded) return; // 接地中は重力補正不要

        // Cancel the "slow-mo gravity" effect for THIS rigidbody only
        float s = Mathf.Clamp(turbo.SlowFactor, 0.05f, 1f); // スロー係数を取得（最小0.05）
        float extraAccel = Physics.gravity.y * (1f / s - 1f); // gravity.y is negative スロー分だけ弱くなった重力を補正する追加加速度

        _rb.AddForce(Vector3.up * extraAccel, ForceMode.Acceleration); // 上方向に補正力を加える
    }


    private void HandleContinuousMovementMomentum()
    {
        if (!_inputEnabled || _isDashing || _isWallSliding || isTapWallJump) return;

        if (_moveInput.sqrMagnitude > 0.01f)
        {
            _movementMomentumTimer += PlayerDT;
            if (_movementMomentumTimer >= 1f)
            {
                MomentumManager.Instance?.AddMomentum(_momentumGainRatePerSecond);
                _movementMomentumTimer = 0f;
            }
        }
        else
        {
            _movementMomentumTimer = 0f;
        }
    }

    public float GetCurrentMovementSpeedNormalized()
    {
        float currentSpeed = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;
        return Mathf.Clamp01(currentSpeed / _moveSpeed);
    }

    public void ApplyBufferedMovement(Vector2 bufferedInput, bool blend = false)
    {
        _moveInput = bufferedInput;
        if (bufferedInput.sqrMagnitude > 0.01f)
            _lastMoveInput = bufferedInput;

        Vector3 moveDir = new Vector3(bufferedInput.x, 0, bufferedInput.y).normalized;
        Vector3 targetVelocity = moveDir * _moveSpeed;

        // Initialize _currentVelocity from rigidbody if not already set
        if (_currentVelocity == Vector3.zero && targetVelocity != Vector3.zero)
        {
            _currentVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        }

        if (blend)
        {
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, _acceleration * PlayerDT);
            _rb.linearVelocity = new Vector3(_currentVelocity.x, _rb.linearVelocity.y, _currentVelocity.z);
        }
        else
        {
            _currentVelocity = targetVelocity;
            _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);
        }
    }

    /// <summary>Hard-freezes the player for tutorials/cutscenes (no drift, no falling).</summary>
    public void SetFrozen(bool frozen)
    {
        if (_rb == null) return;
        if (_frozen == frozen) return; // 既に同じ状態なら何もしない

        if (frozen)
        {
            _savedConstraints = _rb.constraints; // 現在の制約を保存
            _rb.constraints = RigidbodyConstraints.FreezeAll; // 全軸をフリーズ

            _rb.linearVelocity = Vector3.zero; // 速度をゼロにリセット
            _rb.angularVelocity = Vector3.zero; // 角速度をゼロにリセット
            _currentVelocity = Vector3.zero;

            _isDashing = false;
            _dashTimer = 0f;
        }
        else
        {
            _rb.constraints = _savedConstraints; // 保存していた制約を復元

            // avoid “unfreeze kick”
            _rb.linearVelocity = Vector3.zero; // 解除時の「蹴り飛ばし」を防ぐために速度をゼロに
            _rb.angularVelocity = Vector3.zero;
            _currentVelocity = Vector3.zero;
        }

        _frozen = frozen;
    }

    public void CancelDash()
    {
        _isDashing = false;
        _dashTimer = 0f;
    }

    /// <summary>Stops only horizontal drift (use if you still want gravity).</summary>
    public void StopHorizontalInstant()
    {
        if (_rb == null) return;

        var v = _rb.linearVelocity;
        v.x = 0f;
        v.z = 0f;
        _rb.linearVelocity = v;

        _currentVelocity = new Vector3(0f, _currentVelocity.y, 0f);
    }

    public void SetAirComboHang(bool on)
    {
        if (!_useAirComboHang) return;
        if (_rb == null) return;

        if (_airComboHang == on) return;
        _airComboHang = on;

        if (on)
        {
            _savedUseGravity = _rb.useGravity;
            _rb.useGravity = false;

            var v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v;
        }
        else
        {
            _rb.useGravity = _savedUseGravity;
        }
    }
    /// <summary>
    /// While true, Motor won't apply dash / jump-cut / air-hang / wall-slide logic that can destroy knockback arcs.
    /// </summary>
    public void SetHitReactLock(bool on)
    {
        _hitReactLock = on;

        if (on)
        {
            // Stop systems that overwrite velocity during hit
            CancelDash();
            SetAirComboHang(false);

            _isJumpHeld = false;
            _jumpBufferCounter = 0f;
            _dashJumpBufferCounter = 0f;

            _isWallJumping = false;
            _isWallJumpLerping = false;
            _wallJumpTimer = 0f;
            _wallJumpLerpTimer = 0f;

            _isWallSliding = false;
            _allowWallSlide = false;
        }
    }

    /// <summary>
    /// Force the motor rotation so it doesn't "rotate back" next FixedUpdate.
    /// Right=90, Left=-90.
    /// </summary>
    public void ForceFacingYaw(float yaw, bool snap = true)
    {
        _targetRotation = Quaternion.Euler(0f, yaw, 0f);

        // keep “facing direction” consistent for knockback fallback
        _lastMoveInput = (yaw >= 0f) ? Vector2.right : Vector2.left;

        if (snap && _rb != null)
            _rb.MoveRotation(_targetRotation);
    }
}
