// タイムアタックの経過時間をリアルタイムでテキスト表示するスクリプト
using TMPro;
using UnityEngine;

public class TimeAttackTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;       // 時間を表示するTMPテキスト
    [SerializeField] private GameObject _root;     // optional (assign self or parent)

    private void Awake()
    {
        if (_root == null) _root = gameObject;                                     // 未設定なら自身を使用
        if (_text == null) _text = GetComponentInChildren<TMP_Text>(true);         // 子からTMPを自動取得
    }

    private void Update()
    {
        var ta = TimeAttackManager.Instance;
        // Show if TimeAttack is enabled OR if it's running (accounts for scene transitions)
        bool show = ta != null && (ta.Enabled || ta.IsRunning); // タイムアタック有効中または実行中なら表示

        if (_root != null)
        {
            if (_root.activeSelf != show)
            {
                _root.SetActive(show); // 表示状態が変わった場合のみSetActiveを呼ぶ（毎フレームの余分な呼び出しを避ける）
            }
        }

        if (!show || _text == null) return; // 非表示状態またはテキスト未設定なら更新しない

        _text.text = TimeAttackManager.Format(ta.Elapsed); // 経過時間をフォーマットして表示
    }

    /// <summary>Public method to force-show the timer (called by TutorialManager)</summary>
    public void EnsureVisible()
    {
        if (_root != null)
            _root.SetActive(true); // タイマーを強制的に表示する（チュートリアル等から呼び出される）
    }
}
