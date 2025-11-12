using UnityEngine;
using UnityEngine.Playables;

#if CINEMACHINE
using Unity.Cinemachine;
#endif

public class TimelineCameraCleanup : MonoBehaviour
{
    [Header("Director (the Timeline that plays your intro)")]
    [SerializeField] PlayableDirector _director;
    [SerializeField] bool _autoStopDirectorOnEnd = true;

#if CINEMACHINE
    [Header("Cinemachine")]
    [SerializeField] CinemachineCamera _main25D;             // gameplay cam
    [SerializeField] CinemachineCamera[] _animatedCams;      // intro/orbit cams animated by Timeline
    [SerializeField] bool _restoreLensOnEnd = true;
    float[] _lensFOVDefaults;
#endif

    // ─────────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (_director) _director.extrapolationMode = DirectorWrapMode.None;

#if CINEMACHINE
        if (_animatedCams != null && _animatedCams.Length > 0)
        {
            _lensFOVDefaults = new float[_animatedCams.Length];
            for (int i = 0; i < _animatedCams.Length; i++)
                if (_animatedCams[i]) _lensFOVDefaults[i] = _animatedCams[i].Lens.FieldOfView;
        }
#endif
    }

    void OnEnable()
    {
        if (_director != null) _director.stopped += OnDirectorStopped;
    }

    void OnDisable()
    {
        if (_director != null) _director.stopped -= OnDirectorStopped;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TIMELINE ENTRY/EXIT (call these from Signal Receiver)
  
    public void TL_EndCinematic()
    {
        // Optionally stop the director so it releases any holds
        if (_autoStopDirectorOnEnd && _director != null) _director.Stop();

        // 1) camera cleanup & ensure gameplay cam owns priority
        DoCameraCleanup();
    }

    // Optional: force the main cam mid-sequence if you drop a mid-timeline signal
    public void TL_SwitchToMainCamera()
    {
#if CINEMACHINE
        if (_main25D) _main25D.Priority = 100;
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // DIRECTOR CALLBACK
    void OnDirectorStopped(PlayableDirector d)
    {
        // If the Timeline ends naturally, ensure cleanup ran.
        DoCameraCleanup();
    }
    // ─────────────────────────────────────────────────────────────────────────────
    // HELPERS — Camera cleanup
    void DoCameraCleanup()
    {
#if CINEMACHINE
        if (_main25D) _main25D.Priority = 100;

        if (_animatedCams != null)
        {
            for (int i = 0; i < _animatedCams.Length; i++)
            {
                var vcam = _animatedCams[i];
                if (!vcam) continue;

                vcam.Priority = 0;

                if (_restoreLensOnEnd && _lensFOVDefaults != null && i < _lensFOVDefaults.Length)
                    vcam.Lens.FieldOfView = _lensFOVDefaults[i];

                var anim = vcam.GetComponent<Animator>();
                if (anim) anim.enabled = false;

                // If you want them completely off after intro, uncomment:
                vcam.gameObject.SetActive(false);
            }
        }
#endif
    }
}
