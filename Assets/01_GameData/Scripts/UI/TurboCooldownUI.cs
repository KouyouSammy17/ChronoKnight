using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TurboCooldownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _cooldownFill;   // radial fill image INSIDE your _root frame
    [SerializeField] private RectTransform _icon;   // the icon that should rotate

    [Header("Settings")]
    [SerializeField] private float _cooldownDuration = 6f;
    [SerializeField] private float _rotationSpeed = 180f; // degrees per second
    [SerializeField] private bool _useUnscaledTime = true; // keep speed during slow-mo

    private Tween _cooldownTween;
    private Tween _rotateTween;

    private void Awake()
    {
        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 0f; // start empty (ready)
    }

    private void OnEnable()
    {
        // Subscribe to TurboManager events (no Inspector needed)
        if (TurboModeManager.Instance != null)
        {
            TurboModeManager.Instance.onTurboStart.AddListener(OnTurboStart);
            TurboModeManager.Instance.onTurboEnd.AddListener(OnTurboEnd);
        }
    }

    private void OnDisable()
    {
        // Unsubscribe to avoid leaks / errors on scene changes
        if (TurboModeManager.Instance != null)
        {
            TurboModeManager.Instance.onTurboStart.RemoveListener(OnTurboStart);
            TurboModeManager.Instance.onTurboEnd.RemoveListener(OnTurboEnd);
        }
    }

    private void OnTurboStart()
    {
        // Turbo pressed: start spinning icon
        PlayRotation();
    }

    private void OnTurboEnd()
    {
        // Turbo ended: stop spin and start cooldown fill animation
        StopRotation();
        PlayCooldown();
    }

    /// <summary>
    /// Call this when Turbo is on cooldown (here: from OnTurboEnd).
    /// </summary>
    public void PlayCooldown()
    {
        if (_cooldownFill == null) return;

        _cooldownTween?.Kill();

        // Instantly set to full
        _cooldownFill.fillAmount = 1f;

        // Tween from 1 Å® 0 over cooldownDuration
        _cooldownTween = _cooldownFill
            .DOFillAmount(0f, _cooldownDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject);
    }

    /// <summary>
    /// Starts continuous rotation of the icon.
    /// </summary>
    public void PlayRotation()
    {
        if (_icon == null) return;

        _rotateTween?.Kill();

        // Rotate continuously with speed-based mode
        _rotateTween = _icon
            .DORotate(new Vector3(0, 0, -360f), 360f / _rotationSpeed, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetSpeedBased()
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject);
    }

    /// <summary>
    /// Stops the rotation and resets the icon.
    /// </summary>
    public void StopRotation()
    {
        if (_rotateTween != null)
        {
            _rotateTween.Kill();
            _rotateTween = null;
        }

        if (_icon != null)
            _icon.localRotation = Quaternion.identity;
    }
}
