using UnityEngine;

/// <summary>
/// High‑speed moving platform that becomes rideable when time is slowed.
/// Uses kinematic physics for stable interaction and top‑contact detection.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class SuperFastPlatform : MonoBehaviour
{
    [Header("Movement Points")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Timing")]
    [Tooltip("Seconds to travel one way (smaller = faster)")]
    [SerializeField] private float travelTime = 0.7f;
    [Tooltip("Optional pause at each end")]
    [SerializeField] private float pauseAtEnds = 0f;

    [Header("Speed Curve")]
    [Tooltip("Optional easing curve (0..1 → 0..1). Leave empty for linear motion.")]
    [SerializeField] private AnimationCurve easing;

    private Rigidbody rb;
    private Vector3 posA, posB;
    private float t;           // normalized position along the path
    private int direction = +1;
    private float pauseTimer;
    public Vector3 PlatformVelocity { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;  // kinematic rigidbody avoids external forces:contentReference[oaicite:2]{index=2}
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        if (!pointA || !pointB)
        {
            //Debug.LogError("Missing A/B points");
            enabled = false;
            return;
        }
        posA = pointA.position;
        posB = pointB.position;
        transform.position = posA;
    }

    private void FixedUpdate()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;
            PlatformVelocity = Vector3.zero;
            return;
        }

        // Advance normalized time; smaller travelTime -> faster motion.
        float dtNorm = Time.fixedDeltaTime / Mathf.Max(travelTime, 0.001f);
        t = Mathf.Clamp01(t + dtNorm * direction);

        // Evaluate easing (if provided)
        float easedT = (easing != null && easing.keys.Length > 0) ? easing.Evaluate(t) : t;

        // Compute target position and velocity
        Vector3 target = Vector3.Lerp(posA, posB, easedT);
        PlatformVelocity = (target - rb.position) / Time.fixedDeltaTime;

        // Move the kinematic rigidbody
        rb.MovePosition(target);

        // Reverse direction at ends and optionally pause
        if (t <= 0f || t >= 1f)
        {
            direction *= -1;
            if (pauseAtEnds > 0f) pauseTimer = pauseAtEnds;
        }
    }

    // Draw the platform’s path in the editor
    private void OnDrawGizmos()
    {
        if (pointA && pointB)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pointA.position, pointB.position);
            Gizmos.DrawSphere(pointA.position, 0.08f);
            Gizmos.DrawSphere(pointB.position, 0.08f);
        }
    }
}
