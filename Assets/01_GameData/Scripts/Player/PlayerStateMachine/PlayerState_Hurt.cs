// プレイヤーがダメージを受けてヒットスタン中のときの状態を管理するステート
public class PlayerState_Hurt : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Hurt;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        //brain.Motor.InputMove(brain.Input.Move); // ヒットスタン中は移動入力を受け付けない（無効化済み）

        brain.Motor.MotorUpdate(false, false, false); // ジャンプ・モメンタム・壁スライド全て禁止

    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: true); // ノックバックの水平移動は許可する
    }
}
