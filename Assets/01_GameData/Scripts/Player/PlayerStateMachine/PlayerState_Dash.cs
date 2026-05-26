// プレイヤーがダッシュ中のときの状態を管理するステート
public class PlayerState_Dash : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Dash;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // keep buffering for post-attack movement etc.
        brain.Motor.InputMove(brain.Input.Move); // ダッシュ中も移動入力をバッファ

        // allow dash-jump behavior
        if (brain.Input.ConsumeJumpPressed()) brain.Motor.InputJumpPressed(); // ダッシュジャンプのためにジャンプ入力を通知
        if (brain.Input.ConsumeJumpReleased()) brain.Motor.InputJumpReleased();

        // optional: allow attack during dash
        if (brain.Input.ConsumeAttackPressed())
        {
            brain.ChangeState(PlayerStateID.DashAttack); // ダッシュ中の攻撃入力でDashAttackステートへ遷移
            return;
        }
        brain.Motor.MotorUpdate(true, true, true); // ジャンプ・モメンタム・壁スライド全て許可
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        // no horizontal movement while dash is active (dash uses its own velocity)
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false); // ダッシュ中は通常の水平移動制御を無効化（ダッシュ速度を使用）
    }
}
