using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using static UnityEngine.CullingGroup;

public class MomentumBuffFeedUI : MonoBehaviour
{
    [System.Serializable]
    public class PopupEntry
    {
        public RectTransform root;   // icon root
        public CanvasGroup canvasGroup;

        [HideInInspector] public Vector2 startPos;
        [HideInInspector] public Vector3 startScale;
    }

    [System.Serializable]
    public class TierPopup
    {
        public PopupEntry[] entries;      // 50% = 2, 100% = 3, etc.
        [HideInInspector] public Sequence seq;
    }

    [Header("Tier Popups (Icons only)")]
    [SerializeField] private TierPopup _tier25;
    [SerializeField] private TierPopup _tier50;   // 2 icons
    [SerializeField] private TierPopup _tier75;
    [SerializeField] private TierPopup _tier100;  // 3 icons

    [Header("Popup Animation")]
    [SerializeField] private float _startScale = 0.75f;
    [SerializeField] private float _popScale = 1.10f;
    [SerializeField] private float _popTime = 0.18f;
    [SerializeField] private float _riseDistance = 40f;
    [SerializeField] private float _riseTime = 0.40f;
    [SerializeField] private float _holdTime = 0.20f;
    [SerializeField] private float _fadeOutTime = 0.22f;

    [Header("Timing")]
    [SerializeField] private float _betweenEntriesDelay = 0.06f; // small gap between icons
    [SerializeField] private bool _useUnscaledTime = true;

    private CancellationTokenSource _cts;
    private bool _bound;
    private MomentumState _lastState = MomentumState.None;

    private void Awake()
    {
        SetupTier(_tier25);
        SetupTier(_tier50);
        SetupTier(_tier75);
        SetupTier(_tier100);
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        BindAsync(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;

        if (_bound && MomentumManager.Instance != null)
            MomentumManager.Instance.onStateChanged.RemoveListener(OnStateChanged);
        _bound = false;

        KillTier(_tier25);
        KillTier(_tier50);
        KillTier(_tier75);
        KillTier(_tier100);
    }

    private async UniTaskVoid BindAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() =>
      MomentumManager.Instance != null &&
      MomentumManager.Instance.onStateChanged != null,
      cancellationToken: token);
       
        if (token.IsCancellationRequested) return;

        _lastState = MomentumManager.Instance.CurrentState;
        MomentumManager.Instance.onStateChanged.AddListener(OnStateChanged);
        _bound = true;
    }

    private void OnStateChanged(MomentumState state)
    {
        // This event only fires when state changes, so no spam.
        switch (state)
        {
            case MomentumState.Tier1: PlayTier(_tier25); break;
            case MomentumState.Tier2: PlayTier(_tier50); break;  // 2 icons
            case MomentumState.Tier3: PlayTier(_tier75); break;
            case MomentumState.Max: PlayTier(_tier100); break; // 3 icons
        }
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ setup/hide

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

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ play

    private void PlayTier(TierPopup tier)
    {
        if (tier == null || tier.entries == null || tier.entries.Length == 0) return;

        KillTier(tier);

        var seq = DOTween.Sequence();

        // entries appended => one-by-one (like your tutorial UI)
        foreach (var e in tier.entries)
        {
            if (e == null || e.root == null) continue;

            seq.Append(CreateEntrySequence(e));
            seq.AppendInterval(_betweenEntriesDelay);
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
            e.root.localScale = e.startScale * _startScale;

            if (e.canvasGroup != null)
                e.canvasGroup.alpha = 0f;
        });

        // POP
        if (e.canvasGroup != null)
            s.Append(e.canvasGroup.DOFade(1f, _popTime));
        else
            s.AppendInterval(_popTime);

        s.Join(e.root
            .DOScale(e.startScale * _popScale, _popTime)
            .SetEase(Ease.OutBack));

        // RISE
        s.Append(e.root
            .DOAnchorPos(e.startPos + Vector2.up * _riseDistance, _riseTime)
            .SetEase(Ease.OutQuad));

        s.Join(e.root
            .DOScale(e.startScale, _riseTime)
            .SetEase(Ease.OutQuad));

        // HOLD
        s.AppendInterval(_holdTime);

        // FADE OUT
        if (e.canvasGroup != null)
            s.Append(e.canvasGroup.DOFade(0f, _fadeOutTime));
        else
            s.AppendInterval(_fadeOutTime);

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
            if (e?.root == null) continue;
            e.root.gameObject.SetActive(false);
            if (e.canvasGroup != null) e.canvasGroup.alpha = 0f;
        }
    }
}
