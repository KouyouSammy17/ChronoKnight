using UnityEngine;

/// <summary>
/// Handles the transfer of velocity from moving platforms to the player.
/// Detects when player is standing on a moving platform and applies its velocity.
/// Can be gated to only work during Turbo mode.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlatformCarryInjector : MonoBehaviour
{
    /// <summary>Reference to the player's motor component</summary>
    [Header("Refs")]
    [SerializeField] private PlayerMotor _motor;

    /// <summary>Dot product threshold to detect top surface contact (should be standing on top)</summary>
    [Header("Top contact filter")]
    [SerializeField] private float _topContactDot = 0.75f;

    /// <summary>If enabled, platform carry only applies while Turbo mode is active</summary>
    [Header("Turbo gate")]
    [SerializeField] private bool _onlyInTurbo = true;

    /// <summary>Reference to the platform currently carrying the player</summary>
    private SuperFastPlatform _platform;
    /// <summary>Whether top contact with a platform was detected this physics frame</summary>
    private bool _topContactThisStep;

    /// <summary>Checks if Turbo mode is currently active</summary>
    private bool TurboActive =>
        TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;

    /// <summary>Initializes motor reference if not assigned</summary>
    private void Awake()
    {
        if (_motor == null) _motor = GetComponent<PlayerMotor>();
    }

    /// <summary>
    /// Detects collision with moving platforms from above.
    /// Updates platform reference and tracks top contact status.
    /// </summary>
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

    /// <summary>Detaches from platform when collision ends</summary>
    private void OnCollisionExit(Collision c)
    {
        DetachIfThisPlatform(c);
    }

    /// <summary>
    /// Applies platform velocity to the player each physics frame.
    /// Converts platform displacement delta into velocity in horizontal plane only (2.5D).
    /// </summary>
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
        // PlatformDelta is computed in platform FixedUpdate.
        Vector3 carryVel = _platform.PlatformDelta / Time.fixedDeltaTime;

        // 2.5D: carry only X/Z
        carryVel.y = 0f;

        _motor.SetPlatformCarryVelocity(carryVel);
    }

    /// <summary>
    /// Detaches from the current platform if the given collision is with that platform.
    /// </summary>
    private void DetachIfThisPlatform(Collision c)
    {
        if (_platform != null && c.collider.GetComponent<SuperFastPlatform>() == _platform)
        {
            _platform = null;
            if (_motor != null) _motor.ClearPlatformCarryVelocity();
        }
    }
}
