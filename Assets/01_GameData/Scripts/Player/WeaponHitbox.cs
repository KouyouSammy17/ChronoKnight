using MoreMountains.Feedbacks;
using UnityEngine;

public class WeaponHitbox : MonoBehaviour
{
    [HideInInspector] public int Damage;
    [HideInInspector] public float MomentumGain = 0f;

    private Collider _collider;

    [Header("Hit Feedback (FEEL)")]
    [SerializeField] private MMFeedbacks _enemyHitFeedback;


    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.enabled = false; // default: off
    }
    /// <summary>
    /// Call before the hit‐window opens. Assigns damage & momentum, then enables the collider.
    /// </summary>
    public void EnableHitbox(int damage, float momentumGain)
    {
        Damage = damage;
        MomentumGain = momentumGain;
        _collider.enabled = true;
    }

    /// <summary>
    /// Simple disable—use when the window closes.
    /// </summary>
    public void DisableHitbox()
    {
        _collider.enabled = false;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            var enemyStats = other.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                enemyStats.TakeDamage(Damage);

                // ADD MOMENTUM ON HIT
                MomentumManager.Instance.AddMomentum(MomentumGain);
                // ▶ FEEL Feedback!
                _enemyHitFeedback?.PlayFeedbacks();
            }
        }
    }
}
