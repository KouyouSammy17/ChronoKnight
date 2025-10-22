using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;

public class TutorialUIAnimator : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] private CanvasGroup panelGroup;   // whole widget
    [SerializeField] private CanvasGroup contentGroup; // prompt UI
    [SerializeField] private CanvasGroup checkGroup;   // success UI

    [Header("Pieces")]
    [SerializeField] private Image bgFrame;            // vertical fill frame (Filled Vertical, Origin Bottom)
    [SerializeField] private Image outlineArc;         // Radial360, show as outline/arc
    [SerializeField] private Image checkmark;          // success check
    [SerializeField] private Image burst;              // optional success burst/glow

    [Header("Show timings")]
    [SerializeField] private float panelFadeIn   = 0.25f;
    [SerializeField] private float bgFillTime    = 0.35f; // vertical 0→1
    [SerializeField] private float contentFadeIn = 0.30f;

    [Header("Outline Fill (no spin)")]
    [SerializeField] private float arcFillDuration = 1.0f; // seconds for 0→1
    [SerializeField] private float arcAlpha        = 0.9f; // outline opacity while visible
    [SerializeField] private bool  arcYoyo         = true; // true: 0→1→0, false: restart 0→1

    [Header("Success")]
    [SerializeField] private float swapToCheck   = 0.20f;
    [SerializeField] private float successMinScale = 0.7f;   // start small
    [SerializeField] private float successPeakScale = 1.2f;   // grow big
    [SerializeField] private float successEndScale = 0.85f;  // shrink target before hide
    [SerializeField] private float successGrowTime = 0.18f;  // small -> big
    [SerializeField] private float successPeakHold = 0.10f;  // hold at peak
    [SerializeField] private float successShrinkTime = 0.20f;  // big -> small
    [SerializeField] private float successFadeTime = 0.20f;  // alpha to 0


    [Header("Hide timings")]
    [SerializeField] private float contentFadeOut = 0.18f;
    [SerializeField] private float panelFadeOut   = 0.20f;

    private CancellationToken _destroyToken;
    private Tween _arcFillTween;

    void Awake()
    {
        _destroyToken = this.GetCancellationTokenOnDestroy();
        if (!panelGroup) panelGroup = GetComponent<CanvasGroup>();
    }

    void OnDisable()  { _arcFillTween?.Kill(); }
    void OnDestroy()  { _arcFillTween?.Kill(); }

    // ───────────────────────────────────────────────────────────
    // SHOW: 1) BG vertical fill  2) Content fade  3) Arc fill loop
    // ───────────────────────────────────────────────────────────
    public async UniTask ShowAsync(CancellationToken ct = default)
    {
        var token = CancellationTokenSource.CreateLinkedTokenSource(ct, _destroyToken).Token;

        gameObject.SetActive(true);

        // reset states
        panelGroup.alpha = 0f;
        if (contentGroup) { contentGroup.alpha = 0f; contentGroup.gameObject.SetActive(true); }
        if (checkGroup)   { checkGroup.alpha = 0f;   checkGroup.gameObject.SetActive(false); }
        if (burst)        { burst.gameObject.SetActive(false); burst.color = new Color(1,1,1,0); }

        if (bgFrame)   bgFrame.fillAmount   = 0f;
        if (outlineArc)
        {
            // start at transparent fill; ensure visible alpha
            outlineArc.fillAmount = 0f;
            var c = outlineArc.color; c.a = arcAlpha; outlineArc.color = c;
        }

        // panel fade-in
        await panelGroup.DOFade(1f, panelFadeIn).SetUpdate(true).SetEase(Ease.OutQuad).Await(token);

        // parallel: bg fill up & content fade in
        var seq = DOTween.Sequence().SetUpdate(true);
        if (bgFrame)      seq.Join(bgFrame.DOFillAmount(1f, bgFillTime).SetEase(Ease.OutCubic));
        if (contentGroup) seq.Join(contentGroup.DOFade(1f, contentFadeIn).SetEase(Ease.OutQuad));
        await seq.Await(token);

        // start outline fill loop (0→1→0 or restart)
        if (outlineArc)
        {
            _arcFillTween?.Kill();
            outlineArc.fillAmount = 0f;
            _arcFillTween = outlineArc
                .DOFillAmount(1f, Mathf.Max(0.01f, arcFillDuration))
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, arcYoyo ? LoopType.Yoyo : LoopType.Restart)
                .SetUpdate(true);
        }
    }

    // ───────────────────────────────────────────────────────────
    // SUCCESS: keep your check pop (unchanged feel)
    // ───────────────────────────────────────────────────────────
    public async UniTask MarkSuccessAndAutoHideAsync(CancellationToken ct = default)
    {
        var token = CancellationTokenSource.CreateLinkedTokenSource(ct, _destroyToken).Token;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        panelGroup.alpha = 1f;

        if (checkGroup) { checkGroup.gameObject.SetActive(true); checkGroup.alpha = 0f; }
        if (checkmark) checkmark.rectTransform.localScale = Vector3.one * successMinScale;

        // Hide content first
        if (contentGroup)
        {
            await contentGroup.DOFade(0f, swapToCheck).SetUpdate(true).Await(token);
            contentGroup.gameObject.SetActive(false);
        }

        // Optional burst prep
        if (burst)
        {
            burst.gameObject.SetActive(true);
            burst.color = new Color(1, 1, 1, 0f);
            burst.rectTransform.localScale = Vector3.one * 0.2f;
        }

        // SUCCESS: small -> big -> (hold) -> small & fade
        var seq = DOTween.Sequence().SetUpdate(true);

        // fade in + grow to peak
        if (checkGroup) seq.Append(checkGroup.DOFade(1f, successGrowTime));
        if (checkmark) seq.Join(checkmark.rectTransform.DOScale(successPeakScale, successGrowTime).SetEase(Ease.OutBack));

        // burst timed with rise
        if (burst)
        {
            seq.Join(burst.DOFade(0.8f, successGrowTime * 0.33f));
            seq.Join(burst.rectTransform.DOScale(1.2f, successGrowTime * 0.66f));
        }

        // ⬅️ Hold at hero size longer here
        if (successPeakHold > 0f) seq.AppendInterval(successPeakHold);

        // shrink & fade out the check
        if (checkGroup) seq.Append(checkGroup.DOFade(0f, successFadeTime).SetEase(Ease.InQuad));
        if (checkmark) seq.Join(checkmark.rectTransform.DOScale(successEndScale, successShrinkTime).SetEase(Ease.InQuad));

        await seq.Await(token);

        if (burst) burst.gameObject.SetActive(false);

        await HideAsync(token);
    }


    // ───────────────────────────────────────────────────────────
    // HIDE: scale-shrink + fade (kept), outline quick fade & reset
    // ───────────────────────────────────────────────────────────
    public async UniTask HideAsync(CancellationToken ct = default)
    {
        var token = CancellationTokenSource.CreateLinkedTokenSource(ct, _destroyToken).Token;

        // stop outline fill loop
        _arcFillTween?.Kill();

        // quick fade-out for arc (optional)
        if (outlineArc) outlineArc.DOFade(0f, 0.15f).SetUpdate(true);

        // fade content if still visible
        if (contentGroup && contentGroup.gameObject.activeSelf && contentGroup.alpha > 0f)
            await contentGroup.DOFade(0f, contentFadeOut).SetUpdate(true).Await(token);

        // SCALE-SHRINK + PANEL FADE in parallel
        var rt = (RectTransform)transform;
        var baseScale = rt.localScale;

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(rt.DOScale(0.96f, panelFadeOut).SetEase(Ease.InOutQuad));
        seq.Join(panelGroup.DOFade(0f, panelFadeOut).SetEase(Ease.InQuad));
        await seq.Await(token);

        // reset for next Show
        rt.localScale = baseScale;
        if (bgFrame)    bgFrame.fillAmount = 0f;
        if (outlineArc)
        {
            // restore alpha and reset fill for next show
            var c = outlineArc.color;
            outlineArc.color = new Color(c.r, c.g, c.b, arcAlpha);
            outlineArc.fillAmount = 0f;
        }

        if (this && gameObject) gameObject.SetActive(false);
    }
}
