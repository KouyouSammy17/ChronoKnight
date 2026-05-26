// ターボモード中の戦闘アニメーション速度をプレイヤーアニメーターに反映するスクリプト
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
    private CombatController _combat;    // 同じオブジェクトのCombatControllerへの参照
    private PlayerAnimator _playerAnim;  // アニメーション速度を制御するPlayerAnimator

    private void Awake()
    {
        _combat = GetComponent<CombatController>();
        // Try to get the PlayerAnimator from this GameObject or its children
        _playerAnim = GetComponent<PlayerAnimator>() ?? GetComponentInChildren<PlayerAnimator>();

    }

    private void Update()
    {
        // Determine the Turbo compensation factor.  When Turbo is inactive, comp = 1.
        var turbo = TurboModeManager.Instance;

        // Animator is UnscaledTime during Turbo, so we set REAL-TIME multipliers directly:
        // ターボ中はリアルタイム倍率を適用し、非アクティブ時は1倍（通常速度）に戻す
        float comp = (turbo != null && turbo.IsActive) ? turbo.OtherAnimComp : 1f;

        if (_playerAnim == null) return;

        // コンボ実行中でなければロコモーション用の速度を設定する
        bool inCombo = (_combat != null) && _combat.IsComboActive;
        if (!inCombo)
        {
            // Locomotion/idle animations = 1.1x real-time
            _playerAnim.SetAttackSpeed(comp);
        }
    }
}
