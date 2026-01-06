public class PlayerState_Grounded : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Grounded;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // always feed move to motor to keep buffering (even during locks)
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

        brain.Motor.MotorUpdate(true, true, false);
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: true);
    }
}
