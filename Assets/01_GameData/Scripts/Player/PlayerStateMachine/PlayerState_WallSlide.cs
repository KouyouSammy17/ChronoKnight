// プレイヤーが壁をスライドしているときの状態を管理するステート
public class PlayerState_WallSlide : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.WallSlide;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // keep buffering movement
        brain.Motor.InputMove(brain.Input.Move); // 移動入力をバッファし続ける

        // allow wall jump via motor's jump logic
        if (brain.Input.ConsumeJumpPressed()) brain.Motor.InputJumpPressed(); // 壁ジャンプのためにジャンプ入力を通知
        if (brain.Input.ConsumeJumpReleased()) brain.Motor.InputJumpReleased();

        // dash escape
        if (brain.Input.ConsumeDashPressed())
        {
            if (brain.Motor.TryStartDash(brain.Input.Move))
            {
                brain.ChangeState(PlayerStateID.Dash); // ダッシュで壁から逃げる
                return;
            }
        }

        if (brain.Input.ConsumeAttackPressed())
            brain.Combat?.RequestAttack(); // 壁スライド中の攻撃入力を通知

        brain.Motor.MotorUpdate(allowJump: true, allowMomentumGain: false, allowWallSlide: true); // ジャンプ許可・壁スライド許可
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        // let horizontal movement happen (you can drift away from wall)
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: true); // 壁から離れる水平移動を許可
    }
}
