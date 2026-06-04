// チュートリアルUIの表示・成功・非表示アニメーションを非同期で制御するスクリプト
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MoreMountains.Tools;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class TutorialUIAnimator : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] private CanvasGroup panelGroup;   // パネル全体のフェード制御
    [SerializeField] private CanvasGroup contentGroup; // チュートリアル指示UIのフェード制御
    [SerializeField] private CanvasGroup checkGroup;   // 成功チェックマークUIのフェード制御

    [Header("Pieces")]
    [SerializeField] private Image bgFrame;            // 縦方向に塗りつぶされる背景フレーム（Filled Vertical、Origin Bottom）
    [SerializeField] private Image outlineArc;         // ループするアウトラインの弧（Radial360）
    [SerializeField] private Image checkmark;          // 成功チェックマーク画像
    [SerializeField] private Image burst;              // 成功時のバースト・グロー画像（任意）

    [Header("Show timings")]
    [SerializeField] private float panelFadeIn = 0.25f;  // パネルのフェードイン時間
    [SerializeField] private float bgFillTime = 0.35f;  // 背景フレームを0→1に塗りつぶす時間
    [SerializeField] private float contentFadeIn = 0.30f;  // コンテンツのフェードイン時間

    [Header("Outline Fill (no spin)")]
    [SerializeField] private float arcFillDuration = 1.0f; // アウトライン弧が0→1に塗りつぶされる秒数
    [SerializeField] private float arcAlpha = 0.9f; // 表示中のアウトライン透明度
    [SerializeField] private bool arcYoyo = true; // true: 0→1→0のヨーヨー、false: 0→1を繰り返す

    [Header("Success")]
    [SerializeField] private float swapToCheck = 0.20f;      // コンテンツを隠してチェックに切り替えるまでの時間
    [SerializeField] private float successMinScale = 0.7f;     // チェックマークの開始スケール（小）
    [SerializeField] private float successPeakScale = 1.2f;    // チェックマークのピークスケール（大）
    [SerializeField] private float successEndScale = 0.85f;    // フェードアウト前の最終スケール（縮小後）
    [SerializeField] private float successGrowTime = 0.18f;    // スケールアップ（小→大）にかかる時間
    [SerializeField] private float successPeakHold = 0.10f;    // ピーク状態を維持する時間
    [SerializeField] private float successShrinkTime = 0.20f;  // スケールダウン（大→小）にかかる時間
    [SerializeField] private float successFadeTime = 0.20f;    // alpha を0にするフェードアウト時間

    [Header("Success Burst")]
    [SerializeField] private float burstStartScale = 0.2f;     // バーストの開始スケール
    [SerializeField] private float burstPeakScale = 1.2f;      // バーストのピークスケール

    [Header("Hide timings")]
    [SerializeField] private float contentFadeOut = 0.18f;     // コンテンツのフェードアウト時間
    [SerializeField] private float panelFadeOut = 0.20f;     // パネルのフェードアウト時間


    private CancellationToken _destroyToken;    // オブジェクト破棄時にasyncを中断するトークン
    private Tween _arcFillTween;                // アウライン弧のループtween参照

    void Awake()
    {
        _destroyToken = this.GetCancellationTokenOnDestroy(); // 破棄トークンを取得
        if (!panelGroup) panelGroup = GetComponent<CanvasGroup>();
    }

    void OnDisable() { _arcFillTween?.Kill(); }    // 無効化時に弧アニメーションを停止
    void OnDestroy() { _arcFillTween?.Kill(); }    // 破棄時に弧アニメーションを停止

    // ───────────────────────────────────────────────────────────
    // 表示: 1) 背景の縦方向フィル  2) コンテンツのフェード  3) 弧のフィルループ
    // ───────────────────────────────────────────────────────────
    public async UniTask ShowAsync(CancellationToken ct = default)
    {
        // 外部トークンと破棄トークンをリンクして安全にキャンセル可能にする
        var token = CancellationTokenSource.CreateLinkedTokenSource(ct, _destroyToken).Token;

        gameObject.SetActive(true);

        // 状態をリセット
        panelGroup.alpha = 0f;
        if (contentGroup) { contentGroup.alpha = 0f; contentGroup.gameObject.SetActive(true); }
        if (checkGroup) { checkGroup.alpha = 0f; checkGroup.gameObject.SetActive(false); }   // チェックは最初非表示
        if (burst) { burst.gameObject.SetActive(false); burst.color = new Color(1, 1, 1, 0); }

        if (bgFrame) bgFrame.fillAmount = 0f;   // 背景フレームをゼロからスタート
        if (outlineArc)
        {
            // 透明な状態からスタート；表示中のalphaを確保
            outlineArc.fillAmount = 0f;
            var c = outlineArc.color; c.a = arcAlpha; outlineArc.color = c;
        }

        // パネルのフェードイン
        await panelGroup.DOFade(1f, panelFadeIn).SetUpdate(true).SetEase(Ease.OutQuad).Await(token);

        // 並列：背景フィルアップ & コンテンツのフェードイン
        var seq = DOTween.Sequence().SetUpdate(true);
        if (bgFrame) seq.Join(bgFrame.DOFillAmount(1f, bgFillTime).SetEase(Ease.OutCubic)); // 背景を下から上へ塗りつぶす
        if (contentGroup) seq.Join(contentGroup.DOFade(1f, contentFadeIn).SetEase(Ease.OutQuad));
        await seq.Await(token);

        // アウトラインフィルループを開始（0→1→0 またはリスタート）
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
    // 成功：チェックマークのポップアニメーション（演出は変更なし）
    // ───────────────────────────────────────────────────────────
    public async UniTask MarkSuccessAndAutoHideAsync(CancellationToken ct = default)
    {
        var token = CancellationTokenSource.CreateLinkedTokenSource(ct, _destroyToken).Token;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        panelGroup.alpha = 1f;

        if (checkGroup) { checkGroup.gameObject.SetActive(true); checkGroup.alpha = 0f; }
        if (checkmark) checkmark.rectTransform.localScale = Vector3.one * successMinScale; // 小さいスケールから開始

        // まずコンテンツを非表示
        if (contentGroup)
        {
            await contentGroup.DOFade(0f, swapToCheck).SetUpdate(true).Await(token); // 指示UIをフェードアウト
            contentGroup.gameObject.SetActive(false);
        }

        // バーストの準備（任意）
        if (burst)
        {
            burst.gameObject.SetActive(true);
            burst.color = new Color(1, 1, 1, 0f);           // 透明から開始
            burst.rectTransform.localScale = Vector3.one * burstStartScale;
        }

        // 成功：小→大→（維持）→小＆フェード
        var seq = DOTween.Sequence().SetUpdate(true);

        // フェードイン + ピークまで拡大
        if (checkGroup) seq.Append(checkGroup.DOFade(1f, successGrowTime)); // チェックグループをフェードイン
        if (checkmark) seq.Join(checkmark.rectTransform.DOScale(successPeakScale, successGrowTime).SetEase(Ease.OutBack)); // 弾けるスケールアップ

        // バーストを浮上に合わせてタイミング調整
        if (burst)
        {
            seq.Join(burst.DOFade(0.8f, successGrowTime * 0.33f));                          // バーストをフェードイン
            seq.Join(burst.rectTransform.DOScale(burstPeakScale, successGrowTime * 0.66f)); // バーストをスケールアップ
        }

        // ここでヒーローサイズを長めに維持する
        if (successPeakHold > 0f) seq.AppendInterval(successPeakHold); // ピーク状態を一定時間維持

        // チェックマークを縮小してフェードアウト
        if (checkGroup) seq.Append(checkGroup.DOFade(0f, successFadeTime).SetEase(Ease.InQuad)); // チェックをフェードアウト
        if (checkmark) seq.Join(checkmark.rectTransform.DOScale(successEndScale, successShrinkTime).SetEase(Ease.InQuad)); // スケールダウン

        await seq.Await(token);

        if (burst) burst.gameObject.SetActive(false);   // バーストを非表示

        await HideAsync(token); // パネル全体を非表示アニメーション
    }


    // ───────────────────────────────────────────────────────────
    // 非表示：縮小＋フェード（維持）、アウトラインの素早いフェード＆リセット
    // ───────────────────────────────────────────────────────────
    public async UniTask HideAsync(CancellationToken ct = default)
    {
        var token = CancellationTokenSource.CreateLinkedTokenSource(ct, _destroyToken).Token;

        // アウトラインフィルループを停止
        _arcFillTween?.Kill();  // アウライン弧のループを停止

        // アウトラインを素早くフェードアウト（任意）
        if (outlineArc) outlineArc.DOFade(0f, 0.15f).SetUpdate(true);  // アウラインを素早くフェードアウト

        // コンテンツがまだ表示中であればフェードアウト
        if (contentGroup && contentGroup.gameObject.activeSelf && contentGroup.alpha > 0f)
            await contentGroup.DOFade(0f, contentFadeOut).SetUpdate(true).Await(token); // コンテンツをフェードアウト

        // 縮小 + パネルフェードを並列実行
        var rt = (RectTransform)transform;
        var baseScale = rt.localScale;  // 元のスケールを保存

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(rt.DOScale(0.96f, panelFadeOut).SetEase(Ease.InOutQuad)); // 少し縮小させる
        seq.Join(panelGroup.DOFade(0f, panelFadeOut).SetEase(Ease.InQuad)); // パネルをフェードアウト
        await seq.Await(token);

        // 次のShow用にリセット
        rt.localScale = baseScale;      // スケールを元に戻す
        if (bgFrame) bgFrame.fillAmount = 0f;    // 背景フレームをリセット
        if (outlineArc)
        {
            // 次の表示に備えてalphaを復元し、フィル量をリセット
            var c = outlineArc.color;
            outlineArc.color = new Color(c.r, c.g, c.b, arcAlpha); // 透明度を元の値に戻す
            outlineArc.fillAmount = 0f;                              // 塗りつぶし量をリセット
        }

        if (this && gameObject) gameObject.SetActive(false); // 非表示にする
    }
}
