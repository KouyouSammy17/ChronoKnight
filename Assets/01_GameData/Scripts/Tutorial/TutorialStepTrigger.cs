// 物理トリガーによってチュートリアルの表示・完了を制御するスクリプト
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class TutorialStepTrigger : MonoBehaviour
{
    public enum TriggerAction { Show, Complete }    // トリガー進入時の動作種別

    [Header("Which tutorial key does this trigger control?")]
    [SerializeField] private TutorialKey key;                   // このトリガーが制御するチュートリアルキー

    [Header("What happens on enter?")]
    [SerializeField] private TriggerAction action = TriggerAction.Show; // 進入時に表示するか完了とするか

    [Header("Visibility")]
    [SerializeField] private bool hideOnExit = true;         // only relevant if action=Show   // 退出時に非表示にするか

    [Header("Level Gate")]
    [SerializeField] private bool onlyInFirstLevel = true;   // prevent firing in later levels  // 最初のレベルのみ発動するか

    private bool _fired;    // 既に発動済みかどうかのフラグ

    private void Awake()
    {
        // ensure trigger collider
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;  // コライダーをトリガーモードに設定
    }

    private bool Allowed()
    {
        if (!onlyInFirstLevel) return true;     // レベル制限なしなら常に許可

        // Prefer GameManager gate if available
        if (GameManager.Instance != null)
            return GameManager.Instance.IsTutorialLevelActive();

        // Fallback to name check
        return SceneManager.GetActiveScene().name == "Level_01";    // フォールバック：シーン名で判定
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;                             // 二重発動を防ぐ
        if (!other.CompareTag("Player")) return;        // プレイヤー以外は無視
        if (!Allowed()) return;                         // 条件外のレベルなら無視

        // If already learned, no need to show/complete again
        if (TutorialProgress.IsLearned(key))
            return;     // 学習済みなら何もしない

        _fired = true;

        if (action == TriggerAction.Show)
        {
            //  Centralized routing (Momentum/Turbo special cases handled inside TutorialManager)
            TutorialManager.Instance?.RequestShow(key); // TutorialManagerに表示リクエストを送る
        }
        else // Complete
        {
            // Centralized completion (handles SetLearned + success + special resume for Momentum/Turbo)
            TutorialManager.Instance?.CompleteTutorial(key);   // TutorialManagerに完了処理を委譲
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (action != TriggerAction.Show) return;       // 表示アクション以外は退出処理しない
        if (!hideOnExit) return;                        // 退出時非表示が無効なら何もしない
        if (!_fired) return;                            // まだ発動していなければスキップ
        if (!other.CompareTag("Player")) return;        // プレイヤー以外は無視
        if (!Allowed()) return;

        // Only hide if not learned (Momentum/Turbo will ignore hide inside TutorialManager)
        if (!TutorialProgress.IsLearned(key))
            TutorialManager.Instance?.RequestHide(key); // 未学習の場合のみ非表示リクエストを送る
    }
}
