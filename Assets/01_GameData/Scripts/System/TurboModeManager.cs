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

    [SerializeField, Tooltip("Turbo duration (seconds, in real time).")]
    private float _duration = 10f;

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

    // Caches for vertical motion parameters to restore after Turbo.
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
        // This prevents other systems (e.g., MomentumBuffsManager) from overriding
        // the player's speed, acceleration, rotation, dash, and jump parameters
        // while Turbo is active. Without this, buffs or debuffs applied elsewhere
        // could inadvertently cancel out Turbo scaling.
        if (_isActive && _player != null)
        {
            _player.SetMoveSpeed(_originalMoveSpeed * _comp);
            _player.SetAccelDecel(_origAcc * _comp, _origDec * _comp);
            _player.RotateSpeed = _originalRotateSpeed * _comp;
            _player.DashForce = _originalDashForce * _comp;
            _player.JumpForce = _origJumpForce * _comp;
            _player.WallJumpForce = _origWallJumpForce * _comp;
            _player.WallJumpHorizontalForce = _origWallJumpHForce * _comp;
            // Continually reapply fall multiplier to prevent other systems from overwriting it
            float slowFactor = _slowFactor;
            _player.FallMultiplier = _origFallMultiplier + (1f - slowFactor);
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

        // Cache vertical motion parameters
        _origFallMultiplier = _player.FallMultiplier;
        _origMaxFallSpeed = _player.MaxFallSpeed;
        _origWallSlideSpeed = _player.WallSlideSpeed;

        // Apply compensation to player movement/physics. Use SetAccelDecel to set
        // exact compensated values rather than scaling (avoids compounding if other
        // systems adjust acceleration or deceleration while Turbo is active).
        _player.SetMoveSpeed(_originalMoveSpeed * _comp);
        _player.SetAccelDecel(_origAcc * _comp, _origDec * _comp);
        _player.RotateSpeed = _originalRotateSpeed * _comp;
        _player.DashForce = _originalDashForce * _comp;
        _player.JumpForce = _origJumpForce * _comp;
        _player.WallJumpForce = _origWallJumpForce * _comp;
        _player.WallJumpHorizontalForce = _origWallJumpHForce * _comp;

        // Adjust fall multiplier to maintain falling speed during slow-mo. We add
        // (1 – slowFactor) so that extra gravity is increased when time is slowed.
        float slowFactor = _slowFactor;
        float newFallMult = _origFallMultiplier + (1f - slowFactor);
        _player.FallMultiplier = newFallMult;

        // Note: combat animation speed buff is now managed by CombatTurboManager.

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
            // Restore fall multiplier
            _player.FallMultiplier = _origFallMultiplier;
            // MaxFallSpeed and WallSlideSpeed remain unchanged during Turbo, so no need to restore.
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
