using UnityEngine;

public class PlayerState_Win : IPlayerState
{
    public PlayerStateID ID => PlayerStateID.Win;

    public void Enter(PlayerStateMachineBrain brain)
    {
        // hard lock gameplay
        brain.Motor?.DisableInput();
        brain.Motor?.CancelDash();
        brain.Motor?.StopHorizontalInstant();
        brain.Motor?.SetAirComboHang(false);
        brain.Motor?.SetHitReactLock(true); // prevents hit logic overwriting motion
        brain.Motor?.SetFrozen(true);       // freeze rigidbody (pose)

    }

    public void Exit(PlayerStateMachineBrain brain)
    {
        // leave frozen unlock to GameManager after results if needed,
        // but if you ever exit Win back to gameplay:
        brain.Motor?.SetFrozen(false);
        brain.Motor?.SetHitReactLock(false);
        brain.Motor?.EnableInput();
    }

    public void Tick(PlayerStateMachineBrain brain) { }
    public void FixedTick(PlayerStateMachineBrain brain) { }
}
