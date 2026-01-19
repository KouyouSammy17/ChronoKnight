using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class PlayerState_TurboStart : IPlayerState
{
    public PlayerStateID ID => PlayerStateID.TurboStart;

    // tune this to match your TurboStart clip length
    private const float START_ANIM_TIME = 0.45f;

    private CancellationTokenSource _cts;

    public void Enter(PlayerStateMachineBrain brain)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        // lock gameplay input briefly (no dash/attack during the start pose)
        brain.Motor.DisableInput();
        brain.Combat?.CancelCombo();

        // play anim
        var anim = brain.GetComponentInChildren<PlayerAnimator>();
        anim?.TriggerTurboStart();

        // start turbo system
        var turbo = TurboModeManager.Instance;
        if (turbo != null)
        {
            turbo.TryStartTurbo(brain.Motor, anim);
        }

        RunExitAfterAnim(brain, _cts.Token).Forget();
    }

    public void Exit(PlayerStateMachineBrain brain)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Tick(PlayerStateMachineBrain brain)
    {
        // keep it simple: no movement feed
        brain.Motor.MotorUpdate(false, false, false);
    }

    public void FixedTick(PlayerStateMachineBrain brain)
    {
        // stop drift while posing
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false);
    }

    private async UniTaskVoid RunExitAfterAnim(PlayerStateMachineBrain brain, CancellationToken ct)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(START_ANIM_TIME),
                DelayType.Realtime, PlayerLoopTiming.Update, ct);
        }
        catch { return; }

        if (ct.IsCancellationRequested) return;

        // restore control
        brain.Motor.EnableInput();

        // go back to locomotion depending on grounded
        brain.ChangeState(brain.Motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne);
    }
}
