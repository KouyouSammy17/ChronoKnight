using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TurboCooldownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _cooldownFill;         // radial fill image on top
    [SerializeField] private RectTransform _icon;         // ONLY rotates
    [SerializeField] private RectTransform _background;   // frame / ring that scales & punches

    [Header("Cooldown Settings")]
    [SerializeField] private float _cooldownDuration = 6f;
    [SerializeField] private float _rotationSpeed = 180f;   // degrees per second
    [SerializeField] private bool _useUnscaledTime = true;  // UI should ignore timeScale

    [Header("Scale Settings")]
    [SerializeField] private float _lockedScale = 1.0f;     // momentum < 25%
    [SerializeField] private float _readyScale = 1.25f;     // momentum >= 25% and not cooling
    [SerializeField] private float _cooldownScale = 0.8f;   // during cooldown

    [Header("Punch Settings")]
    [SerializeField] private float _punchScale = 0.25f;
    [SerializeField] private float _punchDuration = 0.2f;

    private Tween _cooldownTween;
    private Tween _rotateTween;
    private Tween _bgScaleTween;
    private Tween _bgPunchTween;
    private Tween _lockDelayTween;

    // "Unlocked" = have ≥25% momentum *when idle*
    private bool _unlocked = true;
    private bool _isCoolingDown = false;

    // Tutorial gate: Turbo is not usable until tutorial unlocks it
    private bool _tutorialUnlocked = true;

    // Keep momentum-based unlocked separate (>=25%)
    private bool _momentumUnlocked = true;

    // Effective unlock = tutorial unlocked AND momentum unlocked
    private bool EffectiveUnlocked => _tutorialUnlocked && _momentumUnlocked;


    // Pending lock flag (used to avoid snapping when Turbo starts right after cost)
    private bool _lockPending = false;

    // ───────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 0f; // default ready

        if (_background != null)
            _background.localScale = Vector3.one * _readyScale;
    }

    private void OnEnable()
    {
        // Turbo events
        if (TurboModeManager.Instance != null)
        {
            TurboModeManager.Instance.onTurboStart.AddListener(OnTurboStart);
            TurboModeManager.Instance.onTurboEnd.AddListener(OnTurboEnd);
        }

        // Momentum events
        if (MomentumManager.Instance != null)
        {
            MomentumManager.Instance.onMomentumChanged.AddListener(HandleMomentumChanged);
            InitializeFromMomentum();
        }
        else
        {
            _unlocked = true;
            _isCoolingDown = false;
            if (_cooldownFill != null) _cooldownFill.fillAmount = 0f;
            if (_background != null) _background.localScale = Vector3.one * _readyScale;
        }
    }

    private void OnDisable()
    {
        if (TurboModeManager.Instance != null)
        {
            TurboModeManager.Instance.onTurboStart.RemoveListener(OnTurboStart);
            TurboModeManager.Instance.onTurboEnd.RemoveListener(OnTurboEnd);
        }

        if (MomentumManager.Instance != null)
        {
            MomentumManager.Instance.onMomentumChanged.RemoveListener(HandleMomentumChanged);
        }

        KillTweens();
    }

    private void KillTweens()
    {
        _cooldownTween?.Kill(); _cooldownTween = null;
        _rotateTween?.Kill(); _rotateTween = null;
        _bgScaleTween?.Kill(); _bgScaleTween = null;
        _bgPunchTween?.Kill(); _bgPunchTween = null;
        _lockDelayTween?.Kill(); _lockDelayTween = null;
        _lockPending = false;
    }

    private void ClearPendingLock()
    {
        _lockPending = false;
        if (_lockDelayTween != null)
        {
            _lockDelayTween.Kill();
            _lockDelayTween = null;
        }
    }

    // ───────────────────────────────────────────────────────────────────
    private void InitializeFromMomentum()
    {
        var mm = MomentumManager.Instance;
        if (mm == null)
            return;

        float percent = (mm.MaxMomentum <= 0f)
            ? 0f
            : (mm.CurrentMomentum / mm.MaxMomentum) * 100f;

        _momentumUnlocked = percent >= 25f;
        _unlocked = EffectiveUnlocked; // keep existing var for your cooldown logic
        _isCoolingDown = false;
        KillTweens();

        if (_icon != null)
            _icon.localRotation = Quaternion.identity;

        bool eff = EffectiveUnlocked;

        if (_cooldownFill != null)
            _cooldownFill.fillAmount = eff ? 0f : 1f;

        if (_background != null)
            _background.localScale = Vector3.one * (eff ? _readyScale : _lockedScale);
    }


    private void HandleMomentumChanged(float currentMomentum)
    {
        var mm = MomentumManager.Instance;
        if (mm == null) return;

        float percent = (mm.MaxMomentum <= 0f)
            ? 0f
            : (currentMomentum / mm.MaxMomentum) * 100f;

        bool newMomentumUnlocked = percent >= 25f;
        _momentumUnlocked = newMomentumUnlocked;

        bool newEffective = EffectiveUnlocked;
        if (newEffective == _unlocked) return;

        // Always keep the logical flag updated (this flag now means EFFECTIVE unlock)
        _unlocked = newEffective;

        // IMPORTANT:
        // If we are in the middle of a cooldown animation,
        // DO NOT change visuals here. Just remember the new _unlocked value.
        // OnCooldownComplete() will look at _unlocked and decide:
        //   - !_unlocked → go locked
        //   -  _unlocked → go ready
        if (_isCoolingDown)
            return;

        // Not cooling: normal behavior
        if (_unlocked)
        {
            OnMomentumUnlocked();
        }
        else
        {
            OnMomentumLocked();
        }
    }

    private void OnMomentumUnlocked()
    {
        _isCoolingDown = false;
        ClearPendingLock();

        _cooldownTween?.Kill(); _cooldownTween = null;
        _bgScaleTween?.Kill(); _bgScaleTween = null;
        _bgPunchTween?.Kill(); _bgPunchTween = null;

        if (_background != null)
        {
            _bgScaleTween = _background
                .DOScale(_readyScale, 0.25f)
                .SetEase(Ease.OutBack)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }

        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 0f; // unlocked ⇒ ready
    }

    private void OnMomentumLocked()
    {
        // If turbo is active or the UI is in cooldown,
        // don't snap to locked visuals right now.
        // We'll go locked in OnCooldownComplete() instead.
        bool turboActive = TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;

        if (_isCoolingDown || turboActive)
        {
            // Just update _unlocked; visuals will be handled later.
            return;
        }

        // Here is the tricky case: this is often called RIGHT AFTER Turbo consumed momentum,
        // but BEFORE TurboModeManager sets IsActive and fires onTurboStart.
        // So instead of snapping, schedule a small delayed lock that can be canceled
        // if Turbo actually starts or cooldown starts in this frame.
        ClearPendingLock();
        _lockPending = true;

        _lockDelayTween = DOVirtual.DelayedCall(0.01f, () =>
        {
            _lockDelayTween = null;
            if (!_lockPending) return;

            bool turboNow = TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;
            if (_isCoolingDown || turboNow) return;

            ApplyLockedVisuals();
        })
        .SetUpdate(_useUnscaledTime)
        .SetLink(gameObject);
    }

    private void ApplyLockedVisuals()
    {
        _isCoolingDown = false;
        ClearPendingLock();

        KillTweens();

        if (_icon != null)
            _icon.localRotation = Quaternion.identity;

        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 1f; // locked = full mask

        if (_background != null)
        {
            _bgScaleTween = _background
                .DOScale(_lockedScale, 0.25f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }
    }

    public void SetTutorialUnlocked(bool unlocked)
    {
        if (_tutorialUnlocked == unlocked) return;
        _tutorialUnlocked = unlocked;

        // Re-evaluate visuals immediately (don’t kill cooldown mid animation)
        if (_isCoolingDown) return;

        if (EffectiveUnlocked) OnMomentumUnlocked();
        else ApplyLockedVisuals(); // locked by tutorial OR momentum
    }

    // ───────────────────────────────────────────────────────────────────
    private void OnTurboStart()
    {
        if (!_tutorialUnlocked) return;
        // Once Turbo really starts, we know this is NOT a "idle → locked" case.
        // Cancel any pending lock from the momentum cost.
        ClearPendingLock();

        // We ALWAYS show Turbo use (even if momentum is now <25 because of the cost).
        PlayRotation();
        PlayStartPunch();
    }

    private void OnTurboEnd()
    {
        if (!_tutorialUnlocked) return;
        // Always run cooldown when Turbo ends, even if momentum <25.
        StopRotation();
        StartCooldown();
    }

    private void PlayStartPunch()
    {
        if (_background == null) return;

        _bgPunchTween?.Kill();

        _bgPunchTween = _background
            .DOPunchScale(Vector3.one * _punchScale, _punchDuration, 1, 0.5f)
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject);
    }

    private void StartCooldown()
    {
        if (_cooldownFill == null) return;

        _isCoolingDown = true;
        ClearPendingLock();

        _cooldownTween?.Kill(); _cooldownTween = null;
        _bgScaleTween?.Kill(); _bgScaleTween = null;

        // Fill instantly to 100, then animate down.
        _cooldownFill.fillAmount = 1f;

        // Background to cooldown scale
        if (_background != null)
        {
            _bgScaleTween = _background
                .DOScale(_cooldownScale, 0.2f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }

        _cooldownTween = _cooldownFill
            .DOFillAmount(0f, _cooldownDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject)
            .OnComplete(OnCooldownComplete);
    }

    private void OnCooldownComplete()
    {
        _cooldownTween = null;
        _isCoolingDown = false;

        if (!_unlocked)
        {
            // Case: you used Turbo at exactly 25% → momentum dropped <25
            // or lost momentum during cooldown ⇒ AFTER cooldown ends, go locked.
            ApplyLockedVisuals();
            return;
        }

        // Still have enough momentum ⇒ show ready state.
        if (_background != null)
        {
            _bgScaleTween?.Kill();
            _bgScaleTween = _background
                .DOScale(_readyScale, 0.25f)
                .SetEase(Ease.OutBack)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }

        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 0f;
    }

    // ───────────────────────────────────────────────────────────────────
    private void PlayRotation()
    {
        if (_icon == null) return;

        _rotateTween?.Kill();

        _rotateTween = _icon
            .DORotate(new Vector3(0, 0, -360f), 360f / _rotationSpeed, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetSpeedBased()
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject);
    }

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
