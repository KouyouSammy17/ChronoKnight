using UnityEngine;
using UnityEngine.Events;

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

    // Additional player parameters cached during Turbo so they can be restored.
    private float _originalRotateSpeed;
    private float _originalDashForce;
    private float _origJump;
    private float _origWallJump;
    private float _origWallJumpH;

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

        _originalMoveSpeed = _player.MoveSpeed;
        _originalAnimSpeed = 1f; // we set animator speed additively

        // World slowdown
        Time.timeScale = _slowFactor;                  // e.g., 0.35
        Time.fixedDeltaTime = _originalFixedDelta * _slowFactor;

        // Player feels faster than the world:
        //   comp = cancel world slow (1/slowFactor) × your extra boost (1.5x)
        float comp = (1f / _slowFactor) * _playerSpeedMult; // e.g., 1/0.35 * 1.5 ≈ 4.2857

        // MoveSpeed already boosted:
        _originalMoveSpeed = _player.MoveSpeed;
        _player.SetMoveSpeed(_originalMoveSpeed * comp);

        // Animator already sped up:
        _originalAnimSpeed = 1f;
        _anim?.SetAttackSpeed(comp);

        // NEW: match acceleration feel during slow-mo
        _origAcc = _player.Acceleration;
        _origDec = _player.Deceleration;
        _player.ScaleAccelDecel(comp);

        // Cache and scale additional movement parameters so Turbo feels responsive
        // even when the world is slowed. Without scaling these values the player
        // would rotate and dash at the same pace as the slowed world.
        _originalRotateSpeed = _player.RotateSpeed;
        _originalDashForce = _player.DashForce;
        _origJump = _player.JumpForce;
        _origWallJump = _player.WallJumpForce;
        _origWallJumpH = _player.WallJumpHorizontalForce;

        _player.RotateSpeed = _originalRotateSpeed * comp;
        _player.DashForce = _originalDashForce * comp;
        _player.JumpForce = _origJump * comp;
        _player.WallJumpForce = _origWallJump * comp;
        _player.WallJumpHorizontalForce = _origWallJumpH * comp;

        // Snap horizontal velocity so the player immediately feels the speed boost.
        if (_player.IsHoldingMove)
        {
            _player.ApplyBufferedMovement(_player.GetLastMoveInput());
        }

        _isActive = true;
        onTurboStart?.Invoke();
        // Run the timer in unscaled (real) time
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

        // Restore player movement feel and parameters
        if (_player != null)
        {
            // Restore base movement speed
            _player.SetMoveSpeed(_originalMoveSpeed);
            // Restore accel/decel
            _player.SetAccelDecel(_origAcc, _origDec);
            // Restore rotate/dash/jump settings
            _player.RotateSpeed = _originalRotateSpeed;
            _player.DashForce = _originalDashForce;
            _player.JumpForce = _origJump;
            _player.WallJumpForce = _origWallJump;
            _player.WallJumpHorizontalForce = _origWallJumpH;
        }
        _anim?.SetAttackSpeed(_originalAnimSpeed);

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
