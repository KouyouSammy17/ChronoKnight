// ポーズメニューのボタン間ナビゲーション（左右方向）を設定するスクリプト
using UnityEngine;
using UnityEngine.UI;

public class PauseMenuNav : MonoBehaviour
{
    [Header("Order left → right")]
    public Selectable settingsBtn;  // 設定ボタン
    public Selectable playBtn;      // 再開ボタン
    public Selectable restartBtn;   // リスタートボタン
    public Selectable exitBtn;      // 終了ボタン

    public bool wrap = true; // left of first = last, right of last = first

    private void Awake()
    {
        if (!settingsBtn || !playBtn || !restartBtn || !exitBtn) return; // いずれかのボタンが未設定なら何もしない

        // Explicit horizontal links
        LinkLR(settingsBtn, left: wrap ? exitBtn : null, right: playBtn);   // 設定ボタンの左右を接続
        LinkLR(playBtn, left: settingsBtn, right: restartBtn);              // 再開ボタンの左右を接続
        LinkLR(restartBtn, left: playBtn, right: exitBtn);                  // リスタートボタンの左右を接続
        LinkLR(exitBtn, left: restartBtn, right: wrap ? settingsBtn : null); // 終了ボタンの左右を接続（ラップあり）

        // Disable up/down to avoid vertical drift
        DisableUD(settingsBtn);
        DisableUD(playBtn);
        DisableUD(restartBtn);
        DisableUD(exitBtn);
    }

    private static void LinkLR(Selectable s, Selectable left, Selectable right)
    {
        var nav = s.navigation;
        nav.mode = Navigation.Mode.Explicit; // 明示的ナビゲーションモードに設定
        nav.selectOnLeft = left;
        nav.selectOnRight = right;
        // Keep vertical empty
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
