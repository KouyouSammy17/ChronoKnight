// ステージクリア時のプレイヤー演出（振り返り＆勝利アニメーション）を非同期で制御するスクリプト
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

public class WinCinematicController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerStateMachineBrain _brain;  // プレイヤーのステートマシン
    [SerializeField] private PlayerAnimator _anim;             // プレイヤーのアニメーター
    [SerializeField] private Transform _modelRoot; // プレイヤー全体ではなくこのルートを回転させる

    // クリア演出をキャンセル可能な非同期タスクとして実行する
    public async UniTask PlayWinCinematicAsync(
     PlayerStateMachineBrain brain,
     Transform modelRoot,
     CancellationToken ct)
    {
        // プレイヤーの入力を無効化し、水平移動を即停止してフリーズさせる
        brain.Motor?.DisableInput();
        brain.Motor?.StopHorizontalInstant();
        brain.Motor?.SetFrozen(true);

        // timeScaleに依存しないスムーズな90度回転
        var t = modelRoot
            .DORotate(new Vector3(0f, modelRoot.eulerAngles.y + 90f, 0f), 0.35f, RotateMode.FastBeyond360)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true); // timeScale=0でも動作させる

        // 回転が完了するまで待機する
        await t.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(ct);

        // 勝利アニメーションをトリガーする
        brain.Anim.TriggerWin();
    }
}
