// ターボモード中の戦闘アニメーション速度をプレイヤーアニメーターに反映するスクリプト
using UnityEngine;

/// <summary>
/// ターボモードのスケーリングを戦闘アニメーションに適用する役割を持つ。
/// 攻撃速度の変更をTurboModeManagerから切り離し、専用の戦闘マネージャースクリプトで管理する。
/// ターボモードがアクティブな間は、CombatControllerの攻撃速度バフと
/// PlayerAnimatorのアニメーション速度にターボ補正係数を乗算する。
/// ターボが終了すると、両方を通常の値に戻す。
/// </summary>
[RequireComponent(typeof(CombatController))]
public class CombatTurboManager : MonoBehaviour
{
    private CombatController _combat;    // 同じオブジェクトのCombatControllerへの参照
    private PlayerAnimator _playerAnim;  // アニメーション速度を制御するPlayerAnimator

    private void Awake()
    {
        _combat = GetComponent<CombatController>();
        // このGameObjectまたはその子からPlayerAnimatorを取得する
        _playerAnim = GetComponent<PlayerAnimator>() ?? GetComponentInChildren<PlayerAnimator>();

    }

    private void Update()
    {
        // ターボ補正係数を算出する。ターボが非アクティブなら comp = 1
        var turbo = TurboModeManager.Instance;

        // ターボ中はアニメーターがUnscaledTimeを使うため、リアルタイム倍率を直接設定する：
        // ターボ中はリアルタイム倍率を適用し、非アクティブ時は1倍（通常速度）に戻す
        float comp = (turbo != null && turbo.IsActive) ? turbo.OtherAnimComp : 1f;

        if (_playerAnim == null) return;

        // コンボ実行中でなければロコモーション用の速度を設定する
        bool inCombo = (_combat != null) && _combat.IsComboActive;
        if (!inCombo)
        {
            // 移動・アイドルアニメーション = リアルタイム1.1倍
            _playerAnim.SetAttackSpeed(comp);
        }
    }
}
