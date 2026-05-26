// タイムアタックモードの経過時間を計測・管理するシングルトン
using UnityEngine;

public class TimeAttackManager : MonoBehaviour
{
    public static TimeAttackManager Instance { get; private set; }

    public bool Enabled { get; private set; }     // タイムアタックが有効かどうか
    public bool IsRunning { get; private set; }   // 計測が進行中かどうか
    public float Elapsed { get; private set; }    // 経過時間（秒）

    public bool IsPaused { get; private set; }    // 計測が一時停止中かどうか

    public void PauseRun()
    {
        // タイムアタックが無効なら何もしない
        if (!Enabled) return;
        IsPaused = true;
    }

    public void ResumeRun()
    {
        // タイムアタックが無効なら何もしない
        if (!Enabled) return;
        IsPaused = false;
    }

    private void Awake()
    {
        // シングルトンの初期化：重複インスタンスは破棄する
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // 無効・停止中・一時停止中はカウントしない
        if (!Enabled || !IsRunning) return;
        Elapsed += Time.unscaledDeltaTime; // ignores timeScale (good for result screens)
    }

    public void Configure(bool enable)
    {
        // タイムアタックの有効・無効を設定し、経過時間と状態をリセットする
        Enabled = enable;
        Elapsed = 0f;
        IsRunning = false;
    }

    public void StartRun()
    {
        if (!Enabled) return;
        Elapsed = 0f;      // 計測開始時にリセットする
        IsRunning = true;
    }

    public void StopRun()
    {
        // 計測を停止する（経過時間はそのまま保持する）
        IsRunning = false;
    }

    // 秒数を「MM:SS」形式の文字列に変換する
    public static string Format(float t)
    {
        int min = Mathf.FloorToInt(t / 60f);
        float sec = t - min * 60f;
        return $"{min:00}:{sec:00}";
    }
}
