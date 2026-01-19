using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMotor), typeof(PlayerStats))]
public class PlayerDamageReceiver : MonoBehaviour
{
    [Header("Ground Hit Reaction")]
    [SerializeField] private float _hitStun = 0.25f;
    [SerializeField] private float _knockback = 8f;
    [SerializeField] private float _iframes = 0.5f;
    [SerializeField] private bool _ignoreTimeScale = true;

    [Header("Velocity Handling")]
    [SerializeField] private bool _cancelHorizontalVelocityOnHit = true;
    [SerializeField] private bool _keepUpwardVelocity = true;

    [Header("Air Hit (Kirby / Smash style)")]
    [SerializeField] private bool _airDamageKnockdown = true;
    [SerializeField] private float _airLaunchUp = 6f;
    [SerializeField] private float _airLaunchHorizontalMultiplier = 1.0f;

    [Header("Optional Slam Down")]
    [SerializeField] private bool _useSlamDown = true;
    [SerializeField] private float _slamDelay = 0.08f;
    [SerializeField] private float _slamDownAccel = 45f;

    [Header("Landing Knockdown Sequence")]
    [SerializeField] private float _minAirTumbleTime = 0.10f;
    [SerializeField] private float _impactToDownDelay = 0.12f;
    [SerializeField] private float _recoverTime = 0.5f;

    [Header("Facing")]
    [SerializeField] private bool _faceAttackerOnHit = true;

    private PlayerMotor _motor;
    private PlayerAnimator _anim;
    private Rigidbody _rb;
    private CombatController _combat;

    private CancellationTokenSource _hitCts;
    private CancellationTokenSource _invulnCts;

    private bool _invuln;
    public bool IsInvulnerable => _invuln;
    public bool IsInHitStun { get; private set; }
    public bool IsKnockedDown { get; private set; }

    private void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _anim = GetComponentInChildren<PlayerAnimator>();
        _combat = GetComponent<CombatController>();
        _rb = _motor != null ? _motor.GetRigidbody() : GetComponent<Rigidbody>();
    }

    private void OnDisable()
    {
        _hitCts?.Cancel();
        _hitCts?.Dispose();
        _hitCts = null;
        
        _invulnCts?.Cancel();
        _invulnCts?.Dispose();
        _invulnCts = null;


        _invuln = false;
        IsInHitStun = false;
        IsKnockedDown = false;

        _anim?.SetHurt(false);
        _motor?.EnableInput();
    }

    public void SetInvulnerable(bool v) => _invuln = v;

    public async UniTaskVoid PlayHitReact(Vector3? sourceWorldPos = null, float extraForce = 0f)
    {
        if (_motor == null || _rb == null) return;
        if (_invuln) return;
        var stats = GetComponent<PlayerStats>();
        if (stats != null && stats.CurrentHP <= 0) return;

        // cancel previous reaction
        _hitCts?.Cancel();
        _hitCts?.Dispose();
        _hitCts = new CancellationTokenSource();
        var ct = _hitCts.Token;

        _invuln = true;
        IsInHitStun = true;
        IsKnockedDown = false;

        // lock control
        _motor.DisableInput();

        // IMPORTANT: cancel combat, but do NOT allow it to re-enable control mid-hit
        _combat?.CancelCombo(); // if your CancelCombo enables input in finally, fix it (see note below)

        // also clear input buffers so we don't Ågauto attackÅh on recovery
        _motor.GetComponent<PlayerStateMachineBrain>()?.Input?.ClearPressedBuffers();

        try
        {
            bool inAir = !_motor.IsGrounded;

            if (_airDamageKnockdown && inAir)
            {
                await PlayAirKnockdownSequence(sourceWorldPos, extraForce, ct);
                await DelaySeconds(_iframes, ct);
                return;
            }

            // ground hit
            ApplyKnockback(sourceWorldPos, extraForce, verticalLaunch: 0f, horizontalMultiplier: 1f);

            _anim?.SetHurt(true);
            _anim?.TriggerDamage();

            await DelaySeconds(_hitStun, ct);

            _anim?.SetHurt(false);
            IsInHitStun = false;
            _motor.EnableInput();

            await DelaySeconds(_iframes, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            // cleanup state
            _anim?.SetHurt(false);

            if (!IsKnockedDown) _motor?.EnableInput();

            IsInHitStun = false;
            _invuln = false;

            _hitCts?.Dispose();
            _hitCts = null;
        }
    }

    private async UniTask PlayAirKnockdownSequence(Vector3? sourceWorldPos, float extraForce, CancellationToken ct)
    {
        IsKnockedDown = true;

        float attackerSideX = GetAttackerSideX(sourceWorldPos);

        if (_faceAttackerOnHit)
        {
            // Face the attacker, then get knocked back away from them.
            float yaw = (attackerSideX >= 0f) ? 90f : -90f;
            _motor.ForceFacingYaw(yaw, snap: true);
        }

        // Launch away in air (X + Y)
        ApplyKnockbackFromSide(attackerSideX, extraForce, _airLaunchUp, _airLaunchHorizontalMultiplier);

        _anim?.SetHurt(true);
        _anim?.TriggerDamage();

        // minimum Ågtumble timeÅh
        await DelaySeconds(_minAirTumbleTime, ct);

        // optional slam-down to force the Ågpushed to groundÅh feel
        if (_useSlamDown)
        {
            await DelaySeconds(_slamDelay, ct);
            if (!ct.IsCancellationRequested)
                _rb.AddForce(Vector3.down * _slamDownAccel, ForceMode.Acceleration);
        }

        // wait until grounded
        await UniTask.WaitUntil(() => _motor.IsGrounded, PlayerLoopTiming.Update, ct);
       
        //  stop sliding immediately on impact
        var v = _rb.linearVelocity;
        v.x = 0f; v.z = 0f; v.y = 0f;
        _rb.linearVelocity = v;

        _motor.StopHorizontalInstant(); // also clears cached motor velocity

        // IMPACT (pushed to ground)
        _anim?.TriggerKnockdown();
        await DelaySeconds(_impactToDownDelay, ct);

        // RECOVER (stand up)
        _anim?.TriggerRecover();
        await DelaySeconds(_recoverTime, ct);

        _anim?.SetHurt(false);

        IsKnockedDown = false;
        IsInHitStun = false;

        _motor.EnableInput();
    }

    private void ApplyKnockback(Vector3? sourceWorldPos, float extraForce, float verticalLaunch, float horizontalMultiplier)
    {
        float attackerSideX = GetAttackerSideX(sourceWorldPos);
        float knockDirX = -attackerSideX; // away from attacker

        float force = (_knockback + Mathf.Max(0f, extraForce)) * horizontalMultiplier;

        Vector3 v = _rb.linearVelocity;
        if (_cancelHorizontalVelocityOnHit) v.x = 0f;
        if (_keepUpwardVelocity) v.y = Mathf.Max(v.y, 0f);
        _rb.linearVelocity = v;

        _rb.AddForce(new Vector3(knockDirX * force, verticalLaunch, 0f), ForceMode.VelocityChange);
    }

    private void ApplyKnockbackFromSide(float attackerSideX, float extraForce, float verticalLaunch, float horizontalMultiplier)
    {
        float knockDirX = -attackerSideX;
        float force = (_knockback + Mathf.Max(0f, extraForce)) * horizontalMultiplier;

        Vector3 v = _rb.linearVelocity;
        if (_cancelHorizontalVelocityOnHit) v.x = 0f;
        if (_keepUpwardVelocity) v.y = Mathf.Max(v.y, 0f);
        _rb.linearVelocity = v;

        _rb.AddForce(new Vector3(knockDirX * force, verticalLaunch, 0f), ForceMode.VelocityChange);
    }


    private float GetAttackerSideX(Vector3? sourceWorldPos)
    {
        if (sourceWorldPos.HasValue)
        {
            float dx = sourceWorldPos.Value.x - transform.position.x;
            return (dx >= 0f) ? 1f : -1f;
        }

        // fallback: Ågbehind youÅh
        float facingX = Mathf.Sign(_motor.GetFacingDirection().x);
        if (Mathf.Abs(facingX) < 0.001f) facingX = 1f;
        return -facingX;
    }

    private UniTask DelaySeconds(float seconds, CancellationToken ct)
    {
        return UniTask.Delay(TimeSpan.FromSeconds(seconds),
            _ignoreTimeScale ? DelayType.Realtime : DelayType.DeltaTime,
            PlayerLoopTiming.Update, ct);
    }

    public async UniTaskVoid SetInvulnerableFor(float seconds)
    {
        _invulnCts?.Cancel();
        _invulnCts?.Dispose();
        _invulnCts = null;

        if (seconds <= 0f)
        {
            _invuln = false;
            return;
        }

        _invuln = true;

        _invulnCts = new CancellationTokenSource();
        var ct = _invulnCts.Token;

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(seconds),
                DelayType.Realtime, PlayerLoopTiming.Update, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            // Clear invulnerability and dispose token even if object still exists.
            if (this != null)
                _invuln = false;

            _invulnCts?.Dispose();
            _invulnCts = null;
        }
    }
}
