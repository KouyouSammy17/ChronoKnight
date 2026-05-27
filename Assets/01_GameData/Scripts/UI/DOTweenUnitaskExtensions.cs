// DOTweenのTweenをUniTaskで非同期待機するための拡張メソッド集
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public static class DOTweenUniTaskExtensions
{
    /// TweenまたはSequenceをawaitで待機する。キャンセルトークンが発火した場合はキャンセルされる。
    /// Kill済みのTweenへのクエリは行わない（"killed and invalid"警告を防ぐため）。
    public static UniTask Await(this Tween tween, CancellationToken ct = default, bool killOnCancel = true)
    {
        // nullまたは非アクティブなら待機不要
        if (tween == null || !tween.IsActive()) return UniTask.CompletedTask;

        // 既に完了していれば即座に返す（まだアクティブなので安全）
        if (tween.IsComplete()) return UniTask.CompletedTask;

        var tcs = new UniTaskCompletionSource(); // Tweenの完了を外部に伝えるためのTaskCompletionSource
        bool completed = false; // OnComplete とOnKillの競合を防ぐフラグ

        // TweenがKill/デスポーンされる前に完了をマークする
        tween.OnComplete(() =>
        {
            completed = true;        // 正常完了としてフラグを立てる
            tcs.TrySetResult();      // awaitを解除する
        });

        tween.OnKill(() =>
        {
            // ここでtween.IsComplete()を呼ばないこと — Tween はすでに無効になっている。
            if (!completed) tcs.TrySetCanceled(); // 未完了でKillされた場合はキャンセル扱いにする
        });

        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                // まだ完了していなければ、必要に応じてTweenを停止する
                if (!completed && killOnCancel && tween.IsActive())
                {
                    // コールバックなしでKillする場合は complete:false を渡してOnCompleteを発火させないこともできる。
                    tween.Kill(); // キャンセルトークンが発火したらTweenも停止する
                }
                tcs.TrySetCanceled(); // Taskをキャンセル状態にする
            });
        }

        return tcs.Task;
    }
}
