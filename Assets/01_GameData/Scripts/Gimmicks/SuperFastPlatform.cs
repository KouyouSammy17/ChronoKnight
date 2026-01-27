using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class SuperFastPlatform : MonoBehaviour
{
    [Header("Movement Points")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Timing")]
    [SerializeField] private float travelTime = 0.7f;
    [SerializeField] private float pauseAtEnds = 0f;

    [Header("Speed Curve")]
    [SerializeField] private AnimationCurve easing;

    private Rigidbody rb;
    private Vector3 posA, posB;
    private float t;
    private int direction = +1;
    private float pauseTimer;

    public Vector3 PlatformVelocity { get; private set; }
    public Vector3 PlatformDelta { get; private set; }   // ✅ NEW: per-fixed-step displacement

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        //Helps fast kinematic bodies not miss contacts
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void Start()
    {
        if (!pointA || !pointB) { enabled = false; return; }
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
            PlatformDelta = Vector3.zero;
            return;
        }

        Vector3 prevPos = rb.position;

        float dtNorm = Time.fixedDeltaTime / Mathf.Max(travelTime, 0.001f);
        t = Mathf.Clamp01(t + dtNorm * direction);

        float easedT = (easing != null && easing.keys.Length > 0) ? easing.Evaluate(t) : t;

        Vector3 target = Vector3.Lerp(posA, posB, easedT);

        PlatformDelta = target - prevPos;
        PlatformVelocity = PlatformDelta / Time.fixedDeltaTime;

        rb.MovePosition(target);

        if (t <= 0f || t >= 1f)
        {
            direction *= -1;
            if (pauseAtEnds > 0f) pauseTimer = pauseAtEnds;
        }
    }
}
