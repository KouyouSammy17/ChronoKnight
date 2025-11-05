using UnityEngine;

public class DebugActions : MonoBehaviour
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private PlayerAnimator _playerAnim;

    public void OnResetTutorial()
    {
        // 1) Freeze gameplay control + animator speed
        if (_player) _player.DisableInput();
        if (_playerAnim) _playerAnim.PauseAnimator();   // add method below

        // 2) Do your debug action
        TutorialProgress.ResetAll();
        UIManager.Instance?.ShowTutorial(TutorialKey.Move);

        // 3) Unfreeze after a short delay
        this.StartCoroutine(UnfreezeAfter(0.2f));
    }

    private System.Collections.IEnumerator UnfreezeAfter(float s)
    {
        yield return new WaitForSecondsRealtime(s);
        if (_player) _player.EnableInput();
        if (_playerAnim) _playerAnim.ResumeAnimator();
    }
}
