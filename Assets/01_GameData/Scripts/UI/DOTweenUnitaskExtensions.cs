// DOTweenのTweenをUniTaskで非同期待機するための拡張メソッド集
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public static class DOTweenUniTaskExtensions
{
    /// Await a tween/sequence. Cancels if token is canceled.
    /// No queries on a killed tween (prevents "killed and invalid" warnings).
    public static UniTask Await(this Tween tween, CancellationToken ct = default, bool killOnCancel = true)
    {
        // If null or inactive, nothing to wait for
        if (tween == null || !tween.IsActive()) return UniTask.CompletedTask;

        // If already complete, return immediately (safe because it's still active)
        if (tween.IsComplete()) return UniTask.CompletedTask;

        var tcs = new UniTaskCompletionSource(); // Tweenの完了を外部に伝えるためのTaskCompletionSource
        bool completed = false; // OnComplete とOnKillの競合を防ぐフラグ

        // Mark completion BEFORE the tween gets killed/despawned
        tween.OnComplete(() =>
        {
            completed = true;        // 正常完了としてフラグを立てる
            tcs.TrySetResult();      // awaitを解除する
        });

        tween.OnKill(() =>
        {
            // DO NOT call tween.IsComplete() here — tween is invalid now.
            if (!completed) tcs.TrySetCanceled(); // 未完了でKillされた場合はキャンセル扱いにする
        });

        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                // If not completed yet, optionally kill the tween
                if (!completed && killOnCancel && tween.IsActive())
                {
                    // Kill without callbacks? If you want, pass complete:false to avoid triggering OnComplete.
                    tween.Kill(); // キャンセルトークンが発火したらTweenも停止する
                }
                tcs.TrySetCanceled(); // Taskをキャンセル状態にする
            });
        }

        return tcs.Task;
    }
}
