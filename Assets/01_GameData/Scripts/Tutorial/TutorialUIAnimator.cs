// チュートリアルUIの表示・成功・非表示アニメーションを非同期で制御するスクリプト
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;

public class TutorialUIAnimator : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] private CanvasGroup panelGroup;   // whole widget       // パネル全体のフェード制御
    [SerializeField] private CanvasGroup contentGroup; // prompt UI          // チュートリアル指示UIのフェード制御
    [SerializeField] private CanvasGroup checkGroup;   // success UI         // 成功チェックマークUIのフェード制御

    [Header("Pieces")]
    [SerializeField] private Image bgFrame;            // vertical fill frame (Filled Vertical, Origin Bottom)  // 縦方向に塗りつぶされる背景フレーム
    [SerializeField] private Image outlineArc;         // Radial360, show as outline/arc                        // ループするアウトラインの弧
    [SerializeField] private Image checkmark;          // success check                                          // 成功チェックマーク画像
    [SerializeField] private Image burst;              // optional success burst/glow                            // 成功時のバースト・グロー画像（任意）

    [Header("Show timings")]
    [SerializeField] private float panelFadeIn   = 0.25f;  // パネルのフェードイン時間
    [SerializeField] private float bgFillTime    = 0.35f;  // vertical 0→1   // 背景フレームを塗りつぶす時間
    [SerializeField] private float contentFadeIn = 0.30f;  // コンテンツのフェードイン時間

    [Header("Outline Fill (no spin)")]
    [SerializeField] private float arcFillDuration = 1.0f; // seconds for 0→1    // アウトライン弧が0→1に塗りつぶされる時間
    [SerializeField] private float arcAlpha        = 0.9f; // outline opacity while visible    // 表示中のアウライン透明度
    [SerializeField] private bool  arcYoyo         = true; // true: 0→1→0, false: restart 0→1  // ヨーヨー折り返しか再スタートか

    [Header("Success")]
    [SerializeField] private float swapToCheck   = 0.20f;      // コンテンツを隠してチェックに切り替えるまでの時間
    [SerializeField] private float successMinScale = 0.7f;     // start small    // チェックマークの開始スケール
    [SerializeField] private float successPeakScale = 1.2f;    // grow big       // チェックマークのピークスケール
    [SerializeField] private float successEndScale = 0.85f;    // shrink target before hide  // フェードアウト前の最終スケール
    [SerializeField] private float successGrowTime = 0.18f;    // small -> big   // スケールアップにかかる時間
    [SerializeField] private float successPeakHold = 0.10f;    // hold at peak   // ピーク状態を維持する時間
    [SerializeField] private float successShrinkTime = 0.20f;  // big -> small   // スケールダウンにかかる時間
    [SerializeField] private float successFadeTime = 0.20f;    // alpha to 0     // フェードアウトにかかる時間

    [Header("Success Burst")]
    [SerializeField] private float burstStartScale = 0.2f;     // バーストの開始スケール
    [SerializeField] private float burstPeakScale = 1.2f;      // バーストのピークスケール

    [Header("Hide timings")]
    [SerializeField] private float contentFadeOut = 0.18f;     // コンテンツのフェードアウト時間
    [SerializeField] private float panelFadeOut   = 0.20f;     // パネルのフェードアウト時間

    private CancellationToken _destroyToken;    // オブジェクト破棄時にasyncを中断するトークン
    private Tween _arcFillTween;                // アウライン弧のループtween参照

    void Awake()
    {
        _destroyToken = this.GetCancellationTokenOnDestroy(); // 破棄トークンを取得
        if (!panelGroup) panelGroup = GetComponent<CanvasGroup>();
    }

    void OnDisable()  { _arcFillTween?.Kill(); }    // 無効化時に弧アニメーションを停止
    void OnDestroy()  { _arcFillTween?.Kill(); }    // 破棄時に弧アニメーションを停止

    // ───────────────────────────────────────────────────────────
    // SHOW: 1) BG vertical fill  2) Content fade  3) Arc fill loop
    // ───────────────────────────────────────────────────────────
    public async UniTask ShowAsync(CancellationToken ct = default)
    {
        // 外部トークンと破棄トークンをリンクして安全にキャンセル可能にする
        var token = CancellationTokenSource.CreateLinkedTokenSource(ct, _destroyToken).Token;

        gameObject.SetActive(true);

        // reset states
        panelGroup.alpha = 0f;
        if (contentGroup) { contentGroup.alpha = 0f; contentGroup.gameObject.SetActive(true); }
        if (checkGroup)   { checkGroup.alpha = 0f;   checkGroup.gameObject.SetActive(false); }   // チェックは最初非表示
        if (burst)        { burst.gameObject.SetActive(false); burst.color = new Color(1,1,1,0); }

        if (bgFrame)   bgFrame.fillAmount   = 0f;   // 背景フレームをゼロからスタート
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
        if (bgFrame)      seq.Join(bgFrame.DOFillAmount(1f, bgFillTime).SetEase(Ease.OutCubic)); // 背景を下から上へ塗りつぶす
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
                .SetLoops(-1, arcYoyo ? LoopType.Yoyo : LoopType.Restart) // ヨーヨーまたは再スタートでループ
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
        if (checkmark) checkmark.rectTransform.localScale = Vector3.one * successMinScale; // 小さいスケールから開始

        // Hide content first
        if (contentGroup)
        {
            await contentGroup.DOFade(0f, swapToCheck).SetUpdate(true).Await(token); // 指示UIをフェードアウト
            contentGroup.gameObject.SetActive(false);
        }

        // Optional burst prep
        if (burst)
        {
            burst.gameObject.SetActive(true);
            burst.color = new Color(1, 1, 1, 0f);           // 透明から開始
            burst.rectTransform.localScale = Vector3.one * burstStartScale;
        }

        // SUCCESS: small -> big -> (hold) -> small & fade
        var seq = DOTween.Sequence().SetUpdate(true);

        // fade in + grow to peak
        if (checkGroup) seq.Append(checkGroup.DOFade(1f, successGrowTime)); // チェックグループをフェードイン
        if (checkmark) seq.Join(checkmark.rectTransform.DOScale(successPeakScale, successGrowTime).SetEase(Ease.OutBack)); // 弾けるスケールアップ

        // burst timed with rise
        if (burst)
        {
            seq.Join(burst.DOFade(0.8f, successGrowTime * 0.33f));                          // バーストをフェードイン
            seq.Join(burst.rectTransform.DOScale(burstPeakScale, successGrowTime * 0.66f)); // バーストをスケールアップ
        }

        // ⬅️ Hold at hero size longer here
        if (successPeakHold > 0f) seq.AppendInterval(successPeakHold); // ピーク状態を一定時間維持

        // shrink & fade out the check
        if (checkGroup) seq.Append(checkGroup.DOFade(0f, successFadeTime).SetEase(Ease.InQuad)); // チェックをフェードアウト
        if (checkmark) seq.Join(checkmark.rectTransform.DOScale(successEndScale, successShrinkTime).SetEase(Ease.InQuad)); // スケールダウン

        await seq.Await(token);

        if (burst) burst.gameObject.SetActive(false);   // バーストを非表示

        await HideAsync(token); // パネル全体を非表示アニメーション
    }


    // ───────────────────────────────────────────────────────────
    // HIDE: scale-shrink + fade (kept), outline quick fade & reset
    // ───────────────────────────────────────────────────────────
    public async UniTask HideAsync(CancellationToken ct = default)
    {
        var token = CancellationTokenSource.CreateLinkedTokenSource(ct, _destroyToken).Token;

        // stop outline fill loop
        _arcFillTween?.Kill();  // アウライン弧のループを停止

        // quick fade-out for arc (optional)
        if (outlineArc) outlineArc.DOFade(0f, 0.15f).SetUpdate(true);  // アウラインを素早くフェードアウト

        // fade content if still visible
        if (contentGroup && contentGroup.gameObject.activeSelf && contentGroup.alpha > 0f)
            await contentGroup.DOFade(0f, contentFadeOut).SetUpdate(true).Await(token); // コンテンツをフェードアウト

        // SCALE-SHRINK + PANEL FADE in parallel
        var rt = (RectTransform)transform;
        var baseScale = rt.localScale;  // 元のスケールを保存

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(rt.DOScale(0.96f, panelFadeOut).SetEase(Ease.InOutQuad)); // 少し縮小させる
        seq.Join(panelGroup.DOFade(0f, panelFadeOut).SetEase(Ease.InQuad)); // パネルをフェードアウト
        await seq.Await(token);

        // reset for next Show
        rt.localScale = baseScale;      // スケールを元に戻す
        if (bgFrame)    bgFrame.fillAmount = 0f;    // 背景フレームをリセット
        if (outlineArc)
        {
            // restore alpha and reset fill for next show
            var c = outlineArc.color;
            outlineArc.color = new Color(c.r, c.g, c.b, arcAlpha); // 透明度を元の値に戻す
            outlineArc.fillAmount = 0f;                              // 塗りつぶし量をリセット
        }

        if (this && gameObject) gameObject.SetActive(false); // 非表示にする
    }
}
