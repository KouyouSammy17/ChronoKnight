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

        if (active)
        {
            // If Turbo just started, capture the baseline buff from other systems
            if (!_wasTurboActive)
            {
                if (_combat != null)
                {
                    _baseBuff = _combat.AttackSpeedBuff;
                }
            }
            // Compute final buff as baseline * turbo compensation
            float comp = turbo.TurboComp;
            float finalBuff = _baseBuff * comp;
            if (_combat != null)
            {
                _combat.SetAttackSpeedBuff(finalBuff);
            }
            if (_playerAnim != null)
            {
                // Speed up all animations (locomotion/idle) only by the Turbo compensation
                _playerAnim.SetAttackSpeed(comp);
            }
        }
        else
        {
            if (_wasTurboActive)
            {
                // Turbo just ended: restore baseline buff and reset animator speed
                if (_combat != null)
                {
                    _combat.SetAttackSpeedBuff(_baseBuff);
                }
                if (_playerAnim != null)
                {
                    _playerAnim.SetAttackSpeed(1f);
                }
            }
            else
            {
                // Turbo inactive: check if the base buff changed due to momentum buffs
                if (_combat != null)
                {
                    float currentBuff = _combat.AttackSpeedBuff;
                    if (!Mathf.Approximately(currentBuff, _baseBuff))
                    {
                        _baseBuff = currentBuff;
                    }
                    // Ensure the combat buff is set to baseline
                    _combat.SetAttackSpeedBuff(_baseBuff);
                }
                // Keep animator speed at normal
                if (_playerAnim != null)
                {
                    _playerAnim.SetAttackSpeed(1f);
                }
            }
        }
        // Update the previous Turbo state tracker
        _wasTurboActive = active;
    }
}