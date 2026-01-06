using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class DashCooldownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor _motor;          // NEW: assign in inspector (recommended)
    [SerializeField] private Image _cooldownFill;         // radial image on top of icon
    [SerializeField] private RectTransform _icon;         // dash icon

    [Header("Scales")]
    [SerializeField] private float _readyScale = 1.5f;
    [SerializeField] private float _cooldownScale = 1f;
    [SerializeField] private float _punchScale = 0.3f;
    [SerializeField] private float _punchDuration = 0.2f;

    [Header("Cooldown UI Timing")]
    [SerializeField] private float _delayBeforeFill = 0.4f; // delay measured in SAME units as cooldown seconds

    [Header("Settings")]
    [SerializeField] private bool _useUnscaledTime = true;  // only affects tweens (punch/scale), not cooldown timer

    private Tween _iconTween;

    private bool _cooldownActive;
    private bool _showFill;
    private bool _readyFired;

    private float _cooldownDuration;   // full cooldown seconds at dash start
    private float _uiDuration;         // cooldownDuration - delayBeforeFill

    private void Awake()
    {
        if (_cooldownFill) _cooldownFill.fillAmount = 0f; // 0 = ready
        if (_icon) _icon.localScale = Vector3.one * _readyScale;
    }

    private void OnEnable()
    {
        TryResolveMotor();
        Subscribe(true);

        // If we enabled mid-cooldown (scene load), sync visuals
        SyncFromMotor();
    }

    private void OnDisable()
    {
        Subscribe(false);
        KillTweens();
    }

    private void Update()
    {
        if (_motor == null)
        {
            TryResolveMotor();
            if (_motor == null) return;
        }

        if (!_cooldownActive)
        {
            // keep stable ready visuals
            if (_cooldownFill) _cooldownFill.fillAmount = 0f;
            return;
        }

        float remaining = _motor.DashCooldownRemaining;

        // Delay phase: keep fill at 1 until remaining <= uiDuration
        if (!_showFill)
        {
            float threshold = Mathf.Max(0f, _cooldownDuration - _delayBeforeFill);
            if (remaining <= threshold)
            {
                BeginFillVisuals();
                _showFill = true;
            }
        }

        if (_cooldownFill)
        {
            if (!_showFill)
            {
                _cooldownFill.fillAmount = 1f;
            }
            else
            {
                float uiRemaining = Mathf.Clamp(remaining, 0f, _uiDuration);
                _cooldownFill.fillAmount = (_uiDuration <= 0.01f) ? 0f : (uiRemaining / _uiDuration);
            }
        }

        // Ready
        if (remaining <= 0.001f && !_readyFired)
        {
            _readyFired = true;
            _cooldownActive = false;
            _showFill = false;

            if (_cooldownFill) _cooldownFill.fillAmount = 0f;
            OnCooldownComplete();
        }
    }

    // ─────────────────────────────────────────────────────────────

    private void TryResolveMotor()
    {
        if (_motor != null) return;

        // Best: assign in Inspector. Fallback: find one in scene.
#if UNITY_2023_1_OR_NEWER
        _motor = Object.FindFirstObjectByType<PlayerMotor>();
#else
        _motor = Object.FindObjectOfType<PlayerMotor>();
#endif
    }

    private void Subscribe(bool on)
    {
        if (_motor == null) return;

        if (on) _motor.DashStarted += HandleDashStarted;
        else _motor.DashStarted -= HandleDashStarted;
    }

    private void SyncFromMotor()
    {
        if (_motor == null) return;

        float remaining = _motor.DashCooldownRemaining;
        if (remaining > 0.001f)
        {
            _cooldownActive = true;
            _readyFired = false;

            _cooldownDuration = Mathf.Max(0.01f, _motor.DashCooldownDuration);
            _uiDuration = Mathf.Max(0.01f, _cooldownDuration - _delayBeforeFill);

            // show fill immediately if we are past the delay window already
            float threshold = Mathf.Max(0f, _cooldownDuration - _delayBeforeFill);
            _showFill = remaining <= threshold;

            if (_icon) _icon.localScale = Vector3.one * (_showFill ? _cooldownScale : _readyScale);
            if (_cooldownFill) _cooldownFill.fillAmount = _showFill ? Mathf.Clamp01(remaining / _uiDuration) : 1f;
        }
        else
        {
            _cooldownActive = false;
            _showFill = false;
            _readyFired = false;

            if (_cooldownFill) _cooldownFill.fillAmount = 0f;
            if (_icon) _icon.localScale = Vector3.one * _readyScale;
        }
    }

    private void KillTweens()
    {
        _iconTween?.Kill();
        _iconTween = null;
    }

    private void HandleDashStarted(float cooldownSeconds)
    {
        KillTweens();

        _cooldownActive = true;
        _showFill = false;
        _readyFired = false;

        _cooldownDuration = Mathf.Max(0.01f, cooldownSeconds);
        _uiDuration = Mathf.Max(0.01f, _cooldownDuration - _delayBeforeFill);

        // Start full
        if (_cooldownFill) _cooldownFill.fillAmount = 1f;

        // Start from ready size
        if (_icon) _icon.localScale = Vector3.one * _readyScale;

        // Punch on dash
        if (_icon)
        {
            _iconTween = _icon
                .DOPunchScale(Vector3.one * _punchScale, _punchDuration, 1, 0.5f)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }
    }

    private void BeginFillVisuals()
    {
        // Scale down during cooldown
        if (_icon)
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
        // Grow big when ready
        if (_icon)
        {
            _iconTween?.Kill();
            _iconTween = _icon
                .DOScale(_readyScale, 0.25f)
                .SetEase(Ease.OutBack)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }
    }

    // Optional: let other scripts bind player at runtime (good for DontDestroy UI)
    public void Bind(PlayerMotor motor)
    {
        if (_motor == motor) return;

        Subscribe(false);
        _motor = motor;
        Subscribe(true);

        SyncFromMotor();
    }
}
