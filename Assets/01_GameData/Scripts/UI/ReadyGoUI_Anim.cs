using DG.Tweening;
using UnityEngine;

public class ReadyGoUI_Anim : MonoBehaviour
{
    [Header("Root (optional)")]
    [SerializeField] private CanvasGroup _rootGroup;

    [Header("Groups")]
    [SerializeField] private RectTransform _readyRoot;   // Ready
    [SerializeField] private CanvasGroup _readyGroup;
    [SerializeField] private RectTransform _goRoot;      // Go!
    [SerializeField] private CanvasGroup _goGroup;

    [Header("Optional Dim")]
    [SerializeField] private CanvasGroup _dimGroup;       // fullscreen Image+CanvasGroup (RaycastTarget OFF)
    [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.25f;

    [Header("READY Slide")]
    [SerializeField] private float _readyFromX = -700f;   // slide-in offset from center
    [SerializeField] private float _readyIn = 0.18f;
    [SerializeField] private float _readyHold = 0.28f;
    [SerializeField] private float _readyOut = 0.10f;

    [Header("GO Pop")]
    [SerializeField] private float _goIn = 0.12f;
    [SerializeField] private float _goHold = 0.22f;
    [SerializeField] private float _goOut = 0.16f;

    [Header("Scale")]
    [SerializeField] private float _readyStartScale = 0.95f;
    [SerializeField] private float _readyPeakScale = 1.06f;
    [SerializeField] private float _goStartScale = 0.75f;
    [SerializeField] private float _goPeakScale = 1.20f;

    private Sequence _seq;
    private Vector2 _readyCenterPos;
    private Vector2 _goCenterPos;

    public float TotalDuration => _readyIn + _readyHold + _readyOut + _goIn + _goHold + _goOut;

    private void Reset()
    {
        _rootGroup = GetComponent<CanvasGroup>();
        var tReady = transform.Find("Ready");
        if (tReady) { _readyRoot = tReady as RectTransform; _readyGroup = tReady.GetComponent<CanvasGroup>(); }
        var tGo = transform.Find("Go!");
        if (tGo) { _goRoot = tGo as RectTransform; _goGroup = tGo.GetComponent<CanvasGroup>(); }
    }

    private void Awake()
    {
        if (_rootGroup == null) _rootGroup = GetComponent<CanvasGroup>();
        CacheCenters();
        HardHide();
    }

    private void CacheCenters()
    {
        if (_readyRoot != null) _readyCenterPos = _readyRoot.anchoredPosition;
        if (_goRoot != null) _goCenterPos = _goRoot.anchoredPosition;
    }

    public void HardHide()
    {
        _seq?.Kill();

        if (_dimGroup != null) _dimGroup.alpha = 0f;

        if (_rootGroup != null) _rootGroup.alpha = 0f;

        if (_readyGroup != null) _readyGroup.alpha = 0f;
        if (_readyRoot != null)
        {
            _readyRoot.localScale = Vector3.one;
            _readyRoot.anchoredPosition = _readyCenterPos;
            _readyRoot.gameObject.SetActive(false);
        }

        if (_goGroup != null) _goGroup.alpha = 0f;
        if (_goRoot != null)
        {
            _goRoot.localScale = Vector3.one;
            _goRoot.anchoredPosition = _goCenterPos;
            _goRoot.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
    }

    public void Play()
    {
        _seq?.Kill();
        CacheCenters();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (_rootGroup != null) _rootGroup.alpha = 1f;

        // init
        if (_dimGroup != null) _dimGroup.alpha = 0f;

        _readyRoot.gameObject.SetActive(true);
        _goRoot.gameObject.SetActive(false);

        _readyGroup.alpha = 0f;
        _readyRoot.localScale = Vector3.one * _readyStartScale;
        _readyRoot.anchoredPosition = _readyCenterPos + new Vector2(_readyFromX, 0f);

        _goGroup.alpha = 0f;
        _goRoot.localScale = Vector3.one * _goStartScale;
        _goRoot.anchoredPosition = _goCenterPos;

        _seq = DOTween.Sequence()
            .SetUpdate(true) // unscaled
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // optional dim in (subtle)
        if (_dimGroup != null)
            _seq.Join(_dimGroup.DOFade(_dimAlpha, 0.12f).SetEase(Ease.OutQuad).SetUpdate(true));

        // READY: slide to center + fade in + tiny scale hit
        _seq.Append(_readyGroup.DOFade(1f, _readyIn).SetEase(Ease.OutQuad));
        _seq.Join(_readyRoot.DOAnchorPos(_readyCenterPos, _readyIn).SetEase(Ease.OutCubic));
        _seq.Join(_readyRoot.DOScale(_readyPeakScale, _readyIn).SetEase(Ease.OutBack));

        _seq.AppendInterval(_readyHold);

        // READY out
        _seq.Append(_readyGroup.DOFade(0f, _readyOut).SetEase(Ease.InQuad));
        _seq.Join(_readyRoot.DOScale(_readyStartScale, _readyOut).SetEase(Ease.InQuad));

        // switch to GO
        _seq.AppendCallback(() =>
        {
            _readyRoot.gameObject.SetActive(false);
            _goRoot.gameObject.SetActive(true);

            _goGroup.alpha = 0f;
            _goRoot.localScale = Vector3.one * _goStartScale;
        });

        // GO: pop in at center
        _seq.Append(_goGroup.DOFade(1f, _goIn).SetEase(Ease.OutQuad));
        _seq.Join(_goRoot.DOScale(_goPeakScale, _goIn).SetEase(Ease.OutBack));

        _seq.AppendInterval(_goHold);

        // GO out
        _seq.Append(_goGroup.DOFade(0f, _goOut).SetEase(Ease.InQuad));
        _seq.Join(_goRoot.DOScale(1f, _goOut).SetEase(Ease.InQuad));

        // dim out
        if (_dimGroup != null)
            _seq.Join(_dimGroup.DOFade(0f, 0.16f).SetEase(Ease.InQuad).SetUpdate(true));

        _seq.OnComplete(HardHide);
        _seq.Play();
    }
}
