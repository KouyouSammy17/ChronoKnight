using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

#if CINEMACHINE
using Unity.Cinemachine;
#endif

public class TimelineCameraCleanup : MonoBehaviour
{
    [Header("Director (the Timeline that plays your intro)")]
    [SerializeField] PlayableDirector _director;
    [SerializeField] bool _autoStopDirectorOnEnd = true;

    [Header("Player/Input")]
    [SerializeField] PlayerController _player;   // has EnableInput/DisableInput
    [SerializeField] PlayerInput _playerInput;   // optional (new Input System)
    [SerializeField] string _playerActionMap = "Player";
    [SerializeField] string _uiActionMap = "UI";

    [Header("UI")]
    [SerializeField] private MomentumGaugeUI _gauge;
    [SerializeField] bool _showMoveTutorialOnEnd = false; // optional
    [SerializeField] GameObject[] _extraGOToHide;         // any extra UI roots to hide during intro

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
    public void TL_BeginCinematic()
    {
        // 1) lock input + swap to UI map (so skip/menu keys can still work)
        SetInputEnabled(false);
        SwitchActionMap(_uiActionMap);

        // 2) hide gameplay UI (HUD, tutorials, gauge, extras)
        SetGameplayUIVisible(false);
    }

    public void TL_EndCinematic()
    {
        // Optionally stop the director so it releases any holds
        if (_autoStopDirectorOnEnd && _director != null) _director.Stop();

        // 1) camera cleanup & ensure gameplay cam owns priority
        DoCameraCleanup();

        // 2) show gameplay UI again
        SetGameplayUIVisible(true);

        // 3) back to gameplay input
        SwitchActionMap(_playerActionMap);
        SetInputEnabled(true);
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
    // HELPERS — Input
    void SetInputEnabled(bool enabled)
    {
        if (_player != null)
        {
            if (enabled) _player.EnableInput();
            else _player.DisableInput();
        }

        if (_playerInput != null)
        {
            if (!_playerInput.enabled) _playerInput.enabled = true; // keep component live
            if (_playerInput.actions != null && !_playerInput.actions.enabled) _playerInput.actions.Enable();
        }
    }

    void SwitchActionMap(string map)
    {
        if (string.IsNullOrEmpty(map) || _playerInput == null || _playerInput.actions == null) return;
        var found = _playerInput.actions.FindActionMap(map, false);
        if (found != null)
        {
            _playerInput.defaultActionMap = map;
            _playerInput.SwitchCurrentActionMap(map);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HELPERS — UI
    void SetGameplayUIVisible(bool visible)
    {
        // HUD on/off
        UIManager.Instance?.ShowPlayerUI(visible);

        // Tutorials
        if (!visible)
        {
            UIManager.Instance?.HideAllTutorials();
        }
        else if (_showMoveTutorialOnEnd)
        {
            UIManager.Instance?.ShowTutorial(TutorialKey.Move);
        }

        // Momentum gauge (explicit reference, with fallback)
        var gauge = _gauge ? _gauge : GetComponentInChildren<MomentumGaugeUI>(true);
        if (gauge != null)
        {
            if (visible) gauge.TL_ShowGauge();
            else gauge.TL_HideGauge();
        }


        // Any extra UI roots (separate canvases, etc.)
        if (_extraGOToHide != null)
        {
            foreach (var go in _extraGOToHide)
                if (go) go.SetActive(visible);
        }
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
