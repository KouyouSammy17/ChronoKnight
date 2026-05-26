// プレイヤーがダッシュ攻撃を実行しているときの状態を管理するステート
using UnityEngine;

public class PlayerState_DashAttack : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.DashAttack;

    public override void Enter(PlayerStateMachineBrain brain)
    {
        // stop dash so it doesn't keep pushing velocity
        brain.Motor.CancelDash(); // ダッシュをキャンセルして速度が持続しないようにする
        brain.Motor.StopHorizontalInstant(); // 水平速度を即時停止

        // start dash attack animation / logic
        brain.Combat?.StartDashAttack(); // ダッシュ攻撃を開始
    }

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // allow buffering �gchain into combo�h while dash attack plays
        if (brain.Input.ConsumeAttackPressed())
        {
            brain.Combat?.RequestAttack(); // CombatController will treat this as "chain request" during dash attack
        }

        brain.Motor.MotorUpdate(true, true, true); // ジャンプ・モメンタム・壁スライド全て許可

        // when dash attack ends, fall back to locomotion
        if (brain.Combat == null || !brain.Combat.IsDashAttackActive)
        {
            brain.ChangeState(brain.Motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne); // ダッシュ攻撃終了後に移動ステートへ戻る
        }
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        // no input movement while dash-attacking
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false); // ダッシュ攻撃中は水平移動入力を無効化
    }
}
