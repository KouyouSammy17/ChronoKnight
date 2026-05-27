// モメンタムのティア到達時にアイコンをポップアップ表示するフィードバックUIスクリプト
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
        public RectTransform root;   // アイコンのルート
        public CanvasGroup canvasGroup; // フェード制御用CanvasGroup

        [HideInInspector] public Vector2 startPos;  // アニメーション開始位置（Awakeで記録）
        [HideInInspector] public Vector3 startScale; // アニメーション開始スケール（Awakeで記録）
    }

    [System.Serializable]
    public class TierPopup
    {
        public PopupEntry[] entries;      // 50% = 2個、100% = 3個 など
        [HideInInspector] public Sequence seq; // このティアの再生中Sequence
    }

    [Header("Tier Popups (Icons only)")]
    [SerializeField] private TierPopup _tier25;  // 25%ティアのポップアップ
    [SerializeField] private TierPopup _tier50;  // 50%ティアのポップアップ（2個）
    [SerializeField] private TierPopup _tier75;  // 75%ティアのポップアップ
    [SerializeField] private TierPopup _tier100; // 100%ティアのポップアップ（3個）

    [Header("Popup Animation")]
    [SerializeField] private float _startScale = 0.75f;      // アニメーション開始スケール
    [SerializeField] private float _popScale = 1.10f;        // ポップ時の最大スケール
    [SerializeField] private float _popTime = 0.18f;         // ポップアニメーション時間
    [SerializeField] private float _riseDistance = 40f;      // 上昇する距離（ピクセル）
    [SerializeField] private float _riseTime = 0.40f;        // 上昇アニメーション時間
    [SerializeField] private float _holdTime = 0.20f;        // 表示を維持する時間
    [SerializeField] private float _fadeOutTime = 0.22f;     // フェードアウト時間

    [Header("Timing")]
    [SerializeField] private float _betweenEntriesDelay = 0.06f; // アイコン間のわずかな間隔
    [SerializeField] private bool _useUnscaledTime = true;        // ポーズ中もアニメーションするか

    private CancellationTokenSource _cts; // 非同期バインド処理のキャンセルトークン
    private bool _bound;                  // MomentumManagerへのバインド状態
    private MomentumState _lastState = MomentumState.None; // 直前のモメンタム状態

    private void Awake()
    {
        // 各ティアの初期位置・スケールを記録し、アイコンを非表示にする
        SetupTier(_tier25);
        SetupTier(_tier50);
        SetupTier(_tier75);
        SetupTier(_tier100);
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        BindAsync(_cts.Token).Forget(); // MomentumManagerが準備できたらイベントをバインド
    }

    private void OnDisable()
    {
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;

        if (_bound && MomentumManager.Instance != null)
            MomentumManager.Instance.onStateChanged.RemoveListener(OnStateChanged); // リスナーを解除
        _bound = false;

        // 全ティアのアニメーションを停止してアイコンを非表示にする
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
      cancellationToken: token); // MomentumManagerとそのイベントが準備できるまで待機

        if (token.IsCancellationRequested) return;

        _lastState = MomentumManager.Instance.CurrentState;
        MomentumManager.Instance.onStateChanged.AddListener(OnStateChanged); // 状態変化イベントを購読
        _bound = true;
    }

    private void OnStateChanged(MomentumState state)
    {
        // このイベントは状態変化時のみ発火するため、連続呼び出しは発生しない
        switch (state)
        {
            case MomentumState.Tier1: PlayTier(_tier25); break;           // 25%ティア達成
            case MomentumState.Tier2: PlayTier(_tier50); break;           // 50%ティア達成（2個）
            case MomentumState.Tier3: PlayTier(_tier75); break;           // 75%ティア達成
            case MomentumState.Max: PlayTier(_tier100); break;            // 100%ティア達成（3個）
        }
    }

    // ─────── 初期化・非表示

    private void SetupTier(TierPopup tier)
    {
        if (tier == null || tier.entries == null) return;

        foreach (var e in tier.entries)
        {
            if (e == null || e.root == null) continue;

            e.startPos = e.root.anchoredPosition;     // 初期位置を保存
            e.startScale = e.root.localScale;          // 初期スケールを保存

            if (e.canvasGroup == null)
                e.canvasGroup = e.root.GetComponent<CanvasGroup>();

            if (e.canvasGroup != null)
                e.canvasGroup.alpha = 0f; // 初期状態は透明

            e.root.gameObject.SetActive(false); // 初期状態は非表示
        }
    }

    // ─────── 再生

    private void PlayTier(TierPopup tier)
    {
        if (tier == null || tier.entries == null || tier.entries.Length == 0) return;

        KillTier(tier); // 既に再生中の場合はリセットしてから再生

        var seq = DOTween.Sequence();

        // entriesを順番に追加する（チュートリアルUIと同様）
        foreach (var e in tier.entries)
        {
            if (e == null || e.root == null) continue;

            seq.Append(CreateEntrySequence(e));              // 各アイコンのアニメーションを順番に追加
            seq.AppendInterval(_betweenEntriesDelay);        // アイコン間に小さな間隔を挿入
        }

        if (_useUnscaledTime)
            seq.SetUpdate(true); // ポーズ中もアニメーションが動くように非スケール時間を使用

        tier.seq = seq;
    }

    private Sequence CreateEntrySequence(PopupEntry e)
    {
        var s = DOTween.Sequence();

        s.AppendCallback(() =>
        {
            e.root.gameObject.SetActive(true);               // アイコンを表示する
            e.root.anchoredPosition = e.startPos;            // 開始位置にリセット
            e.root.localScale = e.startScale * _startScale;  // 縮小スケールから開始

            if (e.canvasGroup != null)
                e.canvasGroup.alpha = 0f; // フェードイン前は透明にする
        });

        // ポップ
        if (e.canvasGroup != null)
            s.Append(e.canvasGroup.DOFade(1f, _popTime)); // フェードインでアイコンを表示
        else
            s.AppendInterval(_popTime);

        s.Join(e.root
            .DOScale(e.startScale * _popScale, _popTime)
            .SetEase(Ease.OutBack)); // OutBackイージングでポップ感を演出

        // 上昇
        s.Append(e.root
            .DOAnchorPos(e.startPos + Vector2.up * _riseDistance, _riseTime)
            .SetEase(Ease.OutQuad)); // 上方向へ滑らかに上昇

        s.Join(e.root
            .DOScale(e.startScale, _riseTime)
            .SetEase(Ease.OutQuad)); // 上昇しながら基準スケールへ戻る

        // ホールド
        s.AppendInterval(_holdTime); // 表示を一定時間維持

        // フェードアウト
        if (e.canvasGroup != null)
            s.Append(e.canvasGroup.DOFade(0f, _fadeOutTime)); // フェードアウトで消える
        else
            s.AppendInterval(_fadeOutTime);

        s.OnComplete(() =>
        {
            if (e.root != null)
                e.root.gameObject.SetActive(false); // アニメーション完了後に非表示
        });

        return s;
    }

    private void KillTier(TierPopup tier)
    {
        if (tier == null) return;

        if (tier.seq != null && tier.seq.IsActive())
            tier.seq.Kill(); // 実行中のSequenceを停止

        if (tier.entries == null) return;

        foreach (var e in tier.entries)
        {
            if (e?.root == null) continue;
            e.root.gameObject.SetActive(false);               // アイコンを非表示にリセット
            if (e.canvasGroup != null) e.canvasGroup.alpha = 0f; // アルファをリセット
        }
    }
}
