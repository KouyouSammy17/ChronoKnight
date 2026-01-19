public class PlayerState_Knockdown : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Knockdown;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // no input buffering here (youÅfre ÅgdownÅh)
        brain.Motor.MotorUpdate(false, false, false);
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        // allow knockback while airborne, but stop sliding once grounded
        bool allow = !brain.Motor.IsGrounded;
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: allow);
    }
}
