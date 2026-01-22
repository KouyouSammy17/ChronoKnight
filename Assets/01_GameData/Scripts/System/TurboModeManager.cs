using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TurboModeManager : MonoBehaviour
{
    public static TurboModeManager Instance { get; private set; }

    [Header("Design")]
    [SerializeField, Tooltip("How slow the world gets (0.35 = 65% slower).")]
    private float _slowFactor = 0.35f;

    [Header("Real-time Multipliers")]
    [SerializeField, Tooltip("Move + Attack are 1.5x in REAL TIME during Turbo.")]
    private float _moveAttackRealMult = 1.5f;

    [SerializeField, Tooltip("Everything else is 1.1x in REAL TIME during Turbo.")]
    private float _otherRealMult = 1.1f;

    [SerializeField, Tooltip("Extra multiplier for fall speed during Turbo (1 = normal).")]
    private float _fallTurboScale = 1f;

    [SerializeField, Tooltip("Turbo duration (seconds, in real time).")]
    private float _duration = 10f;

    [SerializeField, Tooltip("Scale applied to JumpCutMultiplier during Turbo (<1 = stronger cut).")]
    private float _jumpCutTurboScale = 0.75f;

    [SerializeField, Tooltip("Cooldown after Turbo ends (real-time seconds).")]
    private float _cooldown = 6f;

    [Header("Momentum Cost")]
    [SerializeField, Tooltip("How much momentum to spend to start Turbo (0-1 of Max).")]
    private float _momentumCostPct = 0.25f;

    [Header("Tutorial Gate")]
    [SerializeField] private bool _turboUnlocked = false;

    [Header("Events")]
    public UnityEvent onTurboStart;
    public UnityEvent onTurboEnd;

    // runtime
    private bool _isActive;
    private bool _onCooldown;
    private float _originalFixedDelta;
    private float _cooldownTimer;

    private PlayerMotor _player;
    private PlayerAnimator _anim;

    // cached originals
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
    private float _comp;

    // Expose multipliers for other scripts
    public float AttackComp => _moveAttackRealMult;         // CombatController uses this (real-time)
    public float MoveComp => _moveAttackRealMult;           // if you need it elsewhere (real-time)
    public float OtherAnimComp => _otherRealMult;           // CombatTurboManager uses this (real-time)
    public float TurboComp => _comp;                        // (1/slowFactor) * 1.5
    public float SlowFactor => _slowFactor;

    // Cancel slow-mo ONLY (no boost). Good for damage impulses.
    public float KnockbackComp => 1f / Mathf.Max(0.0001f, _slowFactor);
    public float RealTimeComp => 1f / Mathf.Max(0.0001f, _slowFactor);

    public bool IsActive => _isActive;
    public bool IsOnCooldown => _onCooldown;
    public bool TurboUnlocked => _turboUnlocked;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        _originalFixedDelta = Time.fixedDeltaTime;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded_ResetTurbo;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_ResetTurbo;
    }

    private void OnSceneLoaded_ResetTurbo(Scene s, LoadSceneMode mode)
    {
        ForceReset(clearCooldown: true);
    }

    private void OnDestroy()
    {
        if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDelta;
        }
        MomentumManager.Instance?.SetGainPaused(false);
    }

    private void Update()
    {
        if (_onCooldown)
        {
            _cooldownTimer -= Time.unscaledDeltaTime;
            if (_cooldownTimer <= 0f) _onCooldown = false;
        }

        if (_isActive && _player != null)
        {
            ApplyTurboScaledValues();
        }
    }

    public bool TryStartTurbo(PlayerMotor player, PlayerAnimator anim)
    {
        if (!_turboUnlocked) return false;
        if (_isActive || _onCooldown) return false;

        var mm = MomentumManager.Instance;
        if (mm == null) return false;

        float cost = mm.MaxMomentum * _momentumCostPct;
        if (mm.CurrentMomentum < cost) return false;

        // Spend momentum and pause gain
        mm.AddMomentum(-cost);
        mm.SetGainPaused(true);

        _player = player;
        _anim = anim;

        // IMPORTANT: Animator should ignore timeScale during Turbo
        if (_anim != null)
            _anim.SetTurboAnimMode(true);

        // Cache originals
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

        // World slowdown
        Time.timeScale = _slowFactor;
        Time.fixedDeltaTime = _originalFixedDelta * _slowFactor;

        // Apply scaling once immediately
        ApplyTurboScaledValues();

        // snap to new move speed if holding input
        if (_player.IsHoldingMove)
            _player.ApplyBufferedMovement(_player.GetLastMoveInput());

        _isActive = true;
        onTurboStart?.Invoke();
        _player.StartCoroutine(Co_TurboTimer());
        return true;
    }

    private void ApplyTurboScaledValues()
    {
        float worldComp = 1f / Mathf.Max(0.0001f, _slowFactor);

        // MAIN COMP for other systems (move/attack 1.5 in real time)
        _comp = worldComp * _moveAttackRealMult;

        // Per-second values (affected by timeScale) need worldComp to become “real-time”
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

        // dash is “per second” feel
        _player.DashForce = _originalDashForce * otherPerSecondComp;

        // jump takeoff (instant)
        _player.DashJumpForce = _originalDashJumpForce *dashPerSecondComp;
        _player.DashJumpBonusUpVelocity = _origDashJumpBonusUpVel * dashPerSecondComp; // contributes to velocity, so compensate too
        _player.JumpForce = _origJumpForce * otherPerSecondComp;
        _player.WallJumpForce = _origWallJumpForce * otherPerSecondComp;
        _player.WallJumpHorizontalForce = _origWallJumpHForce * otherPerSecondComp;

        // keep your cut tuning
        _player.JumpCutMultiplier = _origJumpCutMultiplier * _jumpCutTurboScale;

        // falling/gravity needs real-time compensation (per-second)
        float fallComp = otherPerSecondComp * _fallTurboScale;
        _player.FallMultiplier = _origFallMultiplier * fallComp;
        _player.MaxFallSpeed = _origMaxFallSpeed * fallComp;
        _player.WallSlideSpeed = _origWallSlideSpeed * fallComp;
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

        // Restore player
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

        // Restore animator
        if (_anim != null)
        {
            _anim.SetTurboAnimMode(false);
            _anim.SetAttackSpeed(1f);
        }

        // Resume momentum gain
        MomentumManager.Instance?.SetGainPaused(false);

        _isActive = false;
        _onCooldown = true;
        _cooldownTimer = _cooldown;

        onTurboEnd?.Invoke();

        _player = null;
        _anim = null;
    }

    public void SetTurboUnlocked(bool unlocked) => _turboUnlocked = unlocked;

    public void ForceReset(bool clearCooldown = true)
    {
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
        if (clearCooldown)
        {
            _onCooldown = false;
            _cooldownTimer = 0f;
        }

        _player = null;
        _anim = null;
    }

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
