// ポーズメニューのボタン間ナビゲーション（左右方向）を設定するスクリプト
using UnityEngine;
using UnityEngine.UI;

public class PauseMenuNav : MonoBehaviour
{
    [Header("左から右の順序")]
    public Selectable settingsBtn;  // 設定ボタン
    public Selectable playBtn;      // 再開ボタン
    public Selectable restartBtn;   // リスタートボタン
    public Selectable exitBtn;      // 終了ボタン

    public bool wrap = true; // 先頭の左 = 末尾、末尾の右 = 先頭（ラップ）

    private void Awake()
    {
        if (!settingsBtn || !playBtn || !restartBtn || !exitBtn) return; // いずれかのボタンが未設定なら何もしない

        // 明示的な水平方向リンクを設定
        LinkLR(settingsBtn, left: wrap ? exitBtn : null, right: playBtn);   // 設定ボタンの左右を接続
        LinkLR(playBtn, left: settingsBtn, right: restartBtn);              // 再開ボタンの左右を接続
        LinkLR(restartBtn, left: playBtn, right: exitBtn);                  // リスタートボタンの左右を接続
        LinkLR(exitBtn, left: restartBtn, right: wrap ? settingsBtn : null); // 終了ボタンの左右を接続（ラップあり）

        // 上下方向を無効化して垂直方向へのずれを防ぐ
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
        // 上下方向は空のままにする
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
