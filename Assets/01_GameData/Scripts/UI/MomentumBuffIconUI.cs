using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;

public class MomentumBuffIconUI : MonoBehaviour
{
    [System.Serializable]
    public class IconEntry
    {
        public MomentumState state;     // Tier1/Tier2/Tier3/Max
        public GameObject root;         // icon GameObject
        public CanvasGroup group;       // for fade
        public RectTransform rect;      // for pop scale

        [HideInInspector] public Vector3 baseScale;
        [HideInInspector] public bool isOn;
        [HideInInspector] public Tween tween;
    }

    [Header("Icons (Persistent HUD)")]
    [SerializeField] private IconEntry[] _icons;

    [Header("Behavior")]
    [SerializeField] private bool _showAllUnlockedBelowMax = true; // Tier3 also shows Tier1/2 etc.
    [SerializeField] private bool _animateWhilePaused = true;

    [Header("Anim")]
    [SerializeField] private float _fadeIn = 0.15f;
    [SerializeField] private float _fadeOut = 0.12f;
    [SerializeField] private float _popScale = 1.12f;
    [SerializeField] private float _popTime = 0.12f;

    private CancellationTokenSource _cts;
    private bool _bound;

    private void Awake()
    {
        // cache + default hidden
        if (_icons == null) return;

        foreach (var e in _icons)
        {
            if (e == null || e.root == null) continue;

            if (e.group == null) e.group = e.root.GetComponent<CanvasGroup>();
            if (e.group == null) e.group = e.root.AddComponent<CanvasGroup>();

            if (e.rect == null) e.rect = e.root.transform as RectTransform;
            e.baseScale = e.rect != null ? e.rect.localScale : Vector3.one;

            e.group.alpha = 0f;
            e.root.SetActive(false);
            e.isOn = false;
        }
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        BindAsync(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_bound && MomentumManager.Instance != null)
            MomentumManager.Instance.onMomentumChanged.RemoveListener(OnMomentumChanged);

        _bound = false;

        // kill tweens safely
        if (_icons != null)
        {
            foreach (var e in _icons)
                e?.tween?.Kill();
        }
    }

    private async UniTaskVoid BindAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() => MomentumManager.Instance != null, cancellationToken: token);
        if (token.IsCancellationRequested) return;

        MomentumManager.Instance.onMomentumChanged.AddListener(OnMomentumChanged);
        _bound = true;

        // force refresh
        OnMomentumChanged(MomentumManager.Instance.CurrentMomentum);
    }

    private void OnMomentumChanged(float _)
    {
        if (MomentumManager.Instance == null) return;

        var s = MomentumManager.Instance.CurrentState;
        ApplyState(s);
    }

    private void ApplyState(MomentumState current)
    {
        if (_icons == null) return;

        foreach (var e in _icons)
        {
            if (e == null || e.root == null || e.group == null) continue;

            bool shouldBeOn = _showAllUnlockedBelowMax
                ? (e.state != MomentumState.None && e.state <= current)
                : (e.state == current);

            SetIcon(e, shouldBeOn);
        }
    }

    private void SetIcon(IconEntry e, bool on)
    {
        if (e.isOn == on) return;
        e.isOn = on;

        bool unscaled = _animateWhilePaused;

        e.tween?.Kill();
        e.tween = null;

        if (on)
        {
            e.root.SetActive(true);
            e.group.alpha = 0f;

            if (e.rect != null) e.rect.localScale = e.baseScale;

            // fade in + tiny pop
            var seq = DOTween.Sequence().SetUpdate(unscaled);
            seq.Append(e.group.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad));

            if (e.rect != null)
            {
                seq.Join(e.rect.DOScale(e.baseScale * _popScale, _popTime).SetEase(Ease.OutBack));
                seq.Append(e.rect.DOScale(e.baseScale, 0.08f).SetEase(Ease.OutQuad));
            }

            e.tween = seq.SetLink(e.root, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            // fade out then disable
            e.tween = e.group
                .DOFade(0f, _fadeOut)
                .SetEase(Ease.OutQuad)
                .SetUpdate(unscaled)
                .OnComplete(() =>
                {
                    if (e.root != null) e.root.SetActive(false);
                    if (e.rect != null) e.rect.localScale = e.baseScale;
                })
                .SetLink(e.root, LinkBehaviour.KillOnDestroy);
        }
    }
}
