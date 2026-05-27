// ステージクリア（勝利）後のプレイヤーをロックするステート
using UnityEngine;

public class PlayerState_Win : IPlayerState
{
    public PlayerStateID ID => PlayerStateID.Win;

    public void Enter(PlayerStateMachineBrain brain)
    {
        // ゲームプレイを完全にロック
        brain.Motor?.DisableInput(); // 全入力をロック
        brain.Motor?.CancelDash(); // ダッシュをキャンセル
        brain.Motor?.StopHorizontalInstant(); // 水平移動を即時停止
        brain.Motor?.SetAirComboHang(false); // 空中コンボハングを解除
        brain.Motor?.SetHitReactLock(true); // ヒットロジックが動作を上書きしないようにロック
        brain.Motor?.SetFrozen(true);       // Rigidbodyを完全にフリーズ（ポーズ）

    }

    public void Exit(PlayerStateMachineBrain brain)
    {
        // フリーズ解除は必要に応じてリザルト後にGameManagerへ委ねる。
        // ただし、Winからゲームプレイに戻る場合は：
        brain.Motor?.SetFrozen(false); // フリーズを解除（リザルト後など）
        brain.Motor?.SetHitReactLock(false); // ヒットリアクションロックを解除
        brain.Motor?.EnableInput(); // 入力を再開
    }

    public void Tick(PlayerStateMachineBrain brain) { } // 勝利中は更新処理なし
    public void FixedTick(PlayerStateMachineBrain brain) { } // 勝利中は物理更新処理なし
}
