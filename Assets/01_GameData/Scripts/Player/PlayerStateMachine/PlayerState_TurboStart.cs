// ターボモード起動アニメーションを再生してターボへ移行するステート
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class PlayerState_TurboStart : IPlayerState
{
    public PlayerStateID ID => PlayerStateID.TurboStart;

    // tune this to match your TurboStart clip length
    private const float START_ANIM_TIME = 0.45f; // TurboStartアニメーションクリップの長さに合わせる

    private CancellationTokenSource _cts; // 非同期終了処理のキャンセルトークン

    public void Enter(PlayerStateMachineBrain brain)
    {
        _cts?.Cancel(); // 前のタスクをキャンセル
        _cts = new CancellationTokenSource();

        var turbo = TurboModeManager.Instance;

        if (turbo == null || !turbo.CanStartTurbo())
        {
            brain.ChangeState(brain.Motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne); // ターボ起動不可なら移動ステートへ戻る
            return;
        }

        brain.Motor.DisableInput(); // ターボ起動アニメーション中は入力をロック
        brain.Combat?.CancelCombo(); // 進行中のコンボをキャンセル

        // play anim
        var anim = brain.GetComponentInChildren<PlayerAnimator>();

        // Start turbo FIRST (should switch animator to UnscaledTime + set baseline anim speed)
        bool started = turbo.TryStartTurbo(brain.Motor, anim); // ターボを先に開始（AnimatorをUnscaledTimeモードに切替）
        if (!started)
        {
            brain.Motor.EnableInput(); // 起動失敗なら入力を再開
            brain.ChangeState(brain.Motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne);
            return;
        }

        // Now play the start pose (will play in real-time)
        anim?.TriggerTurboStart(); // ターボ起動アニメーションをトリガー（リアルタイムで再生）

        RunExitAfterAnim(brain, _cts.Token).Forget(); // アニメーション終了後に自動的にステートを終了
    }


    public void Exit(PlayerStateMachineBrain brain)
    {
        _cts?.Cancel(); // 非同期タスクをキャンセル
        _cts?.Dispose();
        _cts = null;
    }

    public void Tick(PlayerStateMachineBrain brain)
    {
        // keep it simple: no movement feed
        brain.Motor.MotorUpdate(false, false, false); // ポーズ中は全ての移動・ジャンプを禁止
    }

    public void FixedTick(PlayerStateMachineBrain brain)
    {
        // stop drift while posing
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false); // ターボ起動ポーズ中は水平移動をロック
    }

    private async UniTaskVoid RunExitAfterAnim(PlayerStateMachineBrain brain, CancellationToken ct)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(START_ANIM_TIME), DelayType.Realtime, PlayerLoopTiming.Update, ct); // 実時間でアニメーション時間を待機
        }
        catch { return; }

        if (ct.IsCancellationRequested) return; // キャンセルされていたら何もしない

        brain.Motor.EnableInput(); // 入力を再開
        brain.ChangeState(brain.Motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne); // 接地状態に応じたステートへ移行
    }
}
