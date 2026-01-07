public class PlayerState_Attack : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Attack;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // keep buffering move while input is locked (combat uses this)
        brain.Motor.InputMove(brain.Input.Move);

        // allow buffering further attacks during combo
        if (brain.Input.ConsumeAttackPressed())
        {
            brain.Combat?.RequestAttack();
        }

        brain.Motor.MotorUpdate(false, true, false); 
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false);
    }
}
