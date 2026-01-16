using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(PlayerMotor)), RequireComponent(typeof(PlayerStats))]
public class PlayerDamageReceiver : MonoBehaviour
{
    [Header("Hit Reaction")]
    [SerializeField] float _hitStun = 0.25f;
    [SerializeField] float _knockback = 8f;
    [SerializeField] float _iframes = 0.5f;
    [SerializeField] LayerMask _groundMask;

    [Header("Velocity Handling")]
    [SerializeField] private bool _cancelHorizontalVelocityOnHit = true;
    [SerializeField] private bool _keepUpwardVelocity = true;

    [Header("Air Knockdown")]
    [SerializeField] private bool _airDamageKnockdown = true;
    [SerializeField] private float _airKnockUp = 4f;          // small pop
    [SerializeField] private float _slamDelay = 0.08f;         // wait then force down
    [SerializeField] private float _slamDownForce = 25f;       // pushes to ground
    [SerializeField] private float _downTime = 0.6f;           // lying duration (seconds)
    [SerializeField] private float _recoverTime = 0.5f;        // stand-up duration (seconds)

    [Header("Rotation Lock (No Rotate On Damage)")]
    [SerializeField, Tooltip("Freeze Y rotation during hit-stun so the player never flips/rotates on damage.")]
    private bool _freezeYawDuringHitStun = true;

    [SerializeField, Tooltip("If true, freezes X/Y/Z rotation during hit-stun (stronger lock).")]
    private bool _freezeAllRotationDuringHitStun = false;

    private CancellationTokenSource _hitCts;

   private PlayerMotor _motor;
   private PlayerAnimator _anim;
   private Rigidbody _rb;
    private CombatController _combat;

    bool _invuln;
    public bool IsInvulnerable => _invuln;
    public bool IsInHitStun { get; private set; }
    public bool IsKnockedDown { get; private set; }

    void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _anim = GetComponentInChildren<PlayerAnimator>();
        _rb = _motor.GetRigidbody();
    }

    private void OnDisable()
    {
        _hitCts?.Cancel();
        _hitCts?.Dispose();
        _hitCts = null;

        _invuln = false;
        IsInHitStun = false;
        IsKnockedDown = false;
    }
    public void SetInvulnerable(bool v) => _invuln = v;

    public async UniTaskVoid SetInvulnerableFor(float seconds)
    {
        if (seconds <= 0f) { _invuln = false; return; }
        _invuln = true;
        await UniTask.Delay((int)(seconds * 1000f), ignoreTimeScale: true);
        _invuln = false;
    }

    public async UniTaskVoid PlayHitReact(Vector3? sourceWorldPos = null, float extraForce = 0f)
    {
        if (_motor == null || _rb == null) return;
        if (_invuln) return;

        // cancel previous reaction
        _hitCts?.Cancel();
        _hitCts?.Dispose();
        _hitCts = new CancellationTokenSource();
        var ct = _hitCts.Token;

        _invuln = true;
        IsInHitStun = true;

        _motor.DisableInput();
        _combat = _combat ?? GetComponent<CombatController>();
        _combat?.CancelCombo();

        RigidbodyConstraints prevConstraints = _rb.constraints;

        try
        {
            // Air knockdown path
            if (_airDamageKnockdown && !_motor.IsGrounded)
            {
                await PlayAirKnockdownSequence(sourceWorldPos, extraForce, ct);

                // after knockdown sequence, we still want i-frames
                IsInHitStun = false;
                await UniTask.Delay((int)(_iframes * 1000f), ignoreTimeScale: true);
                return;
            }

            // Rotation lock during hitstun (optional)
            if (_freezeYawDuringHitStun || _freezeAllRotationDuringHitStun)
            {
                if (_freezeAllRotationDuringHitStun)
                    _rb.constraints = prevConstraints | RigidbodyConstraints.FreezeRotation;
                else
                    _rb.constraints = prevConstraints | RigidbodyConstraints.FreezeRotationY;

                _rb.angularVelocity = Vector3.zero;
            }

            // Knockback direction (X only)
            Vector3 dir = sourceWorldPos.HasValue
                ? (transform.position - sourceWorldPos.Value)
                : -transform.forward;

            dir.y = 0f;
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.left;

            float force = _knockback + Mathf.Max(0f, extraForce);

            Vector3 v = _rb.linearVelocity;
            if (_cancelHorizontalVelocityOnHit) v.x = 0f;
            if (_keepUpwardVelocity) v.y = Mathf.Max(v.y, 0f);
            _rb.linearVelocity = v;

            _rb.AddForce(dir.normalized * force, ForceMode.VelocityChange);

            _anim?.SetAttackSpeed(1f);
            _anim?.SetHurt(true);
            _anim?.TriggerDamage();

            await UniTask.Delay((int)(_hitStun * 1000f), ignoreTimeScale: true);
        }
        catch (OperationCanceledException) { }
        finally
        {
            // restore constraints
            if (_rb != null) _rb.constraints = prevConstraints;

            _anim?.SetHurt(false);
            IsInHitStun = false;

            // restore control
            _motor.EnableInput();

            // i-frames end
            // (if canceled, this will throw; ignore)
            try
            {
                await UniTask.Delay((int)(_iframes * 1000f), ignoreTimeScale: true);
            }
            catch { }

            _invuln = false;
        }
    }


    private async UniTask PlayAirKnockdownSequence(Vector3? sourceWorldPos, float extraForce, CancellationToken ct)
    {
        IsKnockedDown = true;

        // attacker side (+1 right, -1 left)
        float attackerSideX;
        if (sourceWorldPos.HasValue)
            attackerSideX = (sourceWorldPos.Value.x >= transform.position.x) ? 1f : -1f;
        else
        {
            float facingX = Mathf.Sign(_motor.GetFacingDirection().x);
            if (Mathf.Abs(facingX) < 0.001f) facingX = 1f;
            attackerSideX = -facingX;
        }

        // Face attacker (RIGHT=90, LEFT=-90)
        float faceYaw = (attackerSideX > 0f) ? 90f : -90f;
        transform.rotation = Quaternion.Euler(0f, faceYaw, 0f);

        // Knock away in air (X + small Y pop)
        float knockDirX = -attackerSideX;
        float force = _knockback + Mathf.Max(0f, extraForce);

        Vector3 v = _rb.linearVelocity;
        if (_cancelHorizontalVelocityOnHit) v.x = 0f;
        if (_keepUpwardVelocity) v.y = Mathf.Max(v.y, 0f);
        _rb.linearVelocity = v;

        _rb.AddForce(new Vector3(knockDirX * force, _airKnockUp, 0f), ForceMode.VelocityChange);

        _anim?.SetHurt(true);
        _anim?.TriggerDamage();

        // Slam down shortly after (so you land + play impact)
        await UniTask.Delay(TimeSpan.FromSeconds(_slamDelay), DelayType.Realtime, PlayerLoopTiming.Update, ct);
        if (!ct.IsCancellationRequested)
            _rb.AddForce(Vector3.down * _slamDownForce, ForceMode.Acceleration);

        // Wait until grounded
        await UniTask.WaitUntil(() => _motor.IsGrounded, PlayerLoopTiming.Update, ct);

        // Impact -> Down -> Recover
        _anim?.TriggerKnockdown();

        await UniTask.Delay(TimeSpan.FromSeconds(0.15f), DelayType.Realtime, PlayerLoopTiming.Update, ct);
        _anim?.SetDown(true);

        await UniTask.Delay(TimeSpan.FromSeconds(_downTime), DelayType.Realtime, PlayerLoopTiming.Update, ct);

        _anim?.SetDown(false);
        _anim?.TriggerRecover();

        await UniTask.Delay(TimeSpan.FromSeconds(_recoverTime), DelayType.Realtime, PlayerLoopTiming.Update, ct);

        _anim?.SetHurt(false);
        IsKnockedDown = false;
    }
}
