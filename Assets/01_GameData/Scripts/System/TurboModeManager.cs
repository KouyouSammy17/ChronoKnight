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
    private float _originalRotateSpeed;
    private float _originalDashForce;
    private float _origJump, _origWallJump, _origWallJumpH;

    public float PlayerComp => (1f / _slowFactor) * _playerSpeedMult;

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

    private void ApplyTimeScale(float s)
    {
        Time.timeScale = s;
        Time.fixedDeltaTime = _originalFixedDelta * s;
        Debug.Log($"[Turbo] timeScale={s}, fixedDT={Time.fixedDeltaTime}");
    }

    public bool TryStartTurbo(PlayerController player, PlayerAnimator anim)
    {
        if (_isActive || _onCooldown) return false;

        var mm = MomentumManager.Instance;
        if (mm == null) return false;

        float cost = mm.MaxMomentum * _momentumCostPct;
        if (mm.CurrentMomentum < cost) return false;

        mm.AddMomentum(-cost);
        mm.SetGainPaused(true);

        _player = player;
        _anim = anim;

        // Slow the world FIRST
        ApplyTimeScale(_slowFactor);              // was: Time.timeScale = _slowFactor; fixedDT scaled too


        // Cache originals
        _originalMoveSpeed = _player.MoveSpeed;
        _originalAnimSpeed = 1f;
        _origAcc = _player.Acceleration;
        _origDec = _player.Deceleration;
        _originalRotateSpeed = _player.RotateSpeed;
        _originalDashForce = _player.DashForce;
        _origJump = _player.JumpForce;
        _origWallJump = _player.WallJumpForce;
        _origWallJumpH = _player.WallJumpHorizontalForce;

        // Apply comp to PLAYER ONLY
        _player.SetMoveSpeed(_originalMoveSpeed * PlayerComp);
        _anim?.SetAttackSpeed(PlayerComp);
        _player.ScaleAccelDecel(PlayerComp);
        _player.RotateSpeed = _originalRotateSpeed * PlayerComp;
        _player.DashForce = _originalDashForce * PlayerComp;
        _player.JumpForce = _origJump * PlayerComp;
        _player.WallJumpForce = _origWallJump * PlayerComp;
        _player.WallJumpHorizontalForce = _origWallJumpH * PlayerComp;

        // >>> INSERT THIS SNAP <<<
        if (_player.IsHoldingMove)
            _player.ApplyBufferedMovement(_player.GetLastMoveInput(), blend: false);


        _isActive = true;                           // set active AFTER applying comp (no longer needed for comp calc)

        onTurboStart?.Invoke();
        _player.StartCoroutine(Co_TurboTimer());   // uses unscaled time already
        return true;
    }

    public void StopTurbo()
    {
        if (!_isActive) return;

        ApplyTimeScale(1f);                         // restore time & fixedDT

        if (_player != null)
        {
            _player.SetMoveSpeed(_originalMoveSpeed);
            _player.SetAccelDecel(_origAcc, _origDec);
            _player.RotateSpeed = _originalRotateSpeed;
            _player.DashForce = _originalDashForce;
            _player.JumpForce = _origJump;
            _player.WallJumpForce = _origWallJump;
            _player.WallJumpHorizontalForce = _origWallJumpH;
        }
        _anim?.SetAttackSpeed(_originalAnimSpeed);

        MomentumManager.Instance?.SetGainPaused(false);

        _isActive = false;
        _onCooldown = true;
        _cooldownTimer = _cooldown;

        onTurboEnd?.Invoke();

        _player = null;
        _anim = null;
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

    public bool IsActive => _isActive;
    public bool IsOnCooldown => _onCooldown;
}
