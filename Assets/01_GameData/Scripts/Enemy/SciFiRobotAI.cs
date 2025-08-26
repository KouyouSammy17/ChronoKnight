using UnityEngine;
using TGRobotsWheeled;    // make sure this matches the asset's namespace

[RequireComponent(typeof(TGDroidStateManager))]
public class SciFiRobotAI : MonoBehaviour
{
    public float aggroRadius = 6f;
    public float attackRange = 10f;

    public float chaseSpeed = 4f;
    public float patrolSpeed = 2f;

    public float minCombatDistance = 3f;

    [Header("Stagger Settings")]
    [Tooltip("How long (in seconds) the droid is unable to shoot after taking damage")]
    public float staggerDuration = 0.5f;

    [Tooltip("Seconds between each shot")]
    public float shootInterval = 1f;

    private float _lastShootTime = -Mathf.Infinity;

    private enum AIState { Patrol, Alert, Combat }
    private AIState _aiState = AIState.Patrol;

    private Transform _player;
    private TGDroidStateManager _droid;

    private float _staggerTimer = 0f;
    private bool _isStaggered = false;

    // Patrol waypoints
    public Transform pointA, pointB;
    private Vector3 _currentPatrolTarget;

    void OnEnable() => _droid.OnShoot += OnDroidShoot;
    void OnDisable() => _droid.OnShoot -= OnDroidShoot;

    void Awake()
    {
        _droid = GetComponent<TGDroidStateManager>();
        _currentPatrolTarget = pointB.position;
    }

    void Update()
    {
        if (_player == null && GameManager.Instance != null)
            _player = GameManager.Instance.GetPlayer()?.transform;
        if (_player == null) return;

        float dist = Vector3.Distance(transform.position, _player.position);

        if (_isStaggered)
        {
            _droid.Shooting = false;              // forcibly stop any firing
            _staggerTimer -= Time.deltaTime;
            if (_staggerTimer <= 0f)
                _isStaggered = false;            // stagger ends
            return;                               // skip all other AI logic this frame
        }


        // --- STATE TRANSITIONS ---
        switch (_aiState)
        {
            case AIState.Patrol:
                if (dist < aggroRadius)
                    _aiState = AIState.Alert;
                break;

            case AIState.Alert:
                if (dist <= attackRange)
                    _aiState = AIState.Combat;
                else if (dist > aggroRadius * 1.2f)
                    _aiState = AIState.Patrol;
                break;

            case AIState.Combat:
                if (dist > attackRange * 1.1f)
                    _aiState = AIState.Alert;
                break;
        }

        // --- DRIVE DROID STATE & BEHAVIOR ---
        switch (_aiState)
        {
            case AIState.Patrol:
                _droid.State = TGDroidStateManager.TDroidState.Idle;      // idle]patrol blend
                Patrol();
                _droid.Shooting = false;
                break;

            case AIState.Alert:
                _droid.State = TGDroidStateManager.TDroidState.Alarmed;   // alert color/sound
                Chase();
                _droid.Shooting = false;
                break;

            case AIState.Combat:
                _droid.State = TGDroidStateManager.TDroidState.Combat;

                Vector3 flatPlayer = _player.position;
                flatPlayer.y = transform.position.y;
                Vector3 dir = (flatPlayer - transform.position).normalized;
                float combatDist = Vector3.Distance(transform.position, flatPlayer);

                if (combatDist > attackRange)
                {
                    transform.position += dir * chaseSpeed * Time.deltaTime;
                }
                else if (combatDist < minCombatDistance)
                {
                    transform.position -= dir * chaseSpeed * Time.deltaTime;
                }

                Face(dir.x);

                // ƒVƒ…[ƒgŠÔŠu‚ðl—¶
                bool canShoot = (combatDist >= minCombatDistance && combatDist <= attackRange)
                    && (Time.time - _lastShootTime >= shootInterval);
                _droid.Shooting = canShoot;
                break;

        }
    }

    void Patrol()
    {
        Vector3 groundTarget = _currentPatrolTarget;
        groundTarget.y = transform.position.y;

        Vector3 dir = (groundTarget - transform.position).normalized;
        transform.position += dir * patrolSpeed * Time.deltaTime;

        if (Vector3.Distance(transform.position, groundTarget) < 0.2f)
            _currentPatrolTarget =
                _currentPatrolTarget == pointA.position
                ? pointB.position
                : pointA.position;

        Face(dir.x);
    }

    void Chase()
    {
        // Build a target thatfs on the same Y level as the droid
        Vector3 target = _player.position;
        target.y = transform.position.y;

        // Get horizontal direction only
        Vector3 dir = (target - transform.position).normalized;

        // Move along ground only
        transform.position += dir * chaseSpeed * Time.deltaTime;

        Face(dir.x);
    }

    void Face(float x)
    {
        if (x > 0.1f) transform.rotation = Quaternion.Euler(0, 90, 0);
        else if (x < -0.1f) transform.rotation = Quaternion.Euler(0, -90, 0);
    }

    private void OnDroidShoot(Transform origin)
    {
        _lastShootTime = Time.time;      // record shot time
        _droid.Shooting = false;         // immediately turn off Shooting flag
    }

    public void Stagger()
    {
        _isStaggered = true;
        _staggerTimer = staggerDuration;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, minCombatDistance);
    }

}
