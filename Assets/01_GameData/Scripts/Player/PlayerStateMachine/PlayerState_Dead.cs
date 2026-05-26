// プレイヤーが死亡状態のときの処理を管理するステート
using UnityEngine;

public class PlayerState_Dead : IPlayerState
{
    public PlayerStateID ID => PlayerStateID.Dead;

    public void Enter(PlayerStateMachineBrain brain)
    {
        // hard lock everything
        brain.Motor.DisableInput(); // 全ての入力をロック
        brain.Motor.StopHorizontalInstant(); // 水平移動を即時停止
        brain.Combat?.CancelCombo(); // 進行中のコンボをキャンセル

        // stop any dash / special movement (if you have a method)
        brain.Motor.CancelDash(); // ダッシュをキャンセル

        // play death anim
        var anim = brain.GetComponentInChildren<PlayerAnimator>();
        anim?.TriggerDie(); // 死亡アニメーションをトリガー
        anim?.SetDeadLoop(true); // optional bool 死亡ループアニメーションを開始
    }

    public void Tick(PlayerStateMachineBrain brain)
    {
        // do nothing (stay dead)
        // 死亡中は何も処理しない
    }

    public void FixedTick(PlayerStateMachineBrain brain)
    {
        // freeze horizontal drift forever
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false); // 死亡中は水平移動を永続的にロック
    }

    public void Exit(PlayerStateMachineBrain brain)
    {
        // usually never exits unless respawn/restart
        var anim = brain.GetComponentInChildren<PlayerAnimator>();
        anim?.SetDeadLoop(false); // リスポーン時に死亡ループアニメーションを解除
    }
}
