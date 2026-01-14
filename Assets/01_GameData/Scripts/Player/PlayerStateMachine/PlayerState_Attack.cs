public class PlayerState_Attack : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Attack;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // keep buffering move while input is locked (combat uses this)
        brain.Motor.InputMove(brain.Input.Move);

        // allow buffering further attacks during combo
        if (brain.Input.ConsumeAttackPressed())
            brain.Combat?.RequestAttack();

        // Freeze in air while attacking
        bool shouldHang = !brain.Motor.IsGrounded; // Attack state only runs while combo is active
        brain.Motor.SetAirComboHang(shouldHang);

        brain.Motor.MotorUpdate(false, true, false);
    }
    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false);
    }
    public override void Exit(PlayerStateMachineBrain brain)
    {
        brain.Motor.SetAirComboHang(false);
    }

}
