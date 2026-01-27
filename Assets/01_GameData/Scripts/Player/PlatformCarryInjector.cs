using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlatformCarryInjector : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerMotor _motor;

    [Header("Top contact filter")]
    [SerializeField] private float _topContactDot = 0.75f;

    [Header("Turbo gate")]
    [SerializeField] private bool _onlyInTurbo = true;

    private SuperFastPlatform _platform;
    private bool _topContactThisStep;

    private bool TurboActive =>
        TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;

    private void Awake()
    {
        if (_motor == null) _motor = GetComponent<PlayerMotor>();
    }

    private void OnCollisionStay(Collision c)
    {
        _topContactThisStep = false;

        if (_onlyInTurbo && !TurboActive)
        {
            DetachIfThisPlatform(c);
            return;
        }

        if (c.collider.TryGetComponent(out SuperFastPlatform plat))
        {
            foreach (var cp in c.contacts)
            {
                if (Vector3.Dot(cp.normal, Vector3.up) > _topContactDot)
                {
                    _platform = plat;
                    _topContactThisStep = true;
                    return;
                }
            }
        }
    }

    private void OnCollisionExit(Collision c)
    {
        DetachIfThisPlatform(c);
    }

    private void FixedUpdate()
    {
        if (_motor == null) return;

        // turbo ended => stop carry
        if (_onlyInTurbo && !TurboActive)
        {
            _platform = null;
            _motor.ClearPlatformCarryVelocity();
            return;
        }

        if (_platform == null || !_topContactThisStep)
        {
            _platform = null;
            _motor.ClearPlatformCarryVelocity();
            return;
        }

        // Convert platform displacement to velocity (no accumulation!)
        // PlatformDelta is computed in platform FixedUpdate. :contentReference[oaicite:2]{index=2}
        Vector3 carryVel = _platform.PlatformDelta / Time.fixedDeltaTime;

        // 2.5D: carry only X/Z
        carryVel.y = 0f;

        _motor.SetPlatformCarryVelocity(carryVel);
    }

    private void DetachIfThisPlatform(Collision c)
    {
        if (_platform != null && c.collider.GetComponent<SuperFastPlatform>() == _platform)
        {
            _platform = null;
            if (_motor != null) _motor.ClearPlatformCarryVelocity();
        }
    }
}
