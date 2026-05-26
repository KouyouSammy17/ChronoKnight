// モメンタムの状態に応じてHUDに常時表示するバフアイコンを管理するスクリプト
using UnityEngine;
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

        [HideInInspector] public Vector3 baseScale; // アイコンの基準スケール
        [HideInInspector] public bool isOn;         // 現在の表示状態
        [HideInInspector] public Tween tween;       // 実行中のTween
    }

    [Header("Icons (Persistent HUD)")]
    [SerializeField] private IconEntry[] _icons; // 各ティアに対応するアイコンエントリ一覧

    [Header("Behavior")]
    [SerializeField] private bool _showAllUnlockedBelowMax = true; // Tier3 also shows Tier1/2 etc.
    [SerializeField] private bool _animateWhilePaused = true;      // ポーズ中もアニメーションするか

    [Header("Anim (snappy recommended)")]
    [SerializeField] private float _fadeIn = 0.10f;   // フェードイン時間
    [SerializeField] private float _fadeOut = 0.08f;  // フェードアウト時間
    [SerializeField] private float _popScale = 1.10f; // 出現時のポップスケール
    [SerializeField] private float _popTime = 0.10f;  // ポップアニメーション時間

    private CancellationTokenSource _cts; // 非同期バインド処理のキャンセルトークン
    private bool _bound;                  // MomentumManagerへのバインド状態

    private void Awake()
    {
        if (_icons == null) return;

        foreach (var e in _icons)
        {
            if (e == null || e.root == null) continue;

            if (e.group == null) e.group = e.root.GetComponent<CanvasGroup>();
            if (e.group == null) e.group = e.root.AddComponent<CanvasGroup>(); // なければ追加

            if (e.rect == null) e.rect = e.root.transform as RectTransform;
            e.baseScale = e.rect != null ? e.rect.localScale : Vector3.one; // 基準スケールを保存

            e.group.alpha = 0f;         // 初期状態は透明
            e.root.SetActive(false);    // 初期状態は非表示
            e.isOn = false;
        }
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        BindAsync(_cts.Token).Forget(); // MomentumManagerが準備できたらバインドを開始
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_bound && MomentumManager.Instance != null)
        {
            // NEW: listen to state changes (no spam, instant timing)
            MomentumManager.Instance.onStateChanged.RemoveListener(OnStateChanged); // イベントリスナーを解除
        }

        _bound = false;

        if (_icons != null)
        {
            foreach (var e in _icons)
                e?.tween?.Kill(); // 全アイコンのTweenを停止
        }
    }

    private async UniTaskVoid BindAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() => MomentumManager.Instance != null, cancellationToken: token); // MomentumManagerの準備を待機
        if (token.IsCancellationRequested) return;

        // NEW: subscribe to state change event
        MomentumManager.Instance.onStateChanged.AddListener(OnStateChanged); // 状態変化イベントを購読
        _bound = true;

        // force refresh immediately
        ApplyState(MomentumManager.Instance.CurrentState); // バインド直後に現在の状態を反映
    }

    private void OnStateChanged(MomentumState state)
    {
        ApplyState(state); // 状態変化を受け取りアイコン表示を更新
    }

    private void ApplyState(MomentumState current)
    {
        if (_icons == null) return;

        foreach (var e in _icons)
        {
            if (e == null || e.root == null || e.group == null) continue;

            // _showAllUnlockedBelowMaxがtrueの場合、現在以下の全ティアを表示する
            bool shouldBeOn = _showAllUnlockedBelowMax
                ? (e.state != MomentumState.None && e.state <= current)
                : (e.state == current); // falseの場合は現在のティアのみ表示

            SetIcon(e, shouldBeOn);
        }
    }

    private void SetIcon(IconEntry e, bool on)
    {
        if (e.isOn == on) return; // 状態が変わっていなければ何もしない
        e.isOn = on;

        bool unscaled = _animateWhilePaused;

        e.tween?.Kill(); // 前のTweenを停止
        e.tween = null;

        if (on)
        {
            e.root.SetActive(true);  // 表示してからフェードイン開始
            e.group.alpha = 0f;

            if (e.rect != null) e.rect.localScale = e.baseScale; // スケールを基準値にリセット

            var seq = DOTween.Sequence().SetUpdate(unscaled);
            seq.Append(e.group.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad)); // フェードイン

            if (e.rect != null)
            {
                seq.Join(e.rect.DOScale(e.baseScale * _popScale, _popTime).SetEase(Ease.OutBack)); // ポップスケールアップ
                seq.Append(e.rect.DOScale(e.baseScale, 0.07f).SetEase(Ease.OutQuad));              // 基準スケールへ収束
            }

            e.tween = seq.SetLink(e.root, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            e.tween = e.group
                .DOFade(0f, _fadeOut)
                .SetEase(Ease.OutQuad)
                .SetUpdate(unscaled)
                .OnComplete(() =>
                {
                    if (e.root != null) e.root.SetActive(false);             // フェードアウト完了後に非表示
                    if (e.rect != null) e.rect.localScale = e.baseScale;     // スケールをリセット
                })
                .SetLink(e.root, LinkBehaviour.KillOnDestroy);
        }
    }
}
