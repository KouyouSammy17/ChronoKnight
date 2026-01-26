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

        var turbo = TurboModeManager.Instance;

        if (turbo == null || !turbo.CanStartTurbo())
        {
            brain.ChangeState(brain.Motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne);
            return;
        }

        brain.Motor.DisableInput();
        brain.Combat?.CancelCombo();

        // play anim
        var anim = brain.GetComponentInChildren<PlayerAnimator>();

        // Start turbo FIRST (should switch animator to UnscaledTime + set baseline anim speed)
        bool started = turbo.TryStartTurbo(brain.Motor, anim);
        if (!started)
        {
            brain.Motor.EnableInput();
            brain.ChangeState(brain.Motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne);
            return;
        }

        // Now play the start pose (will play in real-time)
        anim?.TriggerTurboStart();

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
            await UniTask.Delay(TimeSpan.FromSeconds(START_ANIM_TIME), DelayType.Realtime, PlayerLoopTiming.Update, ct);
        }
        catch { return; }

        if (ct.IsCancellationRequested) return;

        brain.Motor.EnableInput();
        brain.ChangeState(brain.Motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne);
    }
}
