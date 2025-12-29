using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;
using MoreMountains.Feedbacks;

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

    [Header("Fill Glow (FEEL)")]
    [SerializeField] private Image _fillImage;
    [SerializeField] private MMFeedbacks _fillGlowBurst; // FB_FillGlowBurst


    [Header("Glow (>= 50%) - FEEL")]
    [SerializeField] private GameObject _glowRoot;       // GlowImage GameObject
    [SerializeField] private CanvasGroup _glowGroup;     // CanvasGroup on GlowImage
    [SerializeField] private MMFeedbacks _glowAppear;    // plays once when enabling
    [SerializeField] private MMFeedbacks _glowBlinkLoop; // loops while enabled
    [SerializeField, Range(0f, 1f)] private float _glowStartPct = 0.5f;

    [Header("Outside Glow (ONLY at 100%) - FEEL")]
    [SerializeField] private GameObject _glowOutsideRoot;        // GlowOutside GameObject
    [SerializeField] private CanvasGroup _glowOutsideGroup;      // CanvasGroup on GlowOutside
    [SerializeField] private MMFeedbacks _glowOutsideAppear;     // plays once when enabling
    [SerializeField] private MMFeedbacks _glowOutsideBlinkLoop;  // loops while enabled
    [SerializeField] private float _maxEpsilon = 0.001f;         // float safety

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
    private bool _glowOn;
    private bool _outsideGlowOn;
    private Color _baseFillColor;
    private void Awake()
    {
        if (_momentumSlider == null) _momentumSlider = GetComponent<Slider>();
        if (_group == null) _group = GetComponent<CanvasGroup>();
        if (_group == null) { _group = gameObject.AddComponent<CanvasGroup>(); } // safe default
        if (_fillImage != null) _baseFillColor = _fillImage.color;
        if (_glowRoot != null) _glowRoot.SetActive(false);
        if (_glowGroup != null) _glowGroup.alpha = 0f;
        if (_glowOutsideRoot != null) _glowOutsideRoot.SetActive(false);
        if (_glowOutsideGroup != null) _glowOutsideGroup.alpha = 0f;
        _glowOn = false;
        _outsideGlowOn = false;
       
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
        if (_fillImage != null) _fillImage.color = _baseFillColor;
        _glowBlinkLoop?.StopFeedbacks();
        _glowAppear?.StopFeedbacks();
        if (_glowGroup != null) _glowGroup.alpha = 0f;
        if (_glowRoot != null) _glowRoot.SetActive(false);
        _glowOutsideBlinkLoop?.StopFeedbacks();
        _glowOutsideAppear?.StopFeedbacks();
        if (_glowOutsideGroup != null) _glowOutsideGroup.alpha = 0f;
        if (_glowOutsideRoot != null) _glowOutsideRoot.SetActive(false);
        _glowOn = false;
        _outsideGlowOn = false;
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

        // detect increase BEFORE _lastValue updates
        bool increased = (_lastValue > -900f) && (target > _lastValue + 0.0001f);

        if (Mathf.Approximately(_lastValue, target)) return;
        _lastValue = target;

        // your existing slider tween
        _valueTween?.Kill();
        _valueTween = _momentumSlider
            .DOValue(target, _tweenDuration)
            .SetEase(_ease)
            .SetUpdate(_animateWhilePaused)
            .SetLink(_momentumSlider.gameObject, LinkBehaviour.KillOnDestroy);

        // FEEL burst on gain
        if (increased)
            _fillGlowBurst?.PlayFeedbacks();

        // FEEL loop from 50%
        float pct = (_momentumSlider.maxValue <= 0f) ? 0f : (target / _momentumSlider.maxValue);
        SetLoopGlow(pct >= _glowStartPct);
        
        bool isMax = (_momentumSlider.maxValue > 0f) && (target >= _momentumSlider.maxValue - _maxEpsilon);
        SetOutsideGlowMaxOnly(isMax);

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

    private void SetLoopGlow(bool enable)
    {
        if (_glowRoot == null) return;

        if (_glowOn == enable) return;
        _glowOn = enable;

        if (enable)
        {
            // show object first
            _glowRoot.SetActive(true);

            // reset alpha to start (so appear always looks correct)
            if (_glowGroup != null) _glowGroup.alpha = 0f;

            // play appear once, then start blink loop
            _glowAppear?.PlayFeedbacks();
            _glowBlinkLoop?.PlayFeedbacks();
        }
        else
        {
            // stop loop + hide
            _glowBlinkLoop?.StopFeedbacks();
            _glowAppear?.StopFeedbacks();

            if (_glowGroup != null) _glowGroup.alpha = 0f;
            _glowRoot.SetActive(false);
        }
    }
    private void SetOutsideGlowMaxOnly(bool enable)
    {
        if (_glowOutsideRoot == null) return;

        if (_outsideGlowOn == enable) return;
        _outsideGlowOn = enable;

        if (enable)
        {
            _glowOutsideRoot.SetActive(true);
            if (_glowOutsideGroup != null) _glowOutsideGroup.alpha = 0f;

            _glowOutsideAppear?.PlayFeedbacks();
            _glowOutsideBlinkLoop?.PlayFeedbacks();
        }
        else
        {
            _glowOutsideBlinkLoop?.StopFeedbacks();
            _glowOutsideAppear?.StopFeedbacks();

            if (_glowOutsideGroup != null) _glowOutsideGroup.alpha = 0f;
            _glowOutsideRoot.SetActive(false);
        }
    }

}
