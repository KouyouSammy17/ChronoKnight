using UnityEngine;

/// <summary>
/// Responsible for applying Turbo Mode scaling to combat animations. This decouples
/// attack speed changes from TurboModeManager so that combat animation speed is
/// managed by a dedicated combat manager script. When Turbo Mode is active it
/// multiplies the attack speed buff on CombatController and the animation speed
/// on PlayerAnimator by the Turbo compensation factor. When Turbo ends it
/// restores both to their normal values.
/// </summary>
[RequireComponent(typeof(CombatController))]
public class CombatTurboManager : MonoBehaviour
{
    private CombatController _combat;
    private PlayerAnimator _playerAnim;

    // Tracks the baseline attack speed buff provided by other systems (e.g. momentum buffs).
    // When Turbo Mode is active, we multiply this by the Turbo compensation factor.
    private float _baseBuff = 1f;
    // Remember whether Turbo Mode was active on the previous frame. Used to detect
    // when Turbo just started or just ended.
    private bool _wasTurboActive = false;

    private void Awake()
    {
        // Cache combat controller on the same GameObject
        _combat = GetComponent<CombatController>();

        // Try to get the PlayerAnimator from this GameObject or its children
        _playerAnim = GetComponent<PlayerAnimator>();
        if (_playerAnim == null)
        {
            _playerAnim = GetComponentInChildren<PlayerAnimator>();
        }
    }

    private void Update()
    {
        // Grab TurboModeManager (may be null if Turbo isn't configured)
        var turbo = TurboModeManager.Instance;
        bool active = (turbo != null && turbo.IsActive);
        // Determine compensation factor. When inactive, comp = 1.
        float comp = (active && turbo != null) ? turbo.TurboComp : 1f;

        // Fetch the current attack speed buff from the combat controller.
        // If CombatController is missing (shouldn't happen with RequireComponent), skip.
        if (_combat != null)
        {
            float currentBuff = _combat.AttackSpeedBuff;
            // Derive the underlying base buff. When Turbo is active the controller's
            // AttackSpeedBuff has been multiplied by comp. When inactive it is just
            // the base buff. Divide by comp to recover the base value in both cases.
            float estimatedBase = currentBuff / comp;
            if (!Mathf.Approximately(estimatedBase, _baseBuff))
            {
                _baseBuff = estimatedBase;
            }

            // Compute the final buff to apply: base buff times comp if Turbo is active,
            // otherwise just the base buff. This ensures momentum buffs stack with Turbo
            // rather than being overwritten.
            float finalBuff = active ? _baseBuff * comp : _baseBuff;
            _combat.SetAttackSpeedBuff(finalBuff);
        }

        // Adjust the animator speed. All animations should be sped up by comp while
        // Turbo is active and reset to normal otherwise. Note: SetAttackSpeed
        // multiplies the Animator.speed property on PlayerAnimator; using it here
        // ensures locomotion/idle/attack states all scale consistently.
        if (_playerAnim != null)
        {
            float animSpeed = active ? comp : 1f;
            _playerAnim.SetAttackSpeed(animSpeed);
        }

        // Track the previous Turbo state. Currently unused but retained for potential
        // future logic (e.g., one-shot events on entering/exiting Turbo).
        _wasTurboActive = active;
    }
}