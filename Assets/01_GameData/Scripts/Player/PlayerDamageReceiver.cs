using UnityEngine;
using Cysharp.Threading.Tasks;

[RequireComponent(typeof(PlayerMotor)), RequireComponent(typeof(PlayerStats))]
public class PlayerDamageReceiver : MonoBehaviour
{
    [Header("Hit Reaction")]
    [SerializeField] float _hitStun = 0.25f;
    [SerializeField] float _knockback = 8f;
    [SerializeField] float _iframes = 0.5f;
    [SerializeField] LayerMask _groundMask;

    PlayerMotor _motor;
    PlayerAnimator _anim;
    Rigidbody _rb;

    bool _invuln;
    bool _attackBuffered;

    public bool IsInvulnerable => _invuln;
    public bool IsInHitStun { get; private set; }

    void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _anim = GetComponentInChildren<PlayerAnimator>();
        _rb = _motor.GetRigidbody();
    }

    public void BufferAttack() => _attackBuffered = true;
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
        if (_invuln) return;

        _invuln = true;
        IsInHitStun = true;

        _motor.DisableInput();
        GetComponent<CombatController>()?.CancelCombo();

        Vector3 dir = sourceWorldPos.HasValue
            ? (transform.position - sourceWorldPos.Value).normalized
            : -_motor.GetFacingDirection();

        dir.y = 0f; dir.z = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.left;

        float force = _knockback + Mathf.Max(0f, extraForce);
        Vector3 v = _rb.linearVelocity; v.y = Mathf.Max(v.y, 0f);
        _rb.linearVelocity = v;
        _rb.AddForce(dir.normalized * force, ForceMode.VelocityChange);

        _anim?.SetAttackSpeed(1f);
        _anim?.SetHurt(true);
        _anim?.TriggerDamage();

        await UniTask.Delay((int)(_hitStun * 1000f), ignoreTimeScale: true);

        _motor.EnableInput();
        _anim?.SetHurt(false);
        IsInHitStun = false;

        if (_attackBuffered)
        {
            _attackBuffered = false;
            GetComponent<CombatController>()?.RequestAttack();
        }

        await UniTask.Delay((int)(_iframes * 1000f), ignoreTimeScale: true);
        _invuln = false;
    }
}
