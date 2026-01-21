using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator _anim;           // Your Animator component
    [SerializeField] private PlayerMotor _player;
    [SerializeField] private CombatController _combat;


    // Cached Animator parameter hashes
    private int _hashSpeed;
    private int _hashVerticalSpeed;
    private int _hashIsGrounded;
    private int _hashIsJumping;
    private int _hashIsAirJumping;
    private int _hashIsDashJumping;
    private int _hashWallJump;
    private int _hashDash;
    private int _hashWallHangIn;    // Trigger for “hang-in” clip
    private int _hashWallHangLoop;  // Bool for staying in WallHangLoop
    private int[] _attackHashes;
    private int _hashDashAttack;
    private int _hashDamage;
    private int _hashIsHurt; 
    private bool _wasWallSliding = false;
    private bool _justWallJumped = false;
    private int _hashKnockdown;
    private int _hashRecover;
    private int _hashTurboStart;
    private int _hashDie;
    private int _hashDeadLoop;



    // NEW: cache the Rigidbody on your player parent
    private Rigidbody _rb;

    private void Awake()
    {
        // Names must match exactly your Animator parameters
        _hashSpeed = Animator.StringToHash("Speed");
        _hashVerticalSpeed = Animator.StringToHash("VerticalSpeed");
        _hashIsGrounded = Animator.StringToHash("IsGrounded");
        _hashIsJumping = Animator.StringToHash("IsJumping");
        _hashIsAirJumping = Animator.StringToHash("IsAirJumping");
        _hashIsDashJumping = Animator.StringToHash("IsDashJumping");
        _hashWallJump = Animator.StringToHash("WallJump");
        _hashDash = Animator.StringToHash("IsDashing");
        _hashWallHangIn = Animator.StringToHash("WallHangIn");
        _hashWallHangLoop = Animator.StringToHash("IsWallHanging");
        _hashDamage = Animator.StringToHash("Damage");
        _hashIsHurt = Animator.StringToHash("IsHurt");
        _hashKnockdown = Animator.StringToHash("Knockdown");
        _hashRecover = Animator.StringToHash("Recover");
        _attackHashes = new[]
       {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3")
        };
        _hashDashAttack = Animator.StringToHash("DashAttack");
        _hashTurboStart = Animator.StringToHash("TurboStart");
        _hashDie = Animator.StringToHash("Die");
        _hashDeadLoop = Animator.StringToHash("IsDead");
        // 2) grab the Rigidbody up the hierarchy
        _rb = GetComponentInParent<Rigidbody>();
        if (_rb == null)
            Debug.LogError("PlayerAnimator: could not find a Rigidbody in parent!", this);
    }

    private void LateUpdate()
    {
        if (_player == null || _anim == null) return;

        float currentSpeed = _player.GetCurrentMovementSpeedNormalized();
        float vSpeed = _player.VerticalSpeed;
        bool isWallSliding = _player.IsWallSliding;

        // Animator grounded should be RAW (raycast), not coyote
        bool grounded = _player.IsGroundedRaw;

        // Extra safety: if moving upward, never call it grounded
        if (vSpeed > 0.1f) grounded = false;

        // wall hang transitions (same as you had)
        if (!_justWallJumped && !_wasWallSliding && isWallSliding)
        {
            _anim.SetTrigger(_hashWallHangIn);
            _anim.SetBool(_hashWallHangLoop, true);
        }
        else if (!_justWallJumped && _wasWallSliding && !isWallSliding)
        {
            _anim.SetBool(_hashWallHangLoop, false);
        }

        // Use unscaled dt during Turbo so damping doesn't “slow”
        float dt = (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        _anim.SetFloat(_hashSpeed, currentSpeed, 0.1f, dt);
        _anim.SetFloat(_hashVerticalSpeed, vSpeed);
        _anim.SetBool(_hashIsGrounded, grounded);

        _justWallJumped = false;
        _wasWallSliding = isWallSliding;
    }

    // This is called every frame _after_ animation is evaluated (if applyRootMotion = true)
    private void OnAnimatorMove()
    {
        if (!_anim.applyRootMotion || _rb == null)
            return;


        // grab the raw root-motion delta
        Vector3 delta = _anim.deltaPosition;

        // kill any Z movement
        delta.z = 0f;

        // apply only X/Y
        _rb.MovePosition(_rb.position + delta);
        _rb.MoveRotation(_rb.rotation * _anim.deltaRotation);
    }
    /// <summary>
    /// Called by PlayerController for a normal ground/double jump.
    /// </summary>
    public void TriggerJump()
    {
        _anim.SetTrigger(_hashIsJumping);
    }

    public void TriggerAirJump()
    {
        _anim.SetTrigger(_hashIsAirJumping);
    }

    public void TriggerDashJump()
    {
        _anim.SetTrigger(_hashIsDashJumping);
    }
    /// <summary>
    /// Called by PlayerController when performing a wall-jump.
    /// Immediately clears the hang loop and goes into WallJump.
    /// </summary>
    public void TriggerWallJump()
    {
        // 1) Exit the loop immediately
        _anim.SetBool(_hashWallHangLoop, false);

        // 2) Fire the WallJump trigger to transition into the WallJump state
        _anim.SetTrigger(_hashWallJump);

        // 3) Prevent any “start/stop hang” logic this frame
        _justWallJumped = true;
        _wasWallSliding = false;
    }

    /// <summary>
    /// Called by PlayerController when starting a dash.
    /// </summary>
    public void TriggerDash()
    {
        _anim.SetTrigger(_hashDash);
    }

    public void SetHurt(bool on)
    {
        _anim.SetBool(_hashIsHurt, on);
    }

    public void TriggerKnockdown() => _anim.SetTrigger(_hashKnockdown);
    public void TriggerRecover() => _anim.SetTrigger(_hashRecover);
    public void TriggerDamage()
    {
        _anim.SetTrigger(_hashDamage);
    }

    public void OnOpenComboWindow() => _combat.OnOpenComboWindow();
    public void OnCloseComboWindow() => _combat.OnCloseComboWindow();
    public void OnCloseDashAttackWindow() => _combat.OnDashAttackEnd();
    public void TriggerAttack(int idx)
    {
        if (idx < 0 || idx >= _attackHashes.Length)
            throw new ArgumentOutOfRangeException(nameof(idx));
        _anim.SetTrigger(_attackHashes[idx]);
    }

    public void TriggerDashAttack()
    {
        _anim.SetTrigger(_hashDashAttack);
    }

    public void TriggerTurboStart()
    {
        _anim.SetTrigger(_hashTurboStart);
    }
    public void TriggerDie()
    {
        _anim.SetTrigger(_hashDie);
    }

    public void SetDeadLoop(bool on)
    {
        _anim.SetBool(_hashDeadLoop, on);
    }
    public void SetAttackSpeed(float multiplier)
    {
        _anim.speed = multiplier;
    }

    public void SetApplyRootMotion(bool on)
    {
        _anim.applyRootMotion = on;
    }

    public void PauseAnimator()
    {
        if (_anim) _anim.speed = 0f;
    }

    public void ResumeAnimator()
    {
        if (_anim) _anim.speed = 1f;
    }


    public UniTask WaitForCurrentAnimationEnd(CancellationToken ct = default)
    {
        return UniTask.WaitUntil(
            () => _anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f,
            cancellationToken: ct
        );
    }

    public void ResetForRespawn()
    {
        SetHurt(false);
       
        // if you have death loop bool / dead flag
        SetDeadLoop(false);

        // if you use root motion during attacks
        SetApplyRootMotion(false);

        // reset attack speed
        SetAttackSpeed(1f);
    }
}