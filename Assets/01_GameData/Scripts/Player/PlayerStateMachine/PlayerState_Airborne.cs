// プレイヤーが空中にいる（ジャンプ・落下中）ときの状態を管理するステート
public class PlayerState_Airborne : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Airborne;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        brain.Motor.InputMove(brain.Input.Move); // 空中でも移動入力を送信

        if (brain.Input.ConsumeJumpPressed()) brain.Motor.InputJumpPressed(); // 二段ジャンプなどのためにジャンプ入力を通知
        if (brain.Input.ConsumeJumpReleased()) brain.Motor.InputJumpReleased(); // ショートホップ用にジャンプ離しを通知

        if (brain.Input.ConsumeDashPressed())
        {
            if (brain.Motor.TryStartDash(brain.Input.Move))
            {
                brain.ChangeState(PlayerStateID.Dash); // エアダッシュ成功時にDashステートへ遷移
                return;
            }
        }

        if (brain.Input.ConsumeAttackPressed())
        {
            brain.Combat?.RequestAttack(); // 空中攻撃を戦闘コントローラーに通知
        }

        brain.Motor.MotorUpdate(true, false, true); // ジャンプ許可・モメンタム獲得禁止・壁スライド許可
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: true); // 空中でも水平移動を許可
    }
}
