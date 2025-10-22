using UnityEngine;

public class DebugActions : MonoBehaviour
{
    public void OnResetTutorial()
    {
        TutorialProgress.ResetAll();
        // Optional: toast / dialog
        UIManager.Instance?.ShowTutorial(TutorialKey.Move); // e.g., preview first tip
    }
}
