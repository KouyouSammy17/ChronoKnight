using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class DashCooldownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _cooldownFill;       // radial image on top of icon
    [SerializeField] private RectTransform _icon;       // dash icon

    [Header("Scales")]
    [SerializeField] private float _readyScale = 1.5f;      // big when ready
    [SerializeField] private float _cooldownScale = 1f;     // small while cooling
    [SerializeField] private float _punchScale = 0.3f;      // punch amount
    [SerializeField] private float _punchDuration = 0.2f;   // punch time
    [SerializeField] private float _delayBeforeFill = 0.4f; // wait after punch before fill starts

    [Header("Settings")]
    [SerializeField] private bool _useUnscaledTime = true;  // UI runs even in slow/paused

    private Tween _cooldownTween;
    private Tween _iconTween;
    private Tween _delayTween;

    private void Awake()
    {
        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 0f; // 0 = ready

        if (_icon != null)
            _icon.localScale = Vector3.one * _readyScale;
    }

    private void OnEnable()
    {
        PlayerController.OnDashStarted += HandleDashStarted;
    }

    private void OnDisable()
    {
        PlayerController.OnDashStarted -= HandleDashStarted;
        KillTweens();
    }

    private void KillTweens()
    {
        _cooldownTween?.Kill();
        _cooldownTween = null;

        _iconTween?.Kill();
        _iconTween = null;

        _delayTween?.Kill();
        _delayTween = null;
    }

    private void HandleDashStarted(float cooldownSeconds)
    {
        KillTweens();

        // Make sure icon starts from "ready" size
        if (_icon != null)
            _icon.localScale = Vector3.one * _readyScale;

        // 1) Punch animation on dash
        if (_icon != null)
        {
            _iconTween = _icon
                .DOPunchScale(Vector3.one * _punchScale, _punchDuration, 1, 0.5f)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }

        // 2) After delay, start cooldown (fill + scale down)
        _delayTween = DOVirtual.DelayedCall(_delayBeforeFill, () =>
        {
            StartCooldown(cooldownSeconds);
        })
        .SetUpdate(_useUnscaledTime)
        .SetLink(gameObject);
    }

    private void StartCooldown(float duration)
    {
        // We already waited _delayBeforeFill, so make the UI fill finish
        // at the same time as the real cooldown:
        // delay + uiDuration ≈ duration  => uiDuration = duration - delay
        float uiDuration = Mathf.Max(0.01f, duration - _delayBeforeFill);

        // fill 1 → 0 = cooldown timer
        if (_cooldownFill != null)
        {
            _cooldownFill.fillAmount = 1f;

            _cooldownTween = _cooldownFill
                .DOFillAmount(0f, uiDuration)
                .SetEase(Ease.Linear)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject)
                .OnComplete(OnCooldownComplete);
        }

        // icon becomes small while cooling
        if (_icon != null)
        {
            _iconTween?.Kill();
            _iconTween = _icon
                .DOScale(_cooldownScale, 0.25f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }
    }

    private void OnCooldownComplete()
    {
        // when ready: grow big
        if (_icon != null)
        {
            _iconTween?.Kill();
            _iconTween = _icon
                .DOScale(_readyScale, 0.25f)
                .SetEase(Ease.OutBack)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }
    }
}
