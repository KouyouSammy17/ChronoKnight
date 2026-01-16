using UnityEngine;

public class PlayerState_DashAttack : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.DashAttack;

    public override void Enter(PlayerStateMachineBrain brain)
    {
        // stop dash so it doesn't keep pushing velocity
        brain.Motor.CancelDash();
        brain.Motor.StopHorizontalInstant();

        // start dash attack animation / logic
        brain.Combat?.StartDashAttack();
    }

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // allow buffering Ågchain into comboÅh while dash attack plays
        if (brain.Input.ConsumeAttackPressed())
        {
            brain.Combat?.RequestAttack(); // CombatController will treat this as "chain request" during dash attack
        }

        brain.Motor.MotorUpdate(true, true, true);

        // when dash attack ends, fall back to locomotion
        if (brain.Combat == null || !brain.Combat.IsDashAttackActive)
        {
            brain.ChangeState(brain.Motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne);
        }
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        // no input movement while dash-attacking
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false);
    }
}
