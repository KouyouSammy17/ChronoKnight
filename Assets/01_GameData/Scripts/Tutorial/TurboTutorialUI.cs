// ターボチュートリアルUIの「続ける」ボタン処理スクリプト
using UnityEngine;

public class TurboTutorialUI : MonoBehaviour
{
    public void OnClickContinue()
    {
        TutorialManager.Instance?.CompleteTutorial(TutorialKey.Turbo); // 「続ける」ボタンでターボチュートリアルを完了
    }
}
