using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class RobotBullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public int Damage = 20;       // how much HP to take
    public float Speed = 15f;      // travel speed
    public float LifeTime = 5f;       // auto-destroy after this many seconds

    private Rigidbody _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;      // bullets fly straight
        _rb.isKinematic = false;
        _rb.linearVelocity = transform.forward * Speed;

        // destroy after Lifetime so stray bullets donÅft pile up
        Destroy(gameObject, LifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1) Hit the player?
        if (other.CompareTag("Player"))
        {
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null)
                stats.TakeDamage(Damage);   // invokes onHealthChanged, GameOver at zero HP :contentReference[oaicite:5]{index=5}

            Destroy(gameObject);
            return;
        }

        // 2) Hitting ÅgsolidÅh world geometry (e.g. your Ground or Walls)
        //    Make sure those colliders have a specific layer like ÅgEnvironmentÅh
        if (other.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            Destroy(gameObject);
        }

    }
}
