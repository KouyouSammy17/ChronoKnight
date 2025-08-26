using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator _anim;           // Your Animator component
    [SerializeField] private PlayerController _player; // Your PlayerController component
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

    private bool _wasWallSliding = false;
    private bool _justWallJumped = false;

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
        _attackHashes = new[]
       {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3")
        };
        // 2) grab the Rigidbody up the hierarchy
        _rb = GetComponentInParent<Rigidbody>();
        if (_rb == null)
            Debug.LogError("PlayerAnimator: could not find a Rigidbody in parent!", this);
    }

    private void Update()
    {
        // 1) Read current movement/grounding/wall state
        float currentSpeed = _player.GetCurrentMovementSpeedNormalized();
        float vSpeed = _player.VerticalSpeed;
        bool grounded = _player.IsGrounded;
        bool isWallSliding = _player.IsWallSliding;

        // 2a) Just started hanging on a wall? (skip if we just wall-jumped)
        if (!_justWallJumped && !_wasWallSliding && isWallSliding)
        {
            _anim.SetTrigger(_hashWallHangIn);
            _anim.SetBool(_hashWallHangLoop, true);
        }
        // 2b) Just stopped hanging (drop-off), but skip if we just wall-jumped
        else if (!_justWallJumped && _wasWallSliding && !isWallSliding)
        {
            // Simply clear the loop; no exit animation
            _anim.SetBool(_hashWallHangLoop, false);
        }

        // 3) Update core blend/jump/dash parameters
        _anim.SetFloat(_hashSpeed,currentSpeed, 0.1f, Time.deltaTime); // use normalized speed here
        _anim.SetFloat(_hashVerticalSpeed, vSpeed);
        _anim.SetBool(_hashIsGrounded, grounded);

        // 4) Reset “just wall-jumped” and record wall-slide state
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

    public void OnOpenComboWindow() => _combat.OnOpenComboWindow();
    public void OnCloseComboWindow() => _combat.OnCloseComboWindow();
    public void TriggerAttack(int idx)
    {
        if (idx < 0 || idx >= _attackHashes.Length)
            throw new ArgumentOutOfRangeException(nameof(idx));
        _anim.SetTrigger(_attackHashes[idx]);
    }

    public void SetAttackSpeed(float multiplier)
    {
        _anim.speed = multiplier;
    }

    public void SetApplyRootMotion(bool on)
    {
        _anim.applyRootMotion = on;
    }

    public UniTask WaitForCurrentAnimationEnd(CancellationToken ct = default)
    {
        return UniTask.WaitUntil(
            () => _anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f,
            cancellationToken: ct
        );
    }
}