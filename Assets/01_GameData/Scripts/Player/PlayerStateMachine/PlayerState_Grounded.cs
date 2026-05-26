// プレイヤーが地上にいる（静止・移動）ときの状態を管理するステート
public class PlayerState_Grounded : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Grounded;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // always feed move to motor to keep buffering (even during locks)
        brain.Motor.InputMove(brain.Input.Move); // 移動入力を常にモーターへ送信（バッファ維持のため）

        if (brain.Input.ConsumeJumpPressed()) brain.Motor.InputJumpPressed(); // ジャンプ押下をモーターへ通知
        if (brain.Input.ConsumeJumpReleased()) brain.Motor.InputJumpReleased(); // ジャンプ離しをモーターへ通知

        if (brain.Input.ConsumeDashPressed())
        {
            if (brain.Motor.TryStartDash(brain.Input.Move))
            {
                brain.ChangeState(PlayerStateID.Dash); // ダッシュ成功時にDashステートへ遷移
                return;
            }
        }

        if (brain.Input.ConsumeAttackPressed())
        {
            brain.Combat?.RequestAttack(); // 攻撃入力を戦闘コントローラーに通知
        }

        brain.Motor.MotorUpdate(true, true, false); // ジャンプ許可・モメンタム獲得許可・壁スライド禁止
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: true); // 水平移動を許可して物理更新
    }
}
