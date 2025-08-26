using UnityEngine;
using TGRobotsWheeled;    // for TGDroidStateManager
                          // (if your AI lives in another namespace, import that too)

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyStats : MonoBehaviour
{
    [SerializeField] private int _maxHP = 50;
    [SerializeField] private float _deathDelay = 1.5f;     // seconds to linger
    private int _currentHP;
    private Rigidbody _rb;

    private void Awake()
    {
        _currentHP = _maxHP;
        _rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Call this when you want to deal damage. When HP hits zero DIE.
    /// </summary>
    public void TakeDamage(int dmg)
    {
        _currentHP = Mathf.Max(_currentHP - dmg, 0);
        Debug.Log($"Enemy took {dmg} damage, HP now {_currentHP}");

        // trigger a brief stagger on the AI
        var ai = GetComponent<SciFiRobotAI>();
        if (ai != null)
            ai.Stagger();

        if (_currentHP == 0)
            Die();
    }

    private void Die()
    {
        // 1) Switch the asset�fs state machine into �gSleep�h
        var droidSM = GetComponent<TGDroidStateManager>();
        if (droidSM != null)
            droidSM.State = TGDroidStateManager.TDroidState.Sleep;

        // 2) Stop chasing/firing
        var ai = GetComponent<SciFiRobotAI>();
        if (ai != null)
            ai.enabled = false;

        // 3) Freeze physics & disable collider
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }
        var col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        // 4) Finally destroy after a short delay
        Destroy(gameObject, _deathDelay);
    }
}
