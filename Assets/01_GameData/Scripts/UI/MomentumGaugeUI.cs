using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

public class MomentumGaugeUI : MonoBehaviour
{
    [SerializeField] private Slider _momentumSlider;
    [SerializeField] private float _tweenDuration = 0.3f;
    [SerializeField] private Ease _ease = Ease.OutQuad;
    [SerializeField] private bool _animateWhilePaused = true;

    // Å• add this
    [Header("Visibility (optional)")]
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private bool _startHidden = false;
    [SerializeField] private float _showHideDuration = 0.25f;


    [Header("Tutorial Highlight")]
    [SerializeField] private GameObject _highlightRoot;      // the highlight Image object
    [SerializeField] private UIHighlightPulse _highlightPulse;

    private Tween _valueTween;
    private float _lastValue = -999f;
    private bool _isBound = false;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (_momentumSlider == null) _momentumSlider = GetComponent<Slider>();
        if (_group == null) _group = GetComponent<CanvasGroup>();
        if (_group == null) { _group = gameObject.AddComponent<CanvasGroup>(); } // safe default

        if (_startHidden)
            SetVisible(false, instant: true);

        // make sure highlight starts off
        if (_highlightRoot != null)
            _highlightRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindWhenReadyAsync(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;
        Unbind();
        _valueTween?.Kill(); _valueTween = null;
    }

    private void OnDestroy()
    {
        Unbind();
        _valueTween?.Kill(); _valueTween = null;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        BindWhenReadyAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid BindWhenReadyAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() => MomentumManager.Instance != null && _momentumSlider != null, cancellationToken: token);
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        if (token.IsCancellationRequested) return;

        var mm = MomentumManager.Instance;
        if (mm == null) return;

        Unbind();
        _momentumSlider.wholeNumbers = false;
        _momentumSlider.minValue = 0f;
        _momentumSlider.maxValue = mm.MaxMomentum;

        mm.onMomentumChanged.AddListener(OnMomentumChanged);
        _isBound = true;

        _lastValue = -999f;
        OnMomentumChanged(mm.CurrentMomentum);
    }

    private void Unbind()
    {
        if (_isBound && MomentumManager.Instance != null)
            MomentumManager.Instance.onMomentumChanged.RemoveListener(OnMomentumChanged);
        _isBound = false;
    }

    private void OnMomentumChanged(float m)
    {
        if (_momentumSlider == null) return;

        float target = Mathf.Clamp(m, 0f, _momentumSlider.maxValue);
        if (Mathf.Approximately(_lastValue, target)) return;
        _lastValue = target;

        _valueTween?.Kill();
        _valueTween = _momentumSlider
            .DOValue(target, _tweenDuration)
            .SetEase(_ease)
            .SetUpdate(_animateWhilePaused)
            .SetLink(_momentumSlider.gameObject, LinkBehaviour.KillOnDestroy);
    }

    // ÑüÑü Timeline-callable helpers ÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑüÑü
    public void TL_HideGauge() => SetVisible(false, instant: false);
    public void TL_ShowGauge() => SetVisible(true, instant: false);

    private void SetVisible(bool visible, bool instant)
    {
        if (_group == null)
        {
            gameObject.SetActive(visible);
            return;
        }

        if (instant || !Application.isPlaying)
        {
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }
        else
        {
            _group
                .DOFade(visible ? 1f : 0f, _showHideDuration)
                .SetUpdate(true); // works even when Time.timeScale = 0
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }
    }

    public void ShowTutorialHighlight(bool show)
    {
        if (_highlightRoot == null) return;

        _highlightRoot.SetActive(show);

        if (!show && _highlightPulse != null)
        {
            _highlightPulse.StopPulse();
        }
    }
}
    