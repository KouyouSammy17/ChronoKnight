using UnityEngine;

public class PlayerState_Dead : IPlayerState
{
    public PlayerStateID ID => PlayerStateID.Dead;

    public void Enter(PlayerStateMachineBrain brain)
    {
        // hard lock everything
        brain.Motor.DisableInput();
        brain.Motor.StopHorizontalInstant();
        brain.Combat?.CancelCombo();

        // stop any dash / special movement (if you have a method)
        brain.Motor.CancelDash();

        // play death anim
        var anim = brain.GetComponentInChildren<PlayerAnimator>();
        anim?.TriggerDie();
        anim?.SetDeadLoop(true); // optional bool
    }

    public void Tick(PlayerStateMachineBrain brain)
    {
        // do nothing (stay dead)
    }

    public void FixedTick(PlayerStateMachineBrain brain)
    {
        // freeze horizontal drift forever
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false);
    }

    public void Exit(PlayerStateMachineBrain brain)
    {
        // usually never exits unless respawn/restart
        var anim = brain.GetComponentInChildren<PlayerAnimator>();
        anim?.SetDeadLoop(false);
    }
}
