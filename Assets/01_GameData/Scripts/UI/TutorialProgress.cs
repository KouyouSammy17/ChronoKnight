using System;

public static class TutorialProgress
{
    const string Prefix = "TUT_";

    public static bool IsLearned(TutorialKey key)
        => UnityEngine.PlayerPrefs.GetInt(Prefix + (int)key, 0) == 1;

    public static void SetLearned(TutorialKey key)
    {
        UnityEngine.PlayerPrefs.SetInt(Prefix + (int)key, 1);
        UnityEngine.PlayerPrefs.Save();
    }

    // NEW: reset all tutorial flags
    public static void ResetAll()
    {
        foreach (TutorialKey key in Enum.GetValues(typeof(TutorialKey)))
        {
            UnityEngine.PlayerPrefs.DeleteKey(Prefix + (int)key);
        }
        UnityEngine.PlayerPrefs.Save();
    }
}