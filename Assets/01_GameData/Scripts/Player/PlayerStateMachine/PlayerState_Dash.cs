public class PlayerState_Dash : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Dash;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // keep buffering for post-attack movement etc.
        brain.Motor.InputMove(brain.Input.Move);

        // allow dash-jump behavior
        if (brain.Input.ConsumeJumpPressed()) brain.Motor.InputJumpPressed();
        if (brain.Input.ConsumeJumpReleased()) brain.Motor.InputJumpReleased();

        // optional: allow attack during dash
        if (brain.Input.ConsumeAttackPressed())
        {
            brain.Combat?.RequestAttack();
        }
        brain.Motor.MotorUpdate(true, true, true);
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        // no horizontal movement while dash is active (dash uses its own velocity)
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false);
    }
}
