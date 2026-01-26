using UnityEngine;

public class TutorialCompleteOnEnemyDeath : MonoBehaviour
{
    [SerializeField] private TutorialKey key = TutorialKey.Attack;
    [SerializeField] private bool onlyInFirstLevel = true;

    private EnemyStats _enemy;
    private bool _done;

    private void Awake()
    {
        _enemy = GetComponent<EnemyStats>();
    }

    private void OnEnable()
    {
        if (_enemy != null) _enemy.OnDied.AddListener(HandleDied);
    }

    private void OnDisable()
    {
        if (_enemy != null) _enemy.OnDied.RemoveListener(HandleDied);
    }

    private bool Allowed()
    {
        if (!onlyInFirstLevel) return true;
        if (GameManager.Instance != null) return GameManager.Instance.IsTutorialLevelActive();
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Level_01";
    }

    private void HandleDied()
    {
        if (_done || !Allowed() || TutorialProgress.IsLearned(key)) return;
        _done = true;

        TutorialProgress.SetLearned(key);
        UIManager.Instance?.TutorialSuccess(key);   //  uses your UIManager
    }
}
