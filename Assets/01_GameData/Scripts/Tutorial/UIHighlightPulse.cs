using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UIHighlightPulse : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform _target; // frame RectTransform
    [SerializeField] private Graphic _graphic;      // Image, etc.

    [Header("Pulse Settings")]
    [SerializeField] private float _scaleMultiplier = 1.06f;
    [SerializeField] private float _duration = 0.6f;
    [SerializeField] private float _minAlpha = 0.65f;
    [SerializeField] private float _maxAlpha = 1.0f;

    [Header("Time")]
    [SerializeField] private bool _ignoreTimeScale = true;

    private Sequence _seq;
    private Vector3 _baseScale;
    private Color _baseColor;

    private void Awake()
    {
        if (_target == null) _target = transform as RectTransform;
        if (_graphic == null) _graphic = GetComponent<Graphic>();

        _baseScale = _target.localScale;
        _baseColor = _graphic.color;
    }

    private void OnEnable()
    {
        PlayPulse();
    }

    private void OnDisable()
    {
        KillSequence();

        // reset
        if (_target != null) _target.localScale = _baseScale;
        if (_graphic != null) _graphic.color = _baseColor;
    }

    public void PlayPulse()
    {
        if (_graphic == null || _target == null) return;

        KillSequence();

        Color cMin = _baseColor; cMin.a = _minAlpha;
        Color cMax = _baseColor; cMax.a = _maxAlpha;

        _graphic.color = cMin;
        _target.localScale = _baseScale;

        _seq = DOTween.Sequence();
        if (_ignoreTimeScale) _seq.SetUpdate(true); // works when Time.timeScale = 0

        _seq
            .Join(_target.DOScale(_baseScale * _scaleMultiplier, _duration)
                          .SetEase(Ease.OutQuad))
            .Join(_graphic.DOColor(cMax, _duration))
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void StopPulse()
    {
        KillSequence();

        if (_target != null) _target.localScale = _baseScale;
        if (_graphic != null) _graphic.color = _baseColor;
    }

    private void KillSequence()
    {
        if (_seq != null)
        {
            _seq.Kill();
            _seq = null;
        }
    }
}
