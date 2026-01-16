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
        // IMPORTANT: allow physics velocity so knockback isnÅft erased
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: true);
    }
}
