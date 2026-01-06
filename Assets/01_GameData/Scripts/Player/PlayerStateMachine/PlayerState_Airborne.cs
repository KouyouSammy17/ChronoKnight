public class PlayerState_Airborne : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Airborne;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        brain.Motor.InputMove(brain.Input.Move);

        if (brain.Input.ConsumeJumpPressed()) brain.Motor.InputJumpPressed();
        if (brain.Input.ConsumeJumpReleased()) brain.Motor.InputJumpReleased();

        if (brain.Input.ConsumeDashPressed())
        {
            if (brain.Motor.TryStartDash(brain.Input.Move))
            {
                brain.ChangeState(PlayerStateID.Dash);
                return;
            }
        }

        if (brain.Input.ConsumeAttackPressed())
        {
            brain.Combat?.RequestAttack();
        }

        brain.Motor.MotorUpdate(true, false, true);
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: true);
    }
}
