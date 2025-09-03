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

    // No extra state is required. Attack speed buffing and Turbo compensation
    // are applied directly in CombatController.  This manager only scales the
    // animator speed when not in a combo, to make idle/locomotion animations
    // reflect Turbo slow/fast time.

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
        // Determine the Turbo compensation factor.  When Turbo is inactive, comp = 1.
        var turbo = TurboModeManager.Instance;
        float comp = (turbo != null && turbo.IsActive) ? turbo.TurboComp : 1f;

        // Adjust the animator speed only when not currently in a combo.  During combos
        // the CombatController sets the animator speed per attack step, including
        // momentum buffs and Turbo compensation.  Overriding it here would
        // interfere with combo timing.  Outside of combos, we scale the animator
        // so idle and locomotion animations reflect Turbo slow/fast time.
        if (_playerAnim != null)
        {
            bool inCombo = false;
            if (_combat != null)
            {
                // Use the IsComboActive property if available.  Avoid calling an
                // extension method or reflection to prevent compile errors when
                // IsComboActive() does not exist.  The property will return
                // true when a combo is currently playing.
                try
                {
                    inCombo = _combat.IsComboActive;
                }
                catch
                {
                    inCombo = false;
                }
            }
            if (!inCombo)
            {
                _playerAnim.SetAttackSpeed(comp);
            }
        }
    }
}