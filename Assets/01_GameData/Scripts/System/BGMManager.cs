// BGMをMMSoundManagerのMusicトラックで管理するシングルトン。
// シーンに応じた自動切り替え・フェードイン/アウト・ポーズ対応を提供する。
//
// ─── セットアップ手順 ────────────────────────────────────────────────
//  1. BGM.prefab（MMSoundManager入り）をシーンに配置したまま維持する。
//     ただし MMF_Player の AutoPlayOnStart を OFF にする
//     （BGMManagerが代わりに再生を管理するため）。
//  2. このスクリプトを BGM.prefab と同じ GameObject にアタッチする
//     か、専用の空 GameObject にアタッチして同シーンに置く。
//  3. Inspector の Scene BGMs リストに、シーン名とBGMクリップを登録する。
//  4. GameManager の PauseGame() / ResumeGame() の末尾で
//     BGMManager.Instance?.OnGamePaused() / OnGameResumed() を呼ぶか、
//     UnityEvent を使って接続する。
//  5. GameClear・GameOver のタイミングで OnGameEnded() を呼ぶ。
// ────────────────────────────────────────────────────────────────────
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using MoreMountains.Tools;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    // ────────────────────────────────────────────────────────────────
    // シーンとBGMのマッピング
    // ────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class SceneBGM
    {
        [Tooltip("SceneManager.GetActiveScene().name と完全一致させる")]
        public string sceneName;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("シーン別BGM設定")]
    [Tooltip("シーン名 → BGMクリップのマッピング。未登録シーンはBGMなし")]
    [SerializeField] private SceneBGM[] _sceneBGMs;

    // ────────────────────────────────────────────────────────────────
    // フェード設定
    // ────────────────────────────────────────────────────────────────

    [Header("フェード時間（秒）")]
    [SerializeField] private float _fadeInDuration  = 1.0f;    // シーン開始時のフェードイン
    [SerializeField] private float _fadeOutDuration = 0.8f;    // シーン切り替え時のフェードアウト
    [SerializeField] private float _endFadeDuration = 2.0f;    // ゲームクリア・ゲームオーバー時のフェードアウト

    // ────────────────────────────────────────────────────────────────
    // 内部状態
    // ────────────────────────────────────────────────────────────────

    // Musicトラックの定数（SFXと音量を別管理するため Music を使う）
    private const MMSoundManager.MMSoundManagerTracks MUSIC = MMSoundManager.MMSoundManagerTracks.Music;

    private AudioSource _currentSource;     // 現在再生中のAudioSource（Trigger の戻り値）
    private AudioClip   _currentClip;       // 現在のクリップ（同じシーンで二重再生しないため）
    private float       _currentVolume = 1f;// クリップ側の指定ボリューム
    private Coroutine   _fadeRoutine;       // フェード処理のコルーチン

    // ────────────────────────────────────────────────────────────────
    // Unityライフサイクル
    // ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 加算ロード（ローディング画面）は無視する
        if (mode == LoadSceneMode.Additive) return;

        var entry = FindEntry(scene.name);

        if (entry == null || entry.clip == null)
        {
            // マッピング未登録のシーンはBGMを停止する
            StopBGM(_fadeOutDuration);
            return;
        }

        SwitchBGM(entry.clip, entry.volume);
    }

    // ────────────────────────────────────────────────────────────────
    // 公開API（GameManagerから呼ぶ or Inspectorで UnityEvent に接続）
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// ポーズ時に呼ぶ。BGMを一時停止する。
    /// GameManager.PauseGame() の末尾で呼ぶこと。
    /// </summary>
    public void OnGamePaused()
    {
        MMSoundManager.Instance?.PauseTrack(MUSIC);
    }

    /// <summary>
    /// ポーズ解除時に呼ぶ。BGMを再開する。
    /// GameManager.ResumeGame() の末尾で呼ぶこと。
    /// </summary>
    public void OnGameResumed()
    {
        MMSoundManager.Instance?.PlayTrack(MUSIC);
    }

    /// <summary>
    /// ゲームクリア・ゲームオーバー時に呼ぶ。BGMをゆっくりフェードアウトして停止する。
    /// </summary>
    public void OnGameEnded()
    {
        StopBGM(_endFadeDuration);
    }

    /// <summary>
    /// 外部から直接クリップを指定して再生したい場合に使う。
    /// </summary>
    public void PlayBGMDirect(AudioClip clip, float volume = 1f)
    {
        SwitchBGM(clip, volume);
    }

    // ────────────────────────────────────────────────────────────────
    // 内部ロジック
    // ────────────────────────────────────────────────────────────────

    private void SwitchBGM(AudioClip clip, float volume)
    {
        // 同じクリップが既に流れていれば何もしない
        if (_currentClip == clip && _currentSource != null) return;

        CancelFade();

        if (_currentSource != null)
        {
            // クロスフェード：現在のBGMをフェードアウトしてから次を再生
            _fadeRoutine = StartCoroutine(Co_CrossFade(clip, volume));
        }
        else
        {
            // 初回再生：そのままフェードイン
            PlayInternal(clip, volume);
        }
    }

    private void StopBGM(float fadeDuration)
    {
        CancelFade();
        _currentClip = null;

        if (_currentSource == null || MMSoundManager.Instance == null) return;

        _fadeRoutine = StartCoroutine(Co_FadeOutAndFree(fadeDuration, restoreVolume: true));
    }

    /// <summary>MMSoundManager に Trigger してフェードインで再生開始する。</summary>
    private void PlayInternal(AudioClip clip, float volume)
    {
        if (clip == null || MMSoundManager.Instance == null) return;

        _currentClip   = clip;
        _currentVolume = volume;

        // Musicトラックのボリュームを再生前にリセット（前回のフェードアウトで下がっている場合がある）
        MMSoundManager.Instance.SetTrackVolume(MUSIC, 1f);

        MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
        options.MmSoundManagerTrack          = MUSIC;
        options.Loop                         = true;
        options.Volume                       = volume;
        options.Persistent                   = true;    // シーン遷移後もAudioSourceを保持
        options.DoNotAutoRecycleIfNotDonePlaying = true;
        // ────── フェードイン ──────
        options.Fade                         = true;
        options.FadeInitialVolume            = 0f;
        options.FadeDuration                 = _fadeInDuration;

        _currentSource = MMSoundManagerSoundPlayEvent.Trigger(clip, options);
    }

    // ────────────────────────────────────────────────────────────────
    // コルーチン
    // ────────────────────────────────────────────────────────────────

    private IEnumerator Co_CrossFade(AudioClip nextClip, float nextVolume)
    {
        yield return Co_FadeOutAndFree(_fadeOutDuration, restoreVolume: false);
        PlayInternal(nextClip, nextVolume);
        _fadeRoutine = null;
    }

    /// <summary>
    /// Musicトラックのボリュームをフェードアウトし、Stop → Free する。
    /// restoreVolume=true の場合、Free後にトラックボリュームを1に戻す。
    /// </summary>
    private IEnumerator Co_FadeOutAndFree(float duration, bool restoreVolume)
    {
        if (MMSoundManager.Instance != null && duration > 0f)
        {
            float startVol = MMSoundManager.Instance.GetTrackVolume(MUSIC, false);
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                // Time.timeScaleに影響されないリアル時間で進める
                elapsed += Time.unscaledDeltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);
                MMSoundManager.Instance.SetTrackVolume(MUSIC, Mathf.Lerp(startVol, 0f, t));
                yield return null;
            }
        }

        if (MMSoundManager.Instance != null)
        {
            MMSoundManager.Instance.StopTrack(MUSIC);
            MMSoundManager.Instance.FreeTrack(MUSIC);

            if (restoreVolume)
                MMSoundManager.Instance.SetTrackVolume(MUSIC, 1f); // ボリュームを1に戻す
        }

        _currentSource = null;
        _fadeRoutine   = null;
    }

    private void CancelFade()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // ヘルパー
    // ────────────────────────────────────────────────────────────────

    private SceneBGM FindEntry(string sceneName)
    {
        if (_sceneBGMs == null) return null;
        foreach (var e in _sceneBGMs)
            if (e != null && e.sceneName == sceneName) return e;
        return null;
    }

    private void OnDestroy()
    {
        // このオブジェクト破棄時（デバッグ中のPlayMode終了など）にコルーチンをクリア
        CancelFade();
    }
}
