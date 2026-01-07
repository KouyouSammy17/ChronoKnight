public class PlayerState_WallSlide : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.WallSlide;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // keep buffering movement
        brain.Motor.InputMove(brain.Input.Move);

        // allow wall jump via motor's jump logic
        if (brain.Input.ConsumeJumpPressed()) brain.Motor.InputJumpPressed();
        if (brain.Input.ConsumeJumpReleased()) brain.Motor.InputJumpReleased();

        // dash escape
        if (brain.Input.ConsumeDashPressed())
        {
            if (brain.Motor.TryStartDash(brain.Input.Move))
            {
                brain.ChangeState(PlayerStateID.Dash);
                return;
            }
        }

        if (brain.Input.ConsumeAttackPressed())
            brain.Combat?.RequestAttack();

        brain.Motor.MotorUpdate(allowJump: true, allowMomentumGain: false, allowWallSlide: true);
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        // let horizontal movement happen (you can drift away from wall)
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: true);
    }
}
