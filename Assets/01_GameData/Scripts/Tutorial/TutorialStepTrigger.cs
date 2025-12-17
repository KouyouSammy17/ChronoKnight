using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class TutorialStepTrigger : MonoBehaviour
{
    public enum TriggerAction { Show, Complete }

    [Header("Which tutorial key does this trigger control?")]
    [SerializeField] private TutorialKey key;

    [Header("What happens on enter?")]
    [SerializeField] private TriggerAction action = TriggerAction.Show;

    [Header("Visibility")]
    [SerializeField] private bool hideOnExit = true;         // only relevant if action=Show

    [Header("Level Gate")]
    [SerializeField] private bool onlyInFirstLevel = true;   // prevent firing in later levels

    private bool _fired;

    private void Awake()
    {
        // ensure trigger collider
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private bool Allowed()
    {
        if (!onlyInFirstLevel) return true;

        // Prefer GameManager gate if available
        if (GameManager.Instance != null)
            return GameManager.Instance.IsFirstLevelActive();

        // Fallback to name check
        return SceneManager.GetActiveScene().name == "Level_01";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!other.CompareTag("Player")) return;
        if (!Allowed()) return;

        // If already learned, no need to show/complete again
        if (TutorialProgress.IsLearned(key))
            return;

        _fired = true;

        if (action == TriggerAction.Show)
        {
            //  Centralized routing (Momentum/Turbo special cases handled inside TutorialManager)
            TutorialManager.Instance?.RequestShow(key);
        }
        else // Complete
        {
            // Centralized completion (handles SetLearned + success + special resume for Momentum/Turbo)
            TutorialManager.Instance?.CompleteTutorial(key);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (action != TriggerAction.Show) return;
        if (!hideOnExit) return;
        if (!_fired) return;
        if (!other.CompareTag("Player")) return;
        if (!Allowed()) return;

        // Only hide if not learned (Momentum/Turbo will ignore hide inside TutorialManager)
        if (!TutorialProgress.IsLearned(key))
            TutorialManager.Instance?.RequestHide(key);
    }
}
