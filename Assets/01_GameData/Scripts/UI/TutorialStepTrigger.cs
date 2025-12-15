using UnityEngine;

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

    private bool Allowed()
    {
        if (!onlyInFirstLevel) return true;

        // Prefer GameManager gate if available
        if (GameManager.Instance != null)
            return GameManager.Instance.IsFirstLevelActive();

        // Fallback to name check
        return UnityEngine.SceneManagement.SceneManager
            .GetActiveScene().name == "Level_01";
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
            // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
            // SPECIAL CASE: Momentum tutorial
            // Use GameManager so it can:
            //  - Zoom camera
            //  - Show gauge + black mask
            //  - Then show the Momentum UI
            // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
            if (key == TutorialKey.Momentum)
            {
                GameManager.Instance?.ShowMomentumTutorial();
            }
            else if (key == TutorialKey.Turbo)
            {
                GameManager.Instance?.ShowTurboTutorial();
            }
            else
            {
                // Default behavior for Move / Jump / Dash / Attack
                UIManager.Instance?.ShowTutorial(key);
            }
        }
        else // Complete
        {
            // Mark learned + success animation
            TutorialProgress.SetLearned(key);
            UIManager.Instance?.TutorialSuccess(key);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (action != TriggerAction.Show) return; // only meaningful for gshowh triggers
        if (!hideOnExit) return;
        if (!other.CompareTag("Player")) return;
        if (!Allowed()) return;

        // If not learned yet, hide the panel on exit
        if (!TutorialProgress.IsLearned(key))
            UIManager.Instance?.HideTutorial(key);
    }
}
