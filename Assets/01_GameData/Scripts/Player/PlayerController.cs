using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _acceleration = 10f;
    [SerializeField] private float _deceleration = 10f;

    [Header("Rotation")]
    [SerializeField] private float _rotateSpeed = 10f;
    private Quaternion _targetRotation;

    [Header("Edge Detection")]
    [SerializeField] private float _edgeCheckForward = 0.5f;   // how far ahead at foot
    [SerializeField] private float _edgeCheckDown = 1.0f;      // how far down from that point
    [SerializeField] private LayerMask _groundLayer;           // layer mask for ground/platform

    [Header("Jump Settings")]
    [SerializeField] private float _jumpForce = 18f;
    [SerializeField] private float _coyoteTime = 0.2f;
    [SerializeField] private float _jumpBufferTime = 0.15f;
    [SerializeField] private float _fallMultiplier = 6f;
    [SerializeField] private float _jumpCutMultiplier = 0.3f;
    [SerializeField] private float _maxFallSpeed = 40f;
    [SerializeField] private float _dashJumpForce = 16f; // ← Tunable jump arc for dash

    [Header("Jump Hold Limit")]
    [SerializeField] private float _maxHoldJumpHeight = 3f; // in world units

    [Header("Wall Slide & Wall Jump")]
    [SerializeField] private float _wallCheckDistance = 0.5f;
    [SerializeField] private LayerMask _wallLayer;
    [SerializeField] private float _wallSlideSpeed = 2f;         // downward speed when sliding
    [SerializeField] private float _wallJumpForce = 12f;         // vertical force
    [SerializeField] private float _wallJumpHorizontalForce = 8f; // away-from-wall force
    [SerializeField] private float _postWallJumpLockTime = 0.2f;

    [Header("Dash Settings")]
    [SerializeField] private float _dashForce = 15f;
    [SerializeField] private float _dashDuration = 0.2f;
    [SerializeField] private float _dashCooldown = 1f;

    [Header("Momentum Gain")]
    [SerializeField] private float _momentumGainRatePerSecond = 10f;

    [Header("References")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private PlayerAnimator _playerAnim;

    // Internal state

    private Vector2 _moveInput = Vector2.zero;
    private Vector2 _moveInputBuffer = Vector2.zero;
    private bool _inputEnabled = true;
    private float _moveBufferTime = 0.2f;
    private float _moveBufferCounter = 0f;
    // Track whether the player is *currently* holding any move input:
    private bool _isHoldingMove = false;



    private Vector3 _currentVelocity;
    private Vector3 _dashDirection;
    private Vector2 _lastMoveInput = Vector2.right; // default facing right
    private float _movementMomentumTimer = 0f;

    private bool _isDashing = false;
    private float _dashTimer = 0f;
    private float _dashCooldownTimer = 0f;
    private bool _isDashJump = false;
    private float _dashJumpBufferTime = 0.15f;
    private float _dashJumpBufferCounter = 0f;

    private bool _isGrounded = false; // NEW: cache the grounded state this frame

    private float _coyoteTimeCounter = 0f;
    private float _jumpBufferCounter = 0f;
    private bool _isJumpHeld = false;
    private float _jumpStartY = 0f;
    private bool _hasUsedCoyoteJump = false;
    private int _extraJumpsUsed = 0;

    private bool _isTouchingWall = false;
    private Vector3 _wallNormal = Vector3.zero;
    private bool _isWallSliding = false;

    private bool _isWallJumping = false;
    private bool _hasWallJumped = false;
    private readonly float _wallJumpDisableTime = 0.2f;
    private float _wallJumpTimer = 0f;

    private bool _isWallJumpLerping = false;
    private readonly float _wallJumpLerpTime = 0.15f; // Duration of the lerp
    private float _wallJumpLerpTimer = 0f;
    private Vector3 _wallJumpStartVelocity = Vector3.zero;
    private Vector3 _wallJumpTargetVelocity = Vector3.zero;
    // Returns true if we’re actively coming off a wall-jump (lerp or immunity)
    private bool isTapWallJump => _isWallJumpLerping || _isWallJumping;
    private float _postWallJumpTimer = 0f;

    // ───────────────────────────────────────────────────────────────────────────────
    // Public properties (for Animator)
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Vertical velocity (y-axis), using linearVelocity. 
    /// Animator uses this to detect when to switch into Fall.
    /// </summary>
    public float VerticalSpeed => _rb.linearVelocity.y;

    /// <summary>
    /// True if a Raycast just below the character hits ground.
    /// Animator uses this to decide when to land (Fall → Locomotion).
    /// </summary>
    public bool IsGrounded => _isGrounded;


    /// <summary>
    /// For Animator to detect wall hanging
    /// </summary>
    public bool IsWallSliding => _isWallSliding;

    public bool CanAirDash { get; private set; } = false;
    public int ExtraJumpCount { get; private set; } = 0;  // 0 = no extra jumps

    public bool IsMoving => _inputEnabled && !_isDashing && _moveInput.sqrMagnitude > 0.01f;

    public Rigidbody GetRigidbody() => _rb;
    public Vector3 GetFacingDirection()
    {
        return new Vector3(Mathf.Sign(_lastMoveInput.x), 0, 0);
    }
    /// <summary>
    /// The movement vector last captured by OnMove (lives for _moveBufferTime).
    /// </summary>
    public Vector2 GetBufferedMovement() => _moveInputBuffer;

    /// <summary>
    /// Expose the movement speed for things like Turbo Mode.
    /// </summary>
    public float MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = value;
    }

    // -----------------------------------------------------------------------------
    // Additional public accessors for Turbo Mode
    // These expose internal movement parameters so TurboModeManager can scale them.
    /// <summary>
    /// Speed at which the character rotates toward the target yaw. Exposed for Turbo.
    /// </summary>
    public float RotateSpeed
    {
        get => _rotateSpeed;
        set => _rotateSpeed = value;
    }

    /// <summary>
    /// Dash force applied to the player. Exposed for Turbo.
    /// </summary>
    public float DashForce
    {
        get => _dashForce;
        set => _dashForce = value;
    }

    /// <summary>
    /// Vertical jump impulse. Exposed for Turbo.
    /// </summary>
    public float JumpForce
    {
        get => _jumpForce;
        set => _jumpForce = value;
    }

    /// <summary>
    /// Vertical component of a wall jump. Exposed for Turbo.
    /// </summary>
    public float WallJumpForce
    {
        get => _wallJumpForce;
        set => _wallJumpForce = value;
    }

    /// <summary>
    /// Horizontal component of a wall jump. Exposed for Turbo.
    /// </summary>
    public float WallJumpHorizontalForce
    {
        get => _wallJumpHorizontalForce;
        set => _wallJumpHorizontalForce = value;
    }

    /// <summary>
    /// Fall multiplier used to accelerate falling. Exposed so Turbo Mode can
    /// adjust it during slow‑motion to maintain consistent fall speeds.
    /// </summary>
    public float FallMultiplier
    {
        get => _fallMultiplier;
        set => _fallMultiplier = value;
    }

    /// <summary>
    /// Maximum fall speed (terminal velocity). Exposed in case Turbo Mode
    /// needs to adjust it. Currently unchanged during Turbo.
    /// </summary>
    public float MaxFallSpeed
    {
        get => _maxFallSpeed;
        set => _maxFallSpeed = value;
    }

    /// <summary>
    /// Downward slide speed while wall sliding. Exposed so Turbo Mode can
    /// adjust it if necessary. Currently unchanged during Turbo.
    /// </summary>
    public float WallSlideSpeed
    {
        get => _wallSlideSpeed;
        set => _wallSlideSpeed = value;
    }

    /// <summary>
    /// Clears the movement buffer and its timer.
    /// </summary>
    public void ClearBufferedMovement()
    {
        _moveInputBuffer = Vector2.zero;
        _moveBufferCounter = 0f;
    }

    /// <summary>
    /// If you were holding at the start of the attack, prefill the buffer
    /// so it never times out during the animation.
    /// </summary>
    public void PreloadMovementBufferFromHold()
    {
        if (_isHoldingMove && _lastMoveInput.sqrMagnitude > 0.01f)
        {
            _moveInputBuffer = _lastMoveInput;
            // effectively infinite so it won't decay while input is locked
            _moveBufferCounter = float.MaxValue;
        }
    }
    public bool IsHoldingMove => _isHoldingMove;
    public Vector2 GetLastMoveInput() => _lastMoveInput;

    /// <summary>
    /// Returns a delta time for vertical physics calculations.  When Turbo Mode is
    /// active the global timeScale is lowered and <see cref="Time.fixedDeltaTime"/>
    /// is scaled accordingly.  Using <see cref="Time.fixedDeltaTime"/> here
    /// ensures that extra gravity and other vertical forces are applied
    /// consistently per physics step rather than using <see cref="Time.unscaledDeltaTime"/>,
    /// which can cause gravity to feel overly slow or fast when the global time
    /// scale is changed.  When Turbo Mode is inactive this still returns
    /// <see cref="Time.fixedDeltaTime"/>.  Note: timers (dash, jump buffers, etc.)
    /// in <see cref="Update()"/> still use real‑time delta so they count down in
    /// real time during Turbo.
    /// </summary>
    private float TurboAwareDeltaTime => Time.fixedDeltaTime;

    /// <summary>
    /// Maximum height (in world units) allowed when holding jump.  When Turbo Mode
    /// boosts jump force, this value is scaled so that the player can still
    /// achieve the intended jump height relative to the boosted force.  When
    /// Turbo ends, this value is restored to its original setting.
    /// </summary>
    public float MaxHoldJumpHeight
    {
        get => _maxHoldJumpHeight;
        set => _maxHoldJumpHeight = value;
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Public Interface for Locking/Unlocking Input (new)
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Disable all player input until EnableInput() is called.
    /// </summary>
    public void DisableInput()
    {
        _inputEnabled = false;
        _moveInput = Vector2.zero;    // stop any current movement
        _jumpBufferCounter = 0f;         // clear any buffered jump
    }

    /// <summary>
    /// Re-enable player input.
    /// </summary>
    public void EnableInput()
    {
        _inputEnabled = true;
    }
    // --- Turbo helpers for accel/decel ---
    public float Acceleration => _acceleration;
    public float Deceleration => _deceleration;

    /// <summary>Multiply current accel/decel (used during Turbo).</summary>
    public void ScaleAccelDecel(float mult)
    {
        if (mult <= 0f) return;
        _acceleration *= mult;
        _deceleration *= mult;
    }

    /// <summary>Restore accel/decel to exact values.</summary>
    public void SetAccelDecel(float acc, float dec)
    {
        _acceleration = acc;
        _deceleration = dec;
    }


    // ───────────────────────────────────────────────────────────────────────────────
    // Input Callbacks (New Input System)
    // ───────────────────────────────────────────────────────────────────────────────

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();

        // update our “currently holding?” flag:
        if (context.performed)
            _isHoldingMove = input.sqrMagnitude > 0.01f;
        else if (context.canceled)
            _isHoldingMove = false;

        // always capture *raw* input into the buffer
        _moveInputBuffer = input;
        _moveBufferCounter = _moveBufferTime;

        // only feed _moveInput into actual movement when inputEnabled
        if (_inputEnabled)
        {
            _moveInput = input;
            if (input.sqrMagnitude > 0.01f)
                _lastMoveInput = input;
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!_inputEnabled) return;   // skip if input is locked

        if (context.started)
        {
            _jumpBufferCounter = _jumpBufferTime;
            _isJumpHeld = true;
        }
        else if (context.canceled)
        {
            _isJumpHeld = false;
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!_inputEnabled) return;
        if (context.started && !_isDashing && _dashCooldownTimer <= 0f)
        {
            // only allow if grounded or air‐dash unlocked
            if (!IsGroundedCheck() && !CanAirDash)
                return;
            const int dashCost = 20;
            if (!GetComponent<PlayerStats>().SpendStamina(dashCost))
                return;

            _isDashing = true;
            _dashTimer = _dashDuration;
            _dashCooldownTimer = _dashCooldown;

            // Use last non-zero input as direction
            Vector2 dashInput = (_moveInput.sqrMagnitude > 0.01f) ? _moveInput : _lastMoveInput;
            _dashDirection = new Vector3(dashInput.x, 0, dashInput.y).normalized;

            _playerAnim.TriggerDash();
            MomentumManager.Instance.AddMomentum(20);
        }
    }

    public void OnTurbo(InputAction.CallbackContext context)
    {
        // Handled by TurboModeManager
        if (!context.started) return;
        if (!enabled) return;

        var turbo = TurboModeManager.Instance;
        if (turbo == null) return;

        // Pass references so Turbo can scale the right things
        var anim = _playerAnim; // already serialized on PlayerController
        turbo.TryStartTurbo(this, anim);
    }


    // ───────────────────────────────────────────────────────────────────────────────
    // Main Update Loop
    // ───────────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        _isGrounded = IsGroundedCheck();
        if (_lastMoveInput == Vector2.zero)
            _lastMoveInput = Vector2.right;

        // initialize to whatever your starting facing is
        _targetRotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        // 1) Smoothly rotate the rigidbody toward your target
        Quaternion newRot = Quaternion.Slerp(
            _rb.rotation,
            _targetRotation,
            _rotateSpeed * Time.fixedDeltaTime
        );
        _rb.MoveRotation(newRot);

        HandleMovementRotation();
        HandleMovement();
        HandleDash();
        HandleVerticalMotion(); // especially if you apply gravity/velocity
    }
    private void Update()
    {
        // Compute a delta time that ignores global slow-mo when Turbo Mode is active.
        // This ensures timers (dash, jump buffers, coyote time, etc.) count down in real time
        // even when Time.timeScale is reduced. When Turbo is inactive we use Time.deltaTime.
        float dt = (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        if (_isDashing)
        {
            _dashJumpBufferCounter = _dashJumpBufferTime;
        }
        else
        {
            _dashJumpBufferCounter -= dt;
        }

        // 1) Wall detection
        CheckWall();

        // count down the post-wall-jump lock
        if (_postWallJumpTimer > 0f)
            _postWallJumpTimer -= dt;

        // 2) Dash & movement input
        HandleDashTimers(dt);

        // only tick the buffer down when input is actually “live” (i.e. not mid-attack)
        if (_moveBufferCounter > 0f)
        {
            if (_inputEnabled)
                _moveBufferCounter -= dt;
        }
        else
        {
            _moveInputBuffer = Vector2.zero;
        }

        bool wasGroundedThisFrame = IsGroundedCheck();

        if (wasGroundedThisFrame)
        {
            if (!_isGrounded)  // just landed this frame
            {
                _coyoteTimeCounter = _coyoteTime;
                _hasUsedCoyoteJump = false;
                _extraJumpsUsed = 0;
                _hasWallJumped = false;
                _isDashJump = false; // ← reset here
            }
            _isGrounded = true;
        }
        else
        {
            _coyoteTimeCounter -= dt;
            if (_coyoteTimeCounter <= 0f)
                _isGrounded = false;
        }

        // 3) Jump input & execution (ground, double, wall)
        HandleJump();
        HandleContinuousMovementMomentum(dt);
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Core Handlers
    // ───────────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        // If we hit a DeathZone, respawn immediately:
        if (other.CompareTag("DeathZone"))
        {
            GameManager.Instance.GameOver();
        }
    }

    /// <summary>
    /// Called by GameManager.RespawnPlayer() to clear all motion flags & timers.
    /// </summary>
    public void ResetPlayerState()
    {
        // Reset all jump/dash/wall‐jump state
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

        //Recheck grounded state here
        _isGrounded = IsGroundedCheck();

        // Optionally, reset the facing direction:
        transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        // Finally, re‐enable any input flags if you’ve disabled them on death
        // (e.g. if you had a “lock input” for death animation).
    }

    private void HandleMovement()
    {
        if (_isDashing || !_inputEnabled)
        {
            return;
        }

        // still also block when we just wall-jumped
        if (_postWallJumpTimer > 0f)
            return;

        // 1) Build desired horizontal move
        Vector3 moveDir = new Vector3(_moveInput.x, 0, _moveInput.y);
        float sign = Mathf.Sign(_moveInput.x);

        // 2) Check if we should block movement because of a wall
        bool shouldBlock = _isTouchingWall && _moveInput.x != 0f && _isGrounded;

        // 3) Edge detection: if “shouldBlock” but there’s no ground ahead, cancel block
        if (shouldBlock)
        {
            Vector3 foot = transform.position
                         + Vector3.down * 0.1f
                         + transform.right * (_edgeCheckForward * sign);

            bool groundAhead = Physics.Raycast(
                foot,
                Vector3.down,
                _edgeCheckDown,
                _groundLayer
            );

            if (!groundAhead)
                shouldBlock = false;
        }

        // 4) Compute target velocity
        Vector3 targetVel;
        if (shouldBlock)
        {
            // zero horizontal when truly up against wall+ground
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

        // 5) Smooth acceleration / deceleration
        _currentVelocity = Vector3.Lerp(
            _currentVelocity,
            targetVel,
            (_moveInput == Vector2.zero ? _deceleration : _acceleration)
            * Time.deltaTime
        );

        _rb.linearVelocity = new Vector3(
            _currentVelocity.x,
            _rb.linearVelocity.y,
            _currentVelocity.z
        );
    }

    private void HandleDashTimers(float dt)
    {
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= dt;
    }

    private void HandleDash()
    {
        if (!_isDashing) return;
        // Use real-time delta for dash duration when Turbo Mode is active so dash length feels consistent
        float dt = (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

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

        // Allow wall jump again if we touched a different wall
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

        // Wall Jump
        if (buffered && canWallJump)
        {
            Vector3 wallJumpDir = (_wallNormal * _wallJumpHorizontalForce) + (Vector3.up * _wallJumpForce);
            RotateOnWallJump(wallJumpDir);

            _rb.linearVelocity = Vector3.zero;
            _rb.AddForce(wallJumpDir, ForceMode.VelocityChange);

            _isJumpHeld = false;
            _jumpBufferCounter = 0f;
            _jumpStartY = transform.position.y;

            _isWallJumping = true;
            _wallJumpTimer = _wallJumpDisableTime;
            _postWallJumpTimer = _postWallJumpLockTime;
            _hasWallJumped = true;

            _playerAnim.TriggerWallJump();
            MomentumManager.Instance.AddMomentum(15f);
            return;
        }

        // First Jump (ground or coyote)
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

        // 1. Wall Jump Lerp (controlled arc)
        // Compute a delta time appropriate for vertical motion.  When Turbo Mode is
        // active the global timeScale slows down physics and reduces Time.deltaTime
        // in FixedUpdate.  Use TurboAwareDeltaTime to ignore the reduced timeScale
        // during Turbo so gravity and timers run at their original pace.  This
        // prevents falling and jump timers from feeling sluggish in slow‑motion.
        float dt = TurboAwareDeltaTime;

        if (_isWallJumpLerping)
        {
            _wallJumpLerpTimer -= dt;
            float t = 1f - (_wallJumpLerpTimer / _wallJumpLerpTime);
            _rb.linearVelocity = Vector3.Lerp(_wallJumpStartVelocity, _wallJumpTargetVelocity, t);
            if (_wallJumpLerpTimer <= 0f) _isWallJumpLerping = false;
            return;
        }

        // 2. Wall Jump Immunity (skip gravity/slide)
        if (_isWallJumping)
        {
            _wallJumpTimer -= dt;
            if (_wallJumpTimer <= 0f) _isWallJumping = false;
            return;
        }

        // 3. Wall Slide
        _isWallSliding = false;
        if (vY < 0f && _isTouchingWall && !_isGrounded)
        {
            _isWallSliding = true;
            vY = Mathf.Max(vY, -_wallSlideSpeed);
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, vY, _rb.linearVelocity.z);
            return;
        }

        // 4. Hold-Jump Height Cap
        if (!isTapWallJump && vY > 0f && _isJumpHeld && heldHeight >= _maxHoldJumpHeight)
        {
            _isJumpHeld = false;
            _jumpBufferCounter = 0f;
            _coyoteTimeCounter = 0f;

            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, vY * _jumpCutMultiplier, _rb.linearVelocity.z);
            return;
        }

        // 5. Short Hop Cut
        if (!_isDashJump && !isTapWallJump && vY > 0f && !_isJumpHeld)
        {
            _jumpBufferCounter = 0f;
            _coyoteTimeCounter = 0f;
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, vY * _jumpCutMultiplier, _rb.linearVelocity.z);
            return;
        }

        // 6. Fast Fall
        if (vY < 0f)
        {
            // Use unscaled delta for gravity when Turbo is active so falling speed feels normal
            _rb.linearVelocity += Vector3.up * Physics.gravity.y * (_fallMultiplier - 1f) * dt;
        }

        if (_isDashJump && vY > 0f && (_rb.position.y - _jumpStartY) > 1.2f)
        {
            _rb.AddForce(Vector3.down * 20f, ForceMode.Acceleration); // Pull down early
        }

        // 7. Clamp Max Fall Speed
        if (_rb.linearVelocity.y < -_maxFallSpeed)
        {
            Vector3 clamped = _rb.linearVelocity;
            clamped.y = -_maxFallSpeed;
            _rb.linearVelocity = clamped;
        }
    }

    private void Jump(bool isAirJump)
    {
        _jumpStartY = transform.position.y;
        Vector3 vel = _rb.linearVelocity;

        _isDashJump = false; // reset each time

        if (_dashJumpBufferCounter > 0f)
        {
            float minDashX = _dashForce * 0.9f;
            if (Mathf.Abs(vel.x) < minDashX)
                vel.x = _dashDirection.x * _dashForce;

            vel.y = _dashJumpForce * 1.2f;  // Stronger burst
            _rb.linearVelocity = vel;

            // Add upward kick for faster rise (snappier)
            _rb.AddForce(Vector3.up * 5f, ForceMode.VelocityChange);

            _isDashing = false;
            _isDashJump = true; // mark this as a dash jump

            _playerAnim.TriggerDashJump(); // ← call only this
        }
        else
        {
            vel.y = _jumpForce;
            _rb.linearVelocity = vel;

            // Trigger correct non-dash animation
            if (isAirJump)
                _playerAnim.TriggerAirJump();
            else
                _playerAnim.TriggerJump();
        }

        _isJumpHeld = true;
        _jumpBufferCounter = 0f;
        _coyoteTimeCounter = 0f;
    }


    private void HandleMovementRotation()
    {
        if (Mathf.Abs(_moveInput.x) > 0.01f)
        {
            // Face left or right along world X
            float yaw = _moveInput.x > 0 ? 90f : -90f;
            _targetRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
    private void RotateOnWallJump(Vector3 wallJumpDirection)
    {
        // Use the full XZ direction you’re leaping in:
        Vector3 dir = new Vector3(wallJumpDirection.x, 0f, wallJumpDirection.z)
                          .normalized;
        if (dir.sqrMagnitude < 0.001f)
            return;

        // Compute a “LookRotation” so you face exactly away from the wall:
        _targetRotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    private bool IsGroundedCheck()
    {
        return Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            1.5f,
            _groundLayer
        );
    }

    private void HandleContinuousMovementMomentum(float dt)
    {
        // Ignore during wall slide, wall jump, dash, or input lock
        if (!_inputEnabled || _isDashing || _isWallSliding || isTapWallJump) return;

        // Only count if there’s actual directional input
        if (_moveInput.sqrMagnitude > 0.01f)
        {
            _movementMomentumTimer += dt;

            // Gain momentum every 1 second of continuous movement
            if (_movementMomentumTimer >= 1f)
            {
                MomentumManager.Instance.AddMomentum(_momentumGainRatePerSecond);
                _movementMomentumTimer = 0f;  // reset
            }
        }
        else
        {
            _movementMomentumTimer = 0f; // reset if stopped
        }
    }

    public float GetCurrentMovementSpeedNormalized()
    {
        // current horizontal velocity magnitude
        float currentSpeed = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;

        // normalize based on your defined move speed
        return Mathf.Clamp01(currentSpeed / _moveSpeed);
    }
    public void SetMoveSpeed(float newSpeed)
    {
        _moveSpeed = newSpeed;
    }

    public void ApplyBufferedMovement(Vector2 bufferedInput, bool blend = false)
    {
        _moveInput = bufferedInput;
        if (bufferedInput.sqrMagnitude > 0.01f)
            _lastMoveInput = bufferedInput;

        Vector3 moveDir = new Vector3(bufferedInput.x, 0, bufferedInput.y).normalized;
        Vector3 targetVelocity = moveDir * _moveSpeed;

        if (blend)
        {
            // Optional: Blend into movement instead of snapping.
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, _acceleration * Time.deltaTime);
            _rb.linearVelocity = new Vector3(
                _currentVelocity.x,
                _rb.linearVelocity.y,
                _currentVelocity.z
            );
        }
        else
        {
            // Instantly set horizontal velocity for immediate movement resume
            _rb.linearVelocity = new Vector3(
                targetVelocity.x,
                _rb.linearVelocity.y,
                targetVelocity.z
            );
            _currentVelocity = targetVelocity;
        }
    }


    public void EnableAirDash() => CanAirDash = true;
    public void DisableAirDash() => CanAirDash = false;
    public void EnableExtraJump(int count) => ExtraJumpCount = count;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // Ground check line
        Gizmos.DrawLine(
            transform.position + Vector3.up * 0.1f,
            transform.position + Vector3.up * 0.1f + Vector3.down * 1.5f
        );

        Gizmos.color = Color.green;
        // Wall check right
        Gizmos.DrawLine(
            transform.position,
            transform.position + transform.forward * _wallCheckDistance
        );
        // Wall check left
        Gizmos.DrawLine(
            transform.position,
            transform.position - transform.forward * _wallCheckDistance
        );
    }
}
