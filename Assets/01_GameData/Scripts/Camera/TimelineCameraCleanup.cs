// タイムラインによるイントロシネマティック終了後にカメラの後始末を行うスクリプト
using UnityEngine;
using UnityEngine.Playables;

#if CINEMACHINE
using Unity.Cinemachine;
#endif

public class TimelineCameraCleanup : MonoBehaviour
{
    [Header("Director (the Timeline that plays your intro)")]
    [SerializeField] PlayableDirector _director;                    // イントロを再生するPlayableDirector
    [SerializeField] bool _autoStopDirectorOnEnd = true;            // 終了時にDirectorを自動停止するか

#if CINEMACHINE
    [Header("Cinemachine")]
    [SerializeField] CinemachineCamera _main25D;             // gameplay cam               // ゲームプレイ用のメインカメラ
    [SerializeField] CinemachineCamera[] _animatedCams;      // intro/orbit cams animated by Timeline  // タイムラインで動かすイントロカメラ群
    [SerializeField] bool _restoreLensOnEnd = true;                 // 終了時にカメラの視野角を元に戻すか
    float[] _lensFOVDefaults;                                       // イントロカメラのデフォルトFOV値を保存する配列
#endif

    // ─────────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (_director) _director.extrapolationMode = DirectorWrapMode.None; // タイムライン終了後に外挿しないよう設定

#if CINEMACHINE
        if (_animatedCams != null && _animatedCams.Length > 0)
        {
            _lensFOVDefaults = new float[_animatedCams.Length];
            for (int i = 0; i < _animatedCams.Length; i++)
                if (_animatedCams[i]) _lensFOVDefaults[i] = _animatedCams[i].Lens.FieldOfView;  // 各イントロカメラのFOVを記録
        }
#endif
    }

    void OnEnable()
    {
        if (_director != null) _director.stopped += OnDirectorStopped;     // Director停止イベントを購読
    }

    void OnDisable()
    {
        if (_director != null) _director.stopped -= OnDirectorStopped;     // Director停止イベントの購読を解除
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TIMELINE ENTRY/EXIT (call these from Signal Receiver)

    public void TL_EndCinematic()
    {
        // Optionally stop the director so it releases any holds
        if (_autoStopDirectorOnEnd && _director != null) _director.Stop(); // Directorを停止してバインドを解放

        // 1) camera cleanup & ensure gameplay cam owns priority
        DoCameraCleanup();  // カメラの後始末を実行
    }

    // Optional: force the main cam mid-sequence if you drop a mid-timeline signal
    public void TL_SwitchToMainCamera()
    {
#if CINEMACHINE
        if (_main25D) _main25D.Priority = 1;    // ゲームプレイカメラに優先度を与えて切り替え
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // DIRECTOR CALLBACK
    void OnDirectorStopped(PlayableDirector d)
    {
        // If the Timeline ends naturally, ensure cleanup ran.
        DoCameraCleanup();  // タイムラインが自然終了した場合も後始末を保証
    }
    // ─────────────────────────────────────────────────────────────────────────────
    // HELPERS — Camera cleanup
    void DoCameraCleanup()
    {
#if CINEMACHINE
        if (_main25D) _main25D.Priority = 1;    // メインゲームプレイカメラを有効な優先度に設定

        if (_animatedCams != null)
        {
            for (int i = 0; i < _animatedCams.Length; i++)
            {
                var vcam = _animatedCams[i];
                if (!vcam) continue;

                vcam.Priority = 0;  // イントロカメラの優先度を0にして無効化

                if (_restoreLensOnEnd && _lensFOVDefaults != null && i < _lensFOVDefaults.Length)
                    vcam.Lens.FieldOfView = _lensFOVDefaults[i];    // タイムライン前のFOVに復元

                var anim = vcam.GetComponent<Animator>();
                if (anim) anim.enabled = false; // カメラのAnimatorを無効化してタイムライン制御を解除

                // If you want them completely off after intro, uncomment:
                vcam.gameObject.SetActive(false);   // イントロ後はカメラオブジェクトを非表示
            }
        }
#endif
    }
}
