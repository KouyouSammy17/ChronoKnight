using UnityEngine;

public class TutorialStepTrigger : MonoBehaviour
{
    [SerializeField] private TutorialKey key;
    [SerializeField] private bool hideOnExit = true;
    private bool _fired;

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!other.CompareTag("Player")) return;
        if (TutorialProgress.IsLearned(key)) return;

        _fired = true;
        UIManager.Instance?.ShowTutorial(key);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!hideOnExit) return;
        if (!other.CompareTag("Player")) return;

        if (!TutorialProgress.IsLearned(key))
            UIManager.Instance?.HideTutorial(key);
    }
}
