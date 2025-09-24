using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlatformRider : MonoBehaviour
{
    private Rigidbody _rb;
    private SuperFastPlatform _platformUnderFeet;
    [SerializeField] private float topContactDot = 0.75f;

    // Optional: small smoothing if platforms are ultra-fast
    [SerializeField] private float velocityBlend = 1f; // 1 = full add, 0.5 = half add, etc.

    public bool HasPlatform => _platformUnderFeet != null;
    public Vector3 CurrentPlatformVelocity { get; private set; }  // expose
    private void Awake() => _rb = GetComponent<Rigidbody>();

    private void OnCollisionStay(Collision c)
    {
        // Detect top contact with a SuperFastPlatform
        if (c.collider.TryGetComponent(out SuperFastPlatform plat))
        {
            foreach (var cp in c.contacts)
            {
                if (Vector3.Dot(cp.normal, Vector3.up) > topContactDot)
                {
                    _platformUnderFeet = plat;
                    return;
                }
            }
        }

        // If weÅfre here, this contact wasnÅft valid Ågtop contactÅh
        // DonÅft clear here; let OnCollisionExit clear to avoid flicker if there are multiple colliders.
    }

    private void OnCollisionExit(Collision c)
    {
        if (_platformUnderFeet != null && c.collider.GetComponent<SuperFastPlatform>() == _platformUnderFeet)
            _platformUnderFeet = null;
    }

    private void FixedUpdate()
    {
        if (_platformUnderFeet == null)
        {
            CurrentPlatformVelocity = Vector3.zero;
            return;
        }

        Vector3 v = _rb.linearVelocity;
        Vector3 pv = _platformUnderFeet.PlatformVelocity;

        // cache exactly what weÅfre adding (horizontal by default)
        CurrentPlatformVelocity = new Vector3(pv.x * velocityBlend, 0f, pv.z * velocityBlend);

        v.x += CurrentPlatformVelocity.x;
        v.z += CurrentPlatformVelocity.z;
        _rb.linearVelocity = v;
    }
}
