using UnityEngine;

public class TutorialStepTrigger : MonoBehaviour
{
    [SerializeField] private TutorialKey key;
    [SerializeField] private bool hideOnExit = true;

    // NEW: only fire in the very first level
    [SerializeField] private bool onlyInFirstLevel = true;

    private bool _fired;

    private bool Allowed()
    {
        if (!onlyInFirstLevel) return true;
        // If GameManager exists, ask it; otherwise fallback to scene name check
        if (GameManager.Instance != null) return GameManager.Instance.IsFirstLevelActive();
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Level_01";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!other.CompareTag("Player")) return;
        if (TutorialProgress.IsLearned(key)) return;
        if (!Allowed()) return;                 // Å© gate

        _fired = true;
        UIManager.Instance?.ShowTutorial(key);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!hideOnExit) return;
        if (!other.CompareTag("Player")) return;
        if (!Allowed()) return;                 // Å© gate

        if (!TutorialProgress.IsLearned(key))
            UIManager.Instance?.HideTutorial(key);
    }
}
