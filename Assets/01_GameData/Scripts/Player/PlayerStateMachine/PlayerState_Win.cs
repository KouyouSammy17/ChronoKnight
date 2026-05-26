// ステージクリア（勝利）後のプレイヤーをロックするステート
using UnityEngine;

public class PlayerState_Win : IPlayerState
{
    public PlayerStateID ID => PlayerStateID.Win;

    public void Enter(PlayerStateMachineBrain brain)
    {
        // hard lock gameplay
        brain.Motor?.DisableInput(); // 全入力をロック
        brain.Motor?.CancelDash(); // ダッシュをキャンセル
        brain.Motor?.StopHorizontalInstant(); // 水平移動を即時停止
        brain.Motor?.SetAirComboHang(false); // 空中コンボハングを解除
        brain.Motor?.SetHitReactLock(true); // prevents hit logic overwriting motion ヒットリアクションが動きを上書きしないようにロック
        brain.Motor?.SetFrozen(true);       // freeze rigidbody (pose) Rigidbodyを完全にフリーズ（ポーズ）

    }

    public void Exit(PlayerStateMachineBrain brain)
    {
        // leave frozen unlock to GameManager after results if needed,
        // but if you ever exit Win back to gameplay:
        brain.Motor?.SetFrozen(false); // フリーズを解除（リザルト後など）
        brain.Motor?.SetHitReactLock(false); // ヒットリアクションロックを解除
        brain.Motor?.EnableInput(); // 入力を再開
    }

    public void Tick(PlayerStateMachineBrain brain) { } // 勝利中は更新処理なし
    public void FixedTick(PlayerStateMachineBrain brain) { } // 勝利中は物理更新処理なし
}
