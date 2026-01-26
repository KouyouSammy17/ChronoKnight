using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

public class WinCinematicController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerStateMachineBrain _brain;
    [SerializeField] private PlayerAnimator _anim;
    [SerializeField] private Transform _modelRoot; // <-- rotate this (not the whole player)

    public async UniTask PlayWinCinematicAsync(
     PlayerStateMachineBrain brain,
     Transform modelRoot,
     CancellationToken ct)
    {
        brain.Motor?.DisableInput();
        brain.Motor?.StopHorizontalInstant();
        brain.Motor?.SetFrozen(true);

        // smooth 180 turn (ignores timeScale)
        var t = modelRoot
            .DORotate(new Vector3(0f, modelRoot.eulerAngles.y + 90f, 0f), 0.35f, RotateMode.FastBeyond360)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);

        await t.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(ct);

        brain.Anim.TriggerWin();
    }
}
