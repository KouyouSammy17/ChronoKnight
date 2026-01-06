public class PlayerState_Dead : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Dead;

    public override void Enter(PlayerStateMachineBrain brain)
    {
        brain.Motor?.DisableInput();
    }

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // still tick motor for gravity/physics if you want:
        brain.Motor.MotorUpdate(false, false, false);

    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false);
    }
}
