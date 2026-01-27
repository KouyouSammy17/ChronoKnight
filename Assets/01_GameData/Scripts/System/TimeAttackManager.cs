using UnityEngine;

public class TimeAttackManager : MonoBehaviour
{
    public static TimeAttackManager Instance { get; private set; }

    public bool Enabled { get; private set; }
    public bool IsRunning { get; private set; }
    public float Elapsed { get; private set; }

    public bool IsPaused { get; private set; }

    public void PauseRun()
    {
        if (!Enabled) return;
        IsPaused = true;
    }

    public void ResumeRun()
    {
        if (!Enabled) return;
        IsPaused = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!Enabled || !IsRunning) return;
        Elapsed += Time.unscaledDeltaTime; // ignores timeScale (good for result screens)
    }

    public void Configure(bool enable)
    {
        Enabled = enable;
        Elapsed = 0f;
        IsRunning = false;
    }

    public void StartRun()
    {
        if (!Enabled) return;
        Elapsed = 0f;
        IsRunning = true;
    }

    public void StopRun()
    {
        IsRunning = false;
    }

    public static string Format(float t)
    {
        int min = Mathf.FloorToInt(t / 60f);
        float sec = t - min * 60f;
        return $"{min:00}:{sec:00}";
    }
}
