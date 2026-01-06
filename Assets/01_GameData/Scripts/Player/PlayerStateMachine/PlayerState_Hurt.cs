public class PlayerState_Hurt : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Hurt;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        brain.Motor.InputMove(brain.Input.Move);

        // buffer attack during stun (optional)
        if (brain.Input.ConsumeAttackPressed())
        {
            brain.Damage?.BufferAttack();
        }

        brain.Motor.MotorUpdate(false, false, false);

    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false);
    }
}
