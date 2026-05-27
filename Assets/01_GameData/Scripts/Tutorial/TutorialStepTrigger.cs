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
    [SerializeField] private bool hideOnExit = true;         // 退出時に非表示にするか（action=Showの場合のみ有効）

    [Header("Level Gate")]
    [SerializeField] private bool onlyInFirstLevel = true;   // 後続のレベルで発動しないよう最初のレベルのみに限定するか

    private bool _fired;    // 既に発動済みかどうかのフラグ

    private void Awake()
    {
        // トリガーコライダーを確認
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;  // コライダーをトリガーモードに設定
    }

    private bool Allowed()
    {
        if (!onlyInFirstLevel) return true;     // レベル制限なしなら常に許可

        // GameManagerのゲートを優先使用
        if (GameManager.Instance != null)
            return GameManager.Instance.IsTutorialLevelActive();

        // フォールバック：シーン名で判定
        return SceneManager.GetActiveScene().name == "Level_01";    // フォールバック：シーン名で判定
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;                             // 二重発動を防ぐ
        if (!other.CompareTag("Player")) return;        // プレイヤー以外は無視
        if (!Allowed()) return;                         // 条件外のレベルなら無視

        // 学習済みの場合は表示・完了処理を再度行う必要はない
        if (TutorialProgress.IsLearned(key))
            return;     // 学習済みなら何もしない

        _fired = true;

        if (action == TriggerAction.Show)
        {
            // 表示ルーティングを一元管理（モメンタム・ターボの特殊ケースはTutorialManager内で処理）
            TutorialManager.Instance?.RequestShow(key); // TutorialManagerに表示リクエストを送る
        }
        else // 完了
        {
            // 完了処理を一元管理（SetLearned + 成功演出 + モメンタム・ターボの特殊再開処理を含む）
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

        // 未学習の場合のみ非表示（モメンタム・ターボはTutorialManager内で非表示を無視）
        if (!TutorialProgress.IsLearned(key))
            TutorialManager.Instance?.RequestHide(key); // 未学習の場合のみ非表示リクエストを送る
    }
}
