// チュートリアルの学習状態をPlayerPrefsに永続保存・参照するスタティッククラス
using System;

public static class TutorialProgress
{
    const string Prefix = "TUT_";  // PlayerPrefsのキープレフィックス

    // 指定したチュートリアルキーが学習済みかどうかを確認する
    public static bool IsLearned(TutorialKey key)
        => UnityEngine.PlayerPrefs.GetInt(Prefix + (int)key, 0) == 1;  // 1ならば学習済み

    // 指定したチュートリアルキーを学習済みとして保存する
    public static void SetLearned(TutorialKey key)
    {
        UnityEngine.PlayerPrefs.SetInt(Prefix + (int)key, 1);  // 学習済みフラグを1に設定
        UnityEngine.PlayerPrefs.Save();                         // 即座に永続保存
    }

    // NEW: reset all tutorial flags
    // すべてのチュートリアルフラグを削除してリセットする
    public static void ResetAll()
    {
        foreach (TutorialKey key in Enum.GetValues(typeof(TutorialKey)))
        {
            UnityEngine.PlayerPrefs.DeleteKey(Prefix + (int)key);   // 各キーのPlayerPrefsエントリを削除
        }
        UnityEngine.PlayerPrefs.Save();     // 削除結果を永続保存
    }
}
