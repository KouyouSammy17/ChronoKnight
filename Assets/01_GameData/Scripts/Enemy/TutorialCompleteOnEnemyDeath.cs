// 敵の死亡イベントに連動してチュートリアルを完了とするスクリプト
using UnityEngine;

public class TutorialCompleteOnEnemyDeath : MonoBehaviour
{
    [SerializeField] private TutorialKey key = TutorialKey.Attack;  // 完了とするチュートリアルのキー
    [SerializeField] private bool onlyInFirstLevel = true;          // 最初のレベルでのみ発動するか

    private EnemyStats _enemy;  // 対象の敵のステータスコンポーネント
    private bool _done;         // 既に処理済みかどうかのフラグ

    private void Awake()
    {
        _enemy = GetComponent<EnemyStats>();    // 同じオブジェクトのEnemyStatsを取得
    }

    private void OnEnable()
    {
        if (_enemy != null) _enemy.OnDied.AddListener(HandleDied);  // 死亡イベントを購読
    }

    private void OnDisable()
    {
        if (_enemy != null) _enemy.OnDied.RemoveListener(HandleDied);  // 死亡イベントの購読を解除
    }

    private bool Allowed()
    {
        if (!onlyInFirstLevel) return true;                         // レベル制限なしなら常に許可
        if (GameManager.Instance != null) return GameManager.Instance.IsTutorialLevelActive();
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Level_01"; // フォールバック：シーン名で判定
    }

    private void HandleDied()
    {
        if (_done || !Allowed() || TutorialProgress.IsLearned(key)) return; // 二重実行・条件外・学習済みを除外
        _done = true;

        TutorialProgress.SetLearned(key);               // チュートリアルを学習済みとして記録
        UIManager.Instance?.TutorialSuccess(key);   //  uses your UIManager
    }
}
