// プレイヤーが死亡状態のときの処理を管理するステート
using UnityEngine;

public class PlayerState_Dead : IPlayerState
{
    public PlayerStateID ID => PlayerStateID.Dead;

    public void Enter(PlayerStateMachineBrain brain)
    {
        // 全てを完全にロック
        brain.Motor.DisableInput(); // 全ての入力をロック
        brain.Motor.StopHorizontalInstant(); // 水平移動を即時停止
        brain.Combat?.CancelCombo(); // 進行中のコンボをキャンセル

        // ダッシュや特殊な移動を停止する
        brain.Motor.CancelDash(); // ダッシュをキャンセル

        // 死亡アニメーションを再生する
        var anim = brain.GetComponentInChildren<PlayerAnimator>();
        anim?.TriggerDie(); // 死亡アニメーションをトリガー
        anim?.SetDeadLoop(true); // 任意のbool：死亡ループアニメーションを開始
    }

    public void Tick(PlayerStateMachineBrain brain)
    {
        // 何もしない（死亡状態を維持）
    }

    public void FixedTick(PlayerStateMachineBrain brain)
    {
        // 水平ドリフトを永続的にフリーズする
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false); // 死亡中は水平移動を永続的にロック
    }

    public void Exit(PlayerStateMachineBrain brain)
    {
        // リスポーン/リスタート以外では通常終了しない
        var anim = brain.GetComponentInChildren<PlayerAnimator>();
        anim?.SetDeadLoop(false); // リスポーン時に死亡ループアニメーションを解除
    }
}
