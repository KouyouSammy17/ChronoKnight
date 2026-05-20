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
    [SerializeField] private float _moveSpeed = 6f;
    /// <summary>Acceleration rate for smooth speed-up</summary>
    [SerializeField] private float _acceleration = 10f;
    /// <summary>Deceleration rate for smooth slow-down</summary>
    [SerializeField] private float _deceleration = 10f;

    /// <summary>Rotation speed toward movement direction</summary>
    [Header("Rotation")]
    [SerializeField] private float _rotateSpeed = 10f;
    /// <summary>Target rotation quaternion for smooth rotation</summary>
    private Quaternion _targetRotation;

    /// <summary>Forward distance for edge detection (prevents walking off cliffs)</summary>
    [Header("Edge Detection")]
    [SerializeField] private float _edgeCheckForward = 0.5f;
    /// <summary>Down distance for ground raycast</summary>
    [SerializeField] private float _edgeCheckDown = 1.0f;
    /// <summary>Layer mask for ground detection</summary>
    [SerializeField] private LayerMask _groundLayer;

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
    private Vector2 _moveInput = Vector2.zero;
    private bool _inputEnabled = true;
    private RigidbodyConstraints _savedConstraints;
    private bool _frozen;

    // buffering (needed for combat buffering)
    private Vector2 _moveInputBuffer = Vector2.zero;
    private float _moveBufferTime = 0.2f;
    private float _moveBufferCounter = 0f;
    private bool _isHoldingMove = false;

    private Vector2 _lastMoveInput = Vector2.right;

    private Vector3 _currentVelocity;
    private Vector3 _dashDirection;

    private float _movementMomentumTimer = 0f;

    private bool _isDashing = false;
    private float _dashTimer = 0f;
    private float _dashCooldownTimer = 0f;

    private bool _isDashJump = false;
    private float _dashJumpBufferTime = 0.15f;
    private float _dashJumpBufferCounter = 0f;

    private bool _isGrounded = false;
    private float _groundIgnoreTimer = 0f;

    private float _coyoteTimeCounter = 0f;
    private float _jumpBufferCounter = 0f;
    private bool _isJumpHeld = false;
    private float _jumpStartY = 0f;
    private bool _hasUsedCoyoteJump = false;
    private int _extraJumpsUsed = 0;

    private bool _isTouchingWall = false;
    private Vector3 _wallNormal = Vector3.zero;
    private bool _isWallSliding = false;
    private bool _allowWallSlide = true;

    private bool _isWallJumping = false;
    private bool _hasWallJumped = false;
    private readonly float _wallJumpDisableTime = 0.2f;
    private float _wallJumpTimer = 0f;

    private bool _isWallJumpLerping = false;
    private readonly float _wallJumpLerpTime = 0.15f;
    private float _wallJumpLerpTimer = 0f;
    private Vector3 _wallJumpStartVelocity = Vector3.zero;
    private Vector3 _wallJumpTargetVelocity = Vector3.zero;

    private float _postWallJumpTimer = 0f;

    private bool _airComboHang;
    private bool _savedUseGravity;
    // --- Hit React Lock (Smash/Kirby style) ---
    private bool _hitReactLock = false;

    // grounded (raw vs coyote)
    private bool _isGroundedRaw = false;
    public bool IsGroundedRaw => _isGroundedRaw;



    private float PlayerDT =>
      (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
          ? Time.unscaledDeltaTime
          : Time.deltaTime;

    private float PlayerFixedDT =>
        (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
            ? Time.fixedUnscaledDeltaTime
            : Time.fixedDeltaTime;
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
        Vector3 v = _rb.linearVelocity - _platformCarryVel;
        v.y = 0f;
        float speed = v.magnitude;
        return Mathf.Clamp01(speed / _moveSpeed);
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
        if (!_inputEnabled) return false;
        if (_isDashing) return false;
        if (_dashCooldownTimer > 0f) return false;

        // only allow if grounded or air-dash unlocked
        if (!IsGroundedCheck() && !CanAirDash) return false;

        const int dashCost = 20;
        var stats = GetComponent<PlayerStats>();
        if (stats != null && !stats.SpendStamina(dashCost)) return false;

        _isDashing = true;
        _dashTimer = _dashDuration;
        _dashCooldownTimer = _dashCooldown;

        Vector2 dashInput = (moveForDirection.sqrMagnitude > 0.01f) ? moveForDirection : _lastMoveInput;
        _dashDirection = new Vector3(dashInput.x, 0, dashInput.y).normalized;

        _playerAnim?.TriggerDash();
        DashStarted?.Invoke(_dashCooldown);
        MomentumManager.Instance?.AddMomentum(20);

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
        _allowWallSlide = allowWallSlide;
        // dash-jump buffer tracking
        if (_isDashing) _dashJumpBufferCounter = _dashJumpBufferTime;
        else _dashJumpBufferCounter -= PlayerDT;

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
        bool rawGrounded = IsGroundedCheck();
        _isGroundedRaw = rawGrounded;

        if (rawGrounded)
        {
            if (!_isGrounded)
            {
                _coyoteTimeCounter = _coyoteTime;
                _hasUsedCoyoteJump = false;
                _extraJumpsUsed = 0;
                _hasWallJumped = false;
                _isDashJump = false;
            }
            _isGrounded = true;
        }
        else
        {
            _coyoteTimeCounter -= PlayerDT;     // IMPORTANT: use PlayerDT
            if (_coyoteTimeCounter <= 0f)
                _isGrounded = false;
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
        if (_isDashing || !_inputEnabled) return;
        if (_postWallJumpTimer > 0f) return;

        Vector3 moveDir = new Vector3(_moveInput.x, 0,0);
        float sign = Mathf.Sign(_moveInput.x);

        bool shouldBlock = _isTouchingWall && _moveInput.x != 0f && _isGrounded;

        if (shouldBlock)
        {
            Vector3 foot = transform.position
                         + Vector3.down * 0.1f
                         + transform.right * (_edgeCheckForward * sign);

            bool groundAhead = Physics.Raycast(foot, Vector3.down, _edgeCheckDown, _groundLayer);
            if (!groundAhead) shouldBlock = false;
        }

        Vector3 targetVel;
        if (shouldBlock)
        {
            targetVel = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
        else
        {
            targetVel = new Vector3(
                moveDir.x * _moveSpeed,
                _rb.linearVelocity.y,
                moveDir.z * _moveSpeed
            );
        }

        float fdt = PlayerFixedDT;
        float rate = (_moveInput == Vector2.zero ? _deceleration : _acceleration);
        float t = Mathf.Clamp01(rate * fdt);

        _currentVelocity = Vector3.Lerp(_currentVelocity, targetVel, t
            );

        _rb.linearVelocity = new Vector3(
            _currentVelocity.x,
            _rb.linearVelocity.y,
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
        bool buffered = _jumpBufferCounter > 0f;
        bool canWallJump = _isTouchingWall && !_isGrounded && !_hasWallJumped;
        bool canFirstJump = !_hasUsedCoyoteJump && (_isGrounded || _coyoteTimeCounter > 0f);
        bool canExtraJump = _extraJumpsUsed < ExtraJumpCount;

        if (_jumpBufferCounter > 0f)
            _jumpBufferCounter -= PlayerDT;

        // Wall Jump
        if (buffered && canWallJump)
        {
            Vector3 wallJumpDir = (_wallNormal * _wallJumpHorizontalForce) + (Vector3.up * _wallJumpForce);
            RotateOnWallJump(wallJumpDir);


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
        if (turbo == null || !turbo.IsActive) return;
        if (IsGrounded) return;

        // Cancel the "slow-mo gravity" effect for THIS rigidbody only
        float s = Mathf.Clamp(turbo.SlowFactor, 0.05f, 1f);
        float extraAccel = Physics.gravity.y * (1f / s - 1f); // gravity.y is negative

        _rb.AddForce(Vector3.up * extraAccel, ForceMode.Acceleration);
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
        if (_frozen == frozen) return;

        if (frozen)
        {
            _savedConstraints = _rb.constraints;
            _rb.constraints = RigidbodyConstraints.FreezeAll;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _currentVelocity = Vector3.zero;

            _isDashing = false;
            _dashTimer = 0f;
        }
        else
        {
            _rb.constraints = _savedConstraints;

            // avoid “unfreeze kick”
            _rb.linearVelocity = Vector3.zero;
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
