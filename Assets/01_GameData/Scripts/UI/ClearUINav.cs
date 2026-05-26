// ゲームクリアUI内のボタン間ナビゲーション（左右方向）を設定するスクリプト
using UnityEngine;
using UnityEngine.UI;

public class ClearUINav : MonoBehaviour
{
    [Header("Left is always Restart (or Back)")]
    [SerializeField] private Selectable _leftBtn;   // Restart button

    [Header("Right is dynamic (Next / Title)")]
    [SerializeField] private Selectable _rightBtn;  // assign default (optional)

    [Tooltip("left of first = last, right of last = first")]
    [SerializeField] private bool _wrap = true; // 端から端へのラップナビゲーションを有効にするか

    private void Awake()
    {
        Apply(); // 起動時にナビゲーションを設定
    }

    /// <summary>
    /// Call this before focusing first selected.
    /// </summary>
    public void Configure(Selectable leftRestart, Selectable rightAction, bool wrap)
    {
        _leftBtn = leftRestart;   // 左ボタン（リスタート）を設定
        _rightBtn = rightAction;  // 右ボタン（次へ/タイトルなど）を設定
        _wrap = wrap;
        Apply(); // ナビゲーションを再設定
    }

    public void Apply()
    {
        if (!_leftBtn || !_rightBtn) return; // ボタンが未設定なら処理しない

        // Explicit horizontal links
        LinkLR(_leftBtn, left: _wrap ? _rightBtn : null, right: _rightBtn);   // 左ボタンの左右を接続
        LinkLR(_rightBtn, left: _leftBtn, right: _wrap ? _leftBtn : null);    // 右ボタンの左右を接続（ラップ）

        // Disable up/down to avoid vertical drift
        DisableUD(_leftBtn);
        DisableUD(_rightBtn);
    }

    private static void LinkLR(Selectable s, Selectable left, Selectable right)
    {
        var nav = s.navigation;
        nav.mode = Navigation.Mode.Explicit; // 明示的ナビゲーションモードに設定
        nav.selectOnLeft = left;
        nav.selectOnRight = right;
        nav.selectOnUp = null;
        nav.selectOnDown = null;
        s.navigation = nav;
    }

    private static void DisableUD(Selectable s)
    {
        var nav = s.navigation;
        nav.selectOnUp = null;   // 上方向への移動を無効化
        nav.selectOnDown = null; // 下方向への移動を無効化
        s.navigation = nav;
    }
}
