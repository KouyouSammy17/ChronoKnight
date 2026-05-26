// ゲーム開始前の「READY」「GO!」アニメーションを再生するスクリプト
using DG.Tweening;
using UnityEngine;

public class ReadyGoUI_Anim : MonoBehaviour
{
    [Header("Root (optional)")]
    [SerializeField] private CanvasGroup _rootGroup; // ルート全体のフェード制御用CanvasGroup

    [Header("Groups")]
    [SerializeField] private RectTransform _readyRoot;   // Ready
    [SerializeField] private CanvasGroup _readyGroup;    // Readyテキストのフェード制御
    [SerializeField] private RectTransform _goRoot;      // Go!
    [SerializeField] private CanvasGroup _goGroup;       // Go!テキストのフェード制御

    [Header("Optional Dim")]
    [SerializeField] private CanvasGroup _dimGroup;       // fullscreen Image+CanvasGroup (RaycastTarget OFF)
    [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.25f; // 暗幕の透明度

    [Header("READY Slide")]
    [SerializeField] private float _readyFromX = -700f;   // slide-in offset from center
    [SerializeField] private float _readyIn = 0.18f;      // Readyスライドイン時間
    [SerializeField] private float _readyHold = 0.28f;    // Ready表示維持時間
    [SerializeField] private float _readyOut = 0.10f;     // Readyフェードアウト時間

    [Header("GO Pop")]
    [SerializeField] private float _goIn = 0.12f;    // GO!ポップイン時間
    [SerializeField] private float _goHold = 0.22f;  // GO!表示維持時間
    [SerializeField] private float _goOut = 0.16f;   // GO!フェードアウト時間

    [Header("Scale")]
    [SerializeField] private float _readyStartScale = 0.95f; // Readyの開始スケール
    [SerializeField] private float _readyPeakScale = 1.06f;  // Readyのピークスケール
    [SerializeField] private float _goStartScale = 0.75f;    // GO!の開始スケール
    [SerializeField] private float _goPeakScale = 1.20f;     // GO!のピークスケール

    private Sequence _seq;            // アニメーション全体のSequence
    private Vector2 _readyCenterPos;  // Readyの中心位置（基準）
    private Vector2 _goCenterPos;     // GO!の中心位置（基準）

    public float TotalDuration => _readyIn + _readyHold + _readyOut + _goIn + _goHold + _goOut; // アニメーション全体の合計時間

    private void Reset()
    {
        _rootGroup = GetComponent<CanvasGroup>();
        var tReady = transform.Find("Ready");
        if (tReady) { _readyRoot = tReady as RectTransform; _readyGroup = tReady.GetComponent<CanvasGroup>(); }
        var tGo = transform.Find("Go!");
        if (tGo) { _goRoot = tGo as RectTransform; _goGroup = tGo.GetComponent<CanvasGroup>(); }
    }

    private void Awake()
    {
        if (_rootGroup == null) _rootGroup = GetComponent<CanvasGroup>();
        CacheCenters(); // 基準位置を保存
        HardHide();     // 起動時は非表示にする
    }

    private void CacheCenters()
    {
        if (_readyRoot != null) _readyCenterPos = _readyRoot.anchoredPosition; // Readyの基準位置を保存
        if (_goRoot != null) _goCenterPos = _goRoot.anchoredPosition;          // GO!の基準位置を保存
    }

    public void HardHide()
    {
        _seq?.Kill(); // アニメーションを即座に停止

        if (_dimGroup != null) _dimGroup.alpha = 0f;

        if (_rootGroup != null) _rootGroup.alpha = 0f; // ルートを透明にする

        if (_readyGroup != null) _readyGroup.alpha = 0f;
        if (_readyRoot != null)
        {
            _readyRoot.localScale = Vector3.one;
            _readyRoot.anchoredPosition = _readyCenterPos;
            _readyRoot.gameObject.SetActive(false); // Readyを非表示
        }

        if (_goGroup != null) _goGroup.alpha = 0f;
        if (_goRoot != null)
        {
            _goRoot.localScale = Vector3.one;
            _goRoot.anchoredPosition = _goCenterPos;
            _goRoot.gameObject.SetActive(false); // GO!を非表示
        }

        gameObject.SetActive(false);
    }

    public void Play()
    {
        _seq?.Kill();
        CacheCenters(); // 基準位置を最新化（UIが動いている場合に備えて）

        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // 最前面に表示

        if (_rootGroup != null) _rootGroup.alpha = 1f;

        // init
        if (_dimGroup != null) _dimGroup.alpha = 0f; // 暗幕を透明から開始

        _readyRoot.gameObject.SetActive(true);
        _goRoot.gameObject.SetActive(false); // GO!は最初非表示

        _readyGroup.alpha = 0f;
        _readyRoot.localScale = Vector3.one * _readyStartScale;
        _readyRoot.anchoredPosition = _readyCenterPos + new Vector2(_readyFromX, 0f); // スライドイン開始位置に設定

        _goGroup.alpha = 0f;
        _goRoot.localScale = Vector3.one * _goStartScale;
        _goRoot.anchoredPosition = _goCenterPos;

        _seq = DOTween.Sequence()
            .SetUpdate(true) // unscaled
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // optional dim in (subtle)
        if (_dimGroup != null)
            _seq.Join(_dimGroup.DOFade(_dimAlpha, 0.12f).SetEase(Ease.OutQuad).SetUpdate(true)); // 暗幕をゆっくりフェードイン

        // READY: slide to center + fade in + tiny scale hit
        _seq.Append(_readyGroup.DOFade(1f, _readyIn).SetEase(Ease.OutQuad));                        // Readyフェードイン
        _seq.Join(_readyRoot.DOAnchorPos(_readyCenterPos, _readyIn).SetEase(Ease.OutCubic));         // 中心へスライドイン
        _seq.Join(_readyRoot.DOScale(_readyPeakScale, _readyIn).SetEase(Ease.OutBack));              // ピークスケールへポップイン

        _seq.AppendInterval(_readyHold); // Ready表示を維持

        // READY out
        _seq.Append(_readyGroup.DOFade(0f, _readyOut).SetEase(Ease.InQuad));          // Readyフェードアウト
        _seq.Join(_readyRoot.DOScale(_readyStartScale, _readyOut).SetEase(Ease.InQuad)); // Readyスケールダウン

        // switch to GO
        _seq.AppendCallback(() =>
        {
            _readyRoot.gameObject.SetActive(false); // Readyを非表示にしてGO!へ切り替え
            _goRoot.gameObject.SetActive(true);

            _goGroup.alpha = 0f;
            _goRoot.localScale = Vector3.one * _goStartScale; // GO!の開始スケールにリセット
        });

        // GO: pop in at center
        _seq.Append(_goGroup.DOFade(1f, _goIn).SetEase(Ease.OutQuad));       // GO!フェードイン
        _seq.Join(_goRoot.DOScale(_goPeakScale, _goIn).SetEase(Ease.OutBack)); // GO!ポップスケールアップ

        _seq.AppendInterval(_goHold); // GO!表示を維持

        // GO out
        _seq.Append(_goGroup.DOFade(0f, _goOut).SetEase(Ease.InQuad));    // GO!フェードアウト
        _seq.Join(_goRoot.DOScale(1f, _goOut).SetEase(Ease.InQuad));      // GO!スケールダウン

        // dim out
        if (_dimGroup != null)
            _seq.Join(_dimGroup.DOFade(0f, 0.16f).SetEase(Ease.InQuad).SetUpdate(true)); // 暗幕フェードアウト

        _seq.OnComplete(HardHide); // アニメーション完了後に全要素を非表示にする
        _seq.Play();
    }
}
