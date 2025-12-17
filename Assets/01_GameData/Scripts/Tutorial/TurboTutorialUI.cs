using UnityEngine;

public class TurboTutorialUI : MonoBehaviour
{
    public void OnClickContinue()
    {
        TutorialManager.Instance?.CompleteTutorial(TutorialKey.Turbo);
    }
}
