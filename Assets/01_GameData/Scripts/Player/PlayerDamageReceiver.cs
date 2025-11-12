using UnityEngine;
using Cysharp.Threading.Tasks;

[RequireComponent(typeof(PlayerController)), RequireComponent(typeof(PlayerStats))]
public class PlayerDamageReceiver : MonoBehaviour
{
    [Header("Hit Reaction")]
    [SerializeField] float _hitStun = 0.25f;
    [SerializeField] float _knockback = 8f;
    [SerializeField] float _iframes = 0.5f;
    [SerializeField] LayerMask _groundMask;

    PlayerController _ctrl;
    PlayerAnimator _anim;
    PlayerStats _stats;
    Rigidbody _rb;

    bool _invuln;                 // global damage gate (set by hit-react OR externally)
    bool _attackBuffered;         // NEW: attack input buffer during stun

    public bool IsInvulnerable => _invuln;

    void Awake()
    {
        _ctrl = GetComponent<PlayerController>();
        _anim = GetComponentInChildren<PlayerAnimator>();
        _stats = GetComponent<PlayerStats>();
        _rb = _ctrl.GetRigidbody();
    }

    // Called by CombatController when attack is pressed while input is locked
    public void BufferAttack() => _attackBuffered = true;

    // NEW: allow GameManager (or others) to gate damage explicitly (e.g., during fall respawn)
    public void SetInvulnerable(bool v) => _invuln = v;

    // NEW: timed i-frames helper (uses realtime so it also works while paused)
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

        // lock input
        _ctrl.DisableInput();

        // cancel any running combo so animator speed resets
        GetComponent<CombatController>()?.CancelCombo();

        // 2) compute knockback dir (2.5D X-axis only)
        Vector3 dir = sourceWorldPos.HasValue
            ? (transform.position - sourceWorldPos.Value).normalized
            : -_ctrl.GetFacingDirection();

        dir.y = 0f; dir.z = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.left;

        // 3) apply knockback
        float force = _knockback + Mathf.Max(0f, extraForce);
        Vector3 v = _rb.linearVelocity; v.y = Mathf.Max(v.y, 0f);
        _rb.linearVelocity = v;
        _rb.AddForce(dir.normalized * force, ForceMode.VelocityChange);

        // 4) anim
        _anim?.SetAttackSpeed(1f);   // ensure no speed leak
        _anim?.SetHurt(true);
        _anim?.TriggerDamage();

        // 5) wait real-time hit-stun
        await UniTask.Delay((int)(_hitStun * 1000f), ignoreTimeScale: true);

        _ctrl.EnableInput();
        _anim?.SetHurt(false);

        if (_attackBuffered)
        {
            _attackBuffered = false;
            GetComponent<CombatController>()?.RequestAttack();
        }

        // 7) keep i-frames a bit longer, but donÅft block actions during this period
        await UniTask.Delay((int)(_iframes * 1000f), ignoreTimeScale: true);
        _invuln = false;
    }
}
