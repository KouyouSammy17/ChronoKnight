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

    // ¥ add this
    [Header("Visibility (optional)")]
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private bool _startHidden = false;
    [SerializeField] private float _showHideDuration = 0.25f;

    [Header("Show Animation (Scan-in)")]
    [SerializeField] private RectTransform _root;   // gauge root rect (usually this transform)
    [SerializeField] private bool _useScanIn = true;
    [SerializeField] private float _fromScale = 0.90f;
    [SerializeField] private float _overshootScale = 1.03f;

    [SerializeField] private bool _useSlide = false;
    [SerializeField] private float _slideY = 18f;   // pixels

    [Header("Glow Pulse (>= 50%)")]
    [SerializeField] private Image _glowImage;           // behind the fill
    [SerializeField] private float _glowMinAlpha = 0.25f;
    [SerializeField] private float _glowMaxAlpha = 0.75f;
    [SerializeField] private float _glowPulseTime = 0.9f;

    [Header("Tutorial Highlight")]
    [SerializeField] private GameObject _highlightRoot;      // the highlight Image object
    [SerializeField] private UIHighlightPulse _highlightPulse;

    private Tween _valueTween;
    private float _lastValue = -999f;
    private bool _isBound = false;
    private CancellationTokenSource _cts;
    private Sequence _showSeq;
    private Vector3 _baseScale;
    private Vector2 _baseAnchoredPos;
    private Tween _glowTween;
    private bool _glowActive;
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

        if (_root == null) _root = transform as RectTransform;
        _baseScale = _root.localScale;
        _baseAnchoredPos = _root.anchoredPosition;
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
        _showSeq?.Kill();
        _showSeq = null;
    }

    private void OnDestroy()
    {
        Unbind();
        _valueTween?.Kill(); _valueTween = null;
        _showSeq?.Kill();
        _showSeq = null;
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

        float pct = (_momentumSlider.maxValue <= 0f) ? 0f : target / _momentumSlider.maxValue;
        SetGlowActive(pct >= 0.5f);

        _valueTween?.Kill();
        _valueTween = _momentumSlider
            .DOValue(target, _tweenDuration)
            .SetEase(_ease)
            .SetUpdate(_animateWhilePaused)
            .SetLink(_momentumSlider.gameObject, LinkBehaviour.KillOnDestroy);
    }

    // „Ÿ„Ÿ Timeline-callable helpers „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    public void TL_HideGauge() => SetVisible(false, instant: false);
    public void TL_ShowGauge() => SetVisible(true, instant: false);

    private void SetVisible(bool visible, bool instant)
    {
        if (_group == null)
        {
            gameObject.SetActive(visible);
            return;
        }

        // stop previous show animation
        _showSeq?.Kill();
        _showSeq = null;

        bool useUnscaled = _animateWhilePaused; // respect your setting

        if (visible)
        {
            // IMPORTANT: must be active to render + run tweens
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (instant || !Application.isPlaying)
            {
                _group.alpha = 1f;
                _group.interactable = true;
                _group.blocksRaycasts = true;

                if (_root != null)
                {
                    _root.localScale = _baseScale;
                    _root.anchoredPosition = _baseAnchoredPos;
                }
                return;
            }

            _group.alpha = 0f;
            _group.interactable = true;
            _group.blocksRaycasts = true;

            if (_root == null || !_useScanIn)
            {
                _group.DOFade(1f, _showHideDuration).SetUpdate(useUnscaled);
                return;
            }

            // start pose
            _root.localScale = _baseScale * _fromScale;
            if (_useSlide)
                _root.anchoredPosition = _baseAnchoredPos - new Vector2(0f, _slideY);

            _showSeq = DOTween.Sequence().SetUpdate(useUnscaled);
            _showSeq.Join(_group.DOFade(1f, _showHideDuration).SetEase(Ease.OutQuad));
            _showSeq.Join(_root.DOScale(_baseScale * _overshootScale, _showHideDuration).SetEase(Ease.OutBack));
            _showSeq.Append(_root.DOScale(_baseScale, 0.10f).SetEase(Ease.OutQuad));

            if (_useSlide)
                _showSeq.Join(_root.DOAnchorPos(_baseAnchoredPos, _showHideDuration + 0.08f).SetEase(Ease.OutCubic));

            _showSeq.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            return;
        }

        // HIDE
        if (instant || !Application.isPlaying)
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            // optional: disable instantly
            // gameObject.SetActive(false);
            return;
        }

        _group.interactable = false;
        _group.blocksRaycasts = false;

        _group.DOFade(0f, _showHideDuration)
              .SetUpdate(useUnscaled)
              .OnComplete(() =>
              {
                  // optional: turn off object after fade
                  // gameObject.SetActive(false);
              });
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

    private void SetGlowActive(bool active)
    {
        if (_glowImage == null) return;
        if (_glowActive == active) return;
        _glowActive = active;

        _glowTween?.Kill();
        _glowTween = null;

        var c = _glowImage.color;

        if (!active)
        {
            c.a = 0f;
            _glowImage.color = c;
            return;
        }

        // start visible
        c.a = _glowMinAlpha;
        _glowImage.color = c;

        _glowTween = DOTween.To(
                () => _glowImage.color.a,
                a => { var cc = _glowImage.color; cc.a = a; _glowImage.color = cc; },
                _glowMaxAlpha,
                _glowPulseTime)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(_animateWhilePaused)
            .SetLink(_glowImage.gameObject, LinkBehaviour.KillOnDestroy);
    }

}
    