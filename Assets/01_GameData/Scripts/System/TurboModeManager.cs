using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
// Include CombatController for setting attack speed buff during Turbo Mode
// This script assumes the CombatController is on the same GameObject as the PlayerController or PlayerAnimator.

public class TurboModeManager : MonoBehaviour
{
    public static TurboModeManager Instance { get; private set; }

    [Header("Design")]
    [SerializeField, Tooltip("How slow the world gets (0.35 = 65% slower).")]
    private float _slowFactor = 0.35f;

    [SerializeField, Tooltip("How much faster the player feels (stacked on top of slow).")]
    private float _playerSpeedMult = 1.5f;

    [SerializeField, Tooltip("Extra multiplier for fall speed during Turbo (1 = normal, <1 = lighter, >1 = heavier)")]
    private float _fallTurboScale = 0.8f; // try values between 0.7–0.9

    [SerializeField, Tooltip("Turbo duration (seconds, in real time).")]
    private float _duration = 10f;

    [SerializeField, Tooltip("Scale applied to JumpCutMultiplier during Turbo (<1 = stronger cut).")]
    private float _jumpCutTurboScale = 0.75f; // try 0.7–0.9

    [SerializeField, Tooltip("Cooldown after Turbo ends (real-time seconds).")]
    private float _cooldown = 6f;

    [Header("Momentum Cost")]
    [SerializeField, Tooltip("How much momentum to spend to start Turbo (0-1 of Max).")]
    private float _momentumCostPct = 0.25f;

    [Header("Events")]
    public UnityEvent onTurboStart;
    public UnityEvent onTurboEnd;

    // runtime
    private bool _isActive;
    private bool _onCooldown;
    private float _originalFixedDelta;
    private float _cooldownTimer;

    private PlayerController _player;
    private PlayerAnimator _anim;
    private float _originalMoveSpeed;
    private float _originalAnimSpeed;
    private float _origAcc, _origDec;

    // Turbo scaling caches for additional movement parameters
    private float _originalRotateSpeed;
    private float _originalDashForce;
    private float _origJumpForce;
    private float _origWallJumpForce;
    private float _origWallJumpHForce;
    private float _origJumpCutMultiplier;
    // Cache maximum hold jump height so we can scale it during Turbo.
    private float _origMaxHoldJumpHeight;

    // Cache vertical fall parameters so we can compensate for global slow-mo.
    private float _origFallMultiplier;
    private float _origMaxFallSpeed;
    private float _origWallSlideSpeed;


    // Store the computed compensation factor so it can be reused during Turbo
    private float _comp;

    /// <summary>
    /// The current Turbo compensation factor (1/slowFactor * playerSpeedMult).
    /// Other systems can use this to scale their own values when Turbo is active.
    /// </summary>
    public float TurboComp => _comp;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
        _originalFixedDelta = Time.fixedDeltaTime;
    }

    private void Update()
    {
        if (_onCooldown)
        {
            _cooldownTimer -= Time.unscaledDeltaTime;  // real-time cooldown
            if (_cooldownTimer <= 0f) _onCooldown = false;
        }

        // If Turbo Mode is active, continually reapply the compensated movement values.
        // This prevents other systems (e.g., MomentumBuffsManager) from overriding the
        // player's speed, acceleration, rotation, dash, and jump parameters while Turbo
        // is active.  Vertical fall parameters are no longer scaled here; vertical
        // motion uses unscaled delta time in PlayerController to maintain consistent
        // falling speed during Turbo.
        if (_isActive && _player != null)
        {
            // Reapply horizontal and rotational movement compensation.
            _player.SetMoveSpeed(_originalMoveSpeed * _comp);
            _player.SetAccelDecel(_origAcc * _comp, _origDec * _comp);
            _player.RotateSpeed = _originalRotateSpeed * _comp;
            _player.DashForce = _originalDashForce * _comp;

            // Compensate vertical motion separately for jump impulses and falling.
            // We use the full compensation factor (_comp) for jump forces so that
            // jump takeoff speed matches the boosted horizontal feel.  For falling
            // (extra gravity and terminal velocity), we use an additional boost
            // factor (playerSpeedMult) to make descents feel snappier.  This gives
            // jumps and falls distinct tuning while preserving the slowed world.

            // Jump forces use the full compensation factor (1/slowFactor * playerSpeedMult)
            float verticalCompJump = _comp;
            _player.JumpForce = _origJumpForce * verticalCompJump;
            _player.WallJumpForce = _origWallJumpForce * verticalCompJump;
            // The horizontal component of a wall jump still uses the horizontal compensation
            _player.WallJumpHorizontalForce = _origWallJumpHForce * _comp;

            // Limit the hold jump height by scaling only with playerSpeedMult.  This prevents
            // jumps from becoming excessively tall while still making them feel boosted.
            _player.MaxHoldJumpHeight = _origMaxHoldJumpHeight * _playerSpeedMult;
            _player.JumpCutMultiplier = _origJumpCutMultiplier * _jumpCutTurboScale;

            // Falling uses an extra boost: multiply the full compensation by the playerSpeedMult.
            // This yields a net multiplier of (playerSpeedMult^2 / slowFactor) so descents
            // accelerate more aggressively than takeoffs.  Without this, falling can still
            // feel floaty in Turbo mode.
            float verticalCompFall = _comp * _fallTurboScale;
            // Scale the entire fallMultiplier so that both base gravity and extra gravity
            // contributions are accelerated uniformly.  Terminal velocity and wall slide
            // speeds also use this stronger multiplier.
            _player.FallMultiplier = _origFallMultiplier * verticalCompFall;
            _player.MaxFallSpeed = _origMaxFallSpeed * verticalCompFall;
            _player.WallSlideSpeed = _origWallSlideSpeed * verticalCompFall;
        }

        // Note: TurboModeManager no longer forces animator speed every frame.
        // CombatTurboManager now manages animation speed, so nothing to enforce here.
    }

    public bool TryStartTurbo(PlayerController player, PlayerAnimator anim)
    {
        if (_isActive || _onCooldown) return false;

        var mm = MomentumManager.Instance;
        if (mm == null) return false;

        float cost = mm.MaxMomentum * _momentumCostPct;
        if (mm.CurrentMomentum < cost) return false;

        // Spend momentum and pause future gain while Turbo is active
        mm.AddMomentum(-cost);
        mm.SetGainPaused(true);

        _player = player;
        _anim = anim;

        // Cache originals
        _originalMoveSpeed = _player.MoveSpeed;
        _originalAnimSpeed = 1f;

        // World slowdown
        Time.timeScale = _slowFactor;
        Time.fixedDeltaTime = _originalFixedDelta * _slowFactor;

        // Compute compensation and store for reuse during Turbo
        _comp = (1f / _slowFactor) * _playerSpeedMult;

        // Cache additional movement parameters
        _origAcc = _player.Acceleration;
        _origDec = _player.Deceleration;
        _originalRotateSpeed = _player.RotateSpeed;
        _originalDashForce = _player.DashForce;
        _origJumpForce = _player.JumpForce;
        _origWallJumpForce = _player.WallJumpForce;
        _origWallJumpHForce = _player.WallJumpHorizontalForce;
        _origJumpCutMultiplier = _player.JumpCutMultiplier;

        // Cache fall parameters to compensate vertical speed.  Global slow-mo
        // slows falling because physics integration uses Time.deltaTime * timeScale.
        // To maintain normal fall speed we will later increase the fall multiplier
        // and max fall speeds.  See Update() for the reapply logic.
        _origFallMultiplier = _player.FallMultiplier;
        _origMaxFallSpeed = _player.MaxFallSpeed;
        _origWallSlideSpeed = _player.WallSlideSpeed;
        // Cache hold jump height so we can boost it during Turbo.
        _origMaxHoldJumpHeight = _player.MaxHoldJumpHeight;


        // Apply horizontal compensation.  Use SetAccelDecel so that acceleration and
        // deceleration are set explicitly rather than compounded by multiple calls.
        _player.SetMoveSpeed(_originalMoveSpeed * _comp);
        _player.SetAccelDecel(_origAcc * _comp, _origDec * _comp);
        _player.RotateSpeed = _originalRotateSpeed * _comp;
        _player.DashForce = _originalDashForce * _comp;

        // Apply vertical compensation with separate tuning for jump forces and falling.
        // Jumps use the full compensation (_comp) so takeoff speed matches horizontal feel.
        float verticalCompStartJump = _comp;
        _player.JumpForce = _origJumpForce * verticalCompStartJump;
        _player.WallJumpForce = _origWallJumpForce * verticalCompStartJump;
        // Horizontal component of wall jump still uses horizontal compensation
        _player.WallJumpHorizontalForce = _origWallJumpHForce * _comp;

        // Limit hold-jump height using only playerSpeedMult to avoid excessively tall jumps
        _player.MaxHoldJumpHeight = _origMaxHoldJumpHeight * _playerSpeedMult;

        // Falling uses an extra boost: multiply the full compensation by playerSpeedMult
        float verticalCompStartFall = _comp * _fallTurboScale;
        _player.FallMultiplier = _origFallMultiplier * verticalCompStartFall;
        _player.MaxFallSpeed = _origMaxFallSpeed * verticalCompStartFall;
        _player.WallSlideSpeed = _origWallSlideSpeed * verticalCompStartFall;

        // Note: Combat animation speed buff is now managed by CombatTurboManager.

        // If the player was holding a direction, snap to new speed immediately
        if (_player.IsHoldingMove)
        {
            _player.ApplyBufferedMovement(_player.GetLastMoveInput());
        }

        _isActive = true;
        onTurboStart?.Invoke();
        _player.StartCoroutine(Co_TurboTimer());
        return true;
    }

    private System.Collections.IEnumerator Co_TurboTimer()
    {
        float t = 0f;
        while (t < _duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        StopTurbo();
    }

    public void StopTurbo()
    {
        if (!_isActive) return;

        // Restore time
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _originalFixedDelta;

        // Restore player movement feel, physics, and vertical fall settings
        if (_player != null)
        {
            _player.SetMoveSpeed(_originalMoveSpeed);
            _player.SetAccelDecel(_origAcc, _origDec);
            _player.RotateSpeed = _originalRotateSpeed;
            _player.DashForce = _originalDashForce;
            _player.JumpForce = _origJumpForce;
            _player.WallJumpForce = _origWallJumpForce;
            _player.WallJumpHorizontalForce = _origWallJumpHForce;
            _player.JumpCutMultiplier = _origJumpCutMultiplier;
            // Restore vertical fall parameters to their original values.
            _player.FallMultiplier = _origFallMultiplier;
            _player.MaxFallSpeed = _origMaxFallSpeed;
            _player.WallSlideSpeed = _origWallSlideSpeed;

            // Restore the original hold jump height.
            _player.MaxHoldJumpHeight = _origMaxHoldJumpHeight;
        }

        // Restore animator speed. Combat animation buff is handled by CombatTurboManager.
        if (_anim != null)
            _anim.SetAttackSpeed(_originalAnimSpeed);

        // Resume momentum gain
        MomentumManager.Instance?.SetGainPaused(false);

        _isActive = false;
        _onCooldown = true;
        _cooldownTimer = _cooldown;

        onTurboEnd?.Invoke();

        _player = null;
        _anim = null;
    }

    public bool IsActive => _isActive;
    public bool IsOnCooldown => _onCooldown;
}
