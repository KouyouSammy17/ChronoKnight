using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using MoreMountains.Feedbacks;

public class MomentumTutorialUI : MonoBehaviour
{
    [Header("Slider & Value Text")]
    [SerializeField] private Slider _momentumSlider;
    [SerializeField] private TMP_Text _valueText;

    [Header("Slider Animation (stepped)")]
    [SerializeField] private float _segmentDuration = 0.6f;      // 0→25, 25→50 etc
    [SerializeField] private float _segmentPauseTime = 0.3f;     // small stop at 25,50,75
    [SerializeField] private float _holdAtMaxSliderTime = 1.0f;  // wait at 100 before restart
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("Value Display")]
    [SerializeField] private bool _showAsPercent = true;
    [SerializeField] private int _decimals = 0;

    // ───────────────────────────── Tier popup data

    [System.Serializable]
    public class PopupEntry
    {
        public RectTransform root;   // parent (icon + text)
        public CanvasGroup canvasGroup;

        [HideInInspector] public Vector2 startPos;
        [HideInInspector] public Vector3 startScale;
    }

    [System.Serializable]
    public class TierPopup
    {
        public PopupEntry[] entries;      // 1 or 2 buffs
        [HideInInspector] public Sequence seq;
    }

    [Header("Tier Popups")]
    [SerializeField] private TierPopup _tier25Popup;
    [SerializeField] private TierPopup _tier50Popup;
    [SerializeField] private TierPopup _tier75Popup;
    [SerializeField] private TierPopup _tier100Popup;

    [Header("Popup Animation")]
    [SerializeField] private float _popupStartScale = 0.7f;
    [SerializeField] private float _popupPopScale = 1.1f;
    [SerializeField] private float _popupPopTime = 0.2f;
    [SerializeField] private float _popupRiseDistance = 40f;
    [SerializeField] private float _popupRiseTime = 0.4f;
    [SerializeField] private float _popupHoldTime = 0.3f;
    [SerializeField] private float _popupFadeOutTime = 0.25f;

    [Header("FEEL Feedbacks (Gauge FX)")]
    [SerializeField] private MMF_Player _tier25GaugeFx;
    [SerializeField] private MMF_Player _tier50GaugeFx;
    [SerializeField] private MMF_Player _tier75GaugeFx;
    [SerializeField] private MMF_Player _tier100GaugeFx;

    // ───────────────────────────── internal state

    private Sequence _loopSequence;
    private float _lastPercent = 0f;

    private bool _tier25Triggered;
    private bool _tier50Triggered;
    private bool _tier75Triggered;
    private bool _tier100Triggered;

    // ───────────────────────────── Unity

    private void Awake()
    {
        if (_momentumSlider == null)
            _momentumSlider = GetComponentInChildren<Slider>();

        SetupTier(_tier25Popup);
        SetupTier(_tier50Popup);
        SetupTier(_tier75Popup);
        SetupTier(_tier100Popup);
    }

    private void OnEnable()
    {
        StartLoop();
    }

    private void OnDisable()
    {
        StopLoop();
    }

    // ───────────────────────────── setup

    private void SetupTier(TierPopup tier)
    {
        if (tier == null || tier.entries == null) return;

        foreach (var e in tier.entries)
        {
            if (e == null || e.root == null) continue;

            e.startPos = e.root.anchoredPosition;
            e.startScale = e.root.localScale;

            if (e.canvasGroup == null)
                e.canvasGroup = e.root.GetComponent<CanvasGroup>();

            if (e.canvasGroup != null)
                e.canvasGroup.alpha = 0f;

            e.root.gameObject.SetActive(false);
        }
    }

    // ───────────────────────────── slider loop with steps

    private void StartLoop()
    {
        if (_momentumSlider == null) return;

        _momentumSlider.minValue = 0f;
        _momentumSlider.maxValue = 100f;
        _momentumSlider.wholeNumbers = true;

        _momentumSlider.value = 0f;
        _lastPercent = 0f;
        ResetTierFlags();
        UpdateValueText();

        _loopSequence?.Kill();

        _loopSequence = DOTween.Sequence();

        // 0 → 25
        _loopSequence.Append(CreateSegmentTween(25f));
        _loopSequence.AppendInterval(_segmentPauseTime);

        // 25 → 50
        _loopSequence.Append(CreateSegmentTween(50f));
        _loopSequence.AppendInterval(_segmentPauseTime);

        // 50 → 75
        _loopSequence.Append(CreateSegmentTween(75f));
        _loopSequence.AppendInterval(_segmentPauseTime);

        // 75 → 100
        _loopSequence.Append(CreateSegmentTween(100f));
        _loopSequence.AppendInterval(_holdAtMaxSliderTime); // stay at max

        _loopSequence.SetLoops(-1, LoopType.Restart);
        _loopSequence.OnStepComplete(OnLoopStepComplete);

        if (_useUnscaledTime)
            _loopSequence.SetUpdate(true);
    }

    private Tween CreateSegmentTween(float targetPercent)
    {
        return _momentumSlider
            .DOValue(targetPercent, _segmentDuration)
            .SetEase(Ease.Linear)
            .OnUpdate(OnSliderUpdated);
    }

    private void StopLoop()
    {
        if (_loopSequence != null && _loopSequence.IsActive())
            _loopSequence.Kill();

        KillTier(_tier25Popup);
        KillTier(_tier50Popup);
        KillTier(_tier75Popup);
        KillTier(_tier100Popup);
    }

    private void OnLoopStepComplete()
    {
        // Called after 0→25→50→75→100 + pauses + hold-at-max finishes
        ResetTierFlags();

        _momentumSlider.value = 0f;
        _lastPercent = 0f;
        UpdateValueText();
    }

    // ───────────────────────────── slider update

    private void OnSliderUpdated()
    {
        UpdateValueText();

        float currentPercent = _momentumSlider.value;

        CheckTier(25f, ref _tier25Triggered, _tier25Popup);
        CheckTier(50f, ref _tier50Triggered, _tier50Popup);
        CheckTier(75f, ref _tier75Triggered, _tier75Popup);
        CheckTier(100f, ref _tier100Triggered, _tier100Popup);

        _lastPercent = currentPercent;
    }

    private void UpdateValueText()
    {
        if (_valueText == null || _momentumSlider == null) return;

        float current = _momentumSlider.value;
        float max = _momentumSlider.maxValue;

        if (_showAsPercent)
        {
            float percent = (max > 0f) ? (current / max) * 100f : 0f;
            _valueText.text = $"{percent.ToString($"F{_decimals}")}%";
        }
        else
        {
            _valueText.text = current.ToString($"F{_decimals}");
        }
    }

    // ───────────────────────────── tier trigger

    private void CheckTier(float threshold, ref bool flag, TierPopup tier)
    {
        if (tier == null) return;

        float currentPercent = _momentumSlider.value;

        if (!flag && _lastPercent < threshold && currentPercent >= threshold)
        {
            flag = true;
            
            // buff popups 
            PlayTier(tier);

            // NEW: gauge effects
            PlayGaugeFxForThreshold(threshold);
        }
    }

    private void ResetTierFlags()
    {
        _tier25Triggered = false;
        _tier50Triggered = false;
        _tier75Triggered = false;
        _tier100Triggered = false;
        _lastPercent = 0f;
    }

    // ───────────────────────────── popup animation

    private void PlayTier(TierPopup tier)
    {
        if (tier.entries == null || tier.entries.Length == 0) return;

        KillTier(tier);

        var seq = DOTween.Sequence();

        // entries are appended → play one by one
        foreach (var e in tier.entries)
        {
            if (e == null || e.root == null) continue;
            seq.Append(CreateEntrySequence(e));
        }

        if (_useUnscaledTime)
            seq.SetUpdate(true);

        tier.seq = seq;
    }

    private Sequence CreateEntrySequence(PopupEntry e)
    {
        var s = DOTween.Sequence();

        s.AppendCallback(() =>
        {
            e.root.gameObject.SetActive(true);
            e.root.anchoredPosition = e.startPos;
            e.root.localScale = e.startScale * _popupStartScale;

            if (e.canvasGroup != null)
                e.canvasGroup.alpha = 0f;
        });

        // POP
        if (e.canvasGroup != null)
            s.Append(e.canvasGroup.DOFade(1f, _popupPopTime));
        else
            s.AppendInterval(_popupPopTime);

        s.Join(e.root
            .DOScale(e.startScale * _popupPopScale, _popupPopTime)
            .SetEase(Ease.OutBack));

        // RISE
        s.Append(e.root
            .DOAnchorPos(e.startPos + Vector2.up * _popupRiseDistance, _popupRiseTime)
            .SetEase(Ease.OutQuad));
        s.Join(e.root
            .DOScale(e.startScale, _popupRiseTime)
            .SetEase(Ease.OutQuad));

        // HOLD
        s.AppendInterval(_popupHoldTime);

        // FADE OUT
        if (e.canvasGroup != null)
            s.Append(e.canvasGroup.DOFade(0f, _popupFadeOutTime));
        else
            s.AppendInterval(_popupFadeOutTime);

        s.OnComplete(() =>
        {
            if (e.root != null)
                e.root.gameObject.SetActive(false);
        });

        return s;
    }

    private void KillTier(TierPopup tier)
    {
        if (tier == null) return;

        if (tier.seq != null && tier.seq.IsActive())
            tier.seq.Kill();

        if (tier.entries == null) return;

        foreach (var e in tier.entries)
        {
            if (e == null || e.root == null) continue;
            e.root.gameObject.SetActive(false);
        }
    }

    private void PlayGaugeFxForThreshold(float threshold)
    {
        // choose which MMF_Player to fire
        if (Mathf.Approximately(threshold, 25f))
        {
            _tier25GaugeFx?.StopFeedbacks();
            _tier25GaugeFx?.PlayFeedbacks();
        }
        else if (Mathf.Approximately(threshold, 50f))
        {
            _tier50GaugeFx?.StopFeedbacks();
            _tier50GaugeFx?.PlayFeedbacks();
        }
        else if (Mathf.Approximately(threshold, 75f))
        {
            _tier75GaugeFx?.StopFeedbacks();
            _tier75GaugeFx?.PlayFeedbacks();
        }
        else if (Mathf.Approximately(threshold, 100f))
        {
            _tier100GaugeFx?.StopFeedbacks();
            _tier100GaugeFx?.PlayFeedbacks();
        }
    }
    // ───────────────────────────── UI Button hook

    public void OnClickContinue()
    {
        // Mark this tutorial as completed & hide it
        UIManager.Instance?.TutorialSuccess(TutorialKey.Momentum);
        TutorialProgress.SetLearned(TutorialKey.Momentum);

        // Resume gameplay (timeScale + inputs + cursor lock)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnMomentumTutorialCompleted();
        }
    }
}
