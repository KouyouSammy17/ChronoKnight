// モメンタム（勢い）システムのチュートリアルUIを段階的なアニメーションで表示するスクリプト
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using MoreMountains.Feedbacks;

public class MomentumTutorialUI : MonoBehaviour
{
    [Header("Slider & Value Text")]
    [SerializeField] private Slider _momentumSlider;    // モメンタム量を示すスライダー
    [SerializeField] private TMP_Text _valueText;       // 数値を表示するテキスト

    [Header("Slider Animation (stepped)")]
    [SerializeField] private float _segmentDuration = 0.6f;      // 0→25, 25→50 etc   // 各区間のアニメーション時間
    [SerializeField] private float _segmentPauseTime = 0.3f;     // small stop at 25,50,75 // 各段階で一時停止する時間
    [SerializeField] private float _holdAtMaxSliderTime = 1.0f;  // wait at 100 before restart // 最大値で待機する時間
    [SerializeField] private bool _useUnscaledTime = true;       // ポーズ中でもアニメーションを継続するか

    [Header("Value Display")]
    [SerializeField] private bool _showAsPercent = true;    // パーセント表示を使用するか
    [SerializeField] private int _decimals = 0;             // 小数点以下の桁数

    // ───────────────────────────── Tier popup data

    [System.Serializable]
    public class PopupEntry
    {
        public RectTransform root;   // parent (icon + text)    // ポップアップの親オブジェクト
        public CanvasGroup canvasGroup;                         // フェード制御用キャンバスグループ

        [HideInInspector] public Vector2 startPos;      // 初期アンカー位置を保存
        [HideInInspector] public Vector3 startScale;    // 初期スケールを保存
    }

    [System.Serializable]
    public class TierPopup
    {
        public PopupEntry[] entries;      // 1 or 2 buffs    // 表示するバフエントリーの配列
        [HideInInspector] public Sequence seq;               // アニメーション用シーケンス
    }

    [Header("Tier Popups")]
    [SerializeField] private TierPopup _tier25Popup;    // 25%達成時のポップアップ
    [SerializeField] private TierPopup _tier50Popup;    // 50%達成時のポップアップ
    [SerializeField] private TierPopup _tier75Popup;    // 75%達成時のポップアップ
    [SerializeField] private TierPopup _tier100Popup;   // 100%達成時のポップアップ

    [Header("Popup Animation")]
    [SerializeField] private float _popupStartScale = 0.7f;     // ポップアップ出現時の初期スケール
    [SerializeField] private float _popupPopScale = 1.1f;       // 弾けるアニメーションの最大スケール
    [SerializeField] private float _popupPopTime = 0.2f;        // 弾けるアニメーションの時間
    [SerializeField] private float _popupRiseDistance = 40f;    // 浮上する距離（ピクセル）
    [SerializeField] private float _popupRiseTime = 0.4f;       // 浮上にかかる時間
    [SerializeField] private float _popupHoldTime = 0.3f;       // 表示を維持する時間
    [SerializeField] private float _popupFadeOutTime = 0.25f;   // フェードアウトにかかる時間

    [Header("FEEL Feedbacks (Gauge FX)")]
    [SerializeField] private MMF_Player _tier25GaugeFx;     // 25%達成時のゲージエフェクト
    [SerializeField] private MMF_Player _tier50GaugeFx;     // 50%達成時のゲージエフェクト
    [SerializeField] private MMF_Player _tier75GaugeFx;     // 75%達成時のゲージエフェクト
    [SerializeField] private MMF_Player _tier100GaugeFx;    // 100%達成時のゲージエフェクト

    // ───────────────────────────── internal state

    private Sequence _loopSequence;         // スライダーループ全体を管理するシーケンス
    private float _lastPercent = 0f;        // 前フレームのスライダー値（閾値通過検出用）

    private bool _tier25Triggered;     // 25%ポップアップが既に発動済みか
    private bool _tier50Triggered;     // 50%ポップアップが既に発動済みか
    private bool _tier75Triggered;     // 75%ポップアップが既に発動済みか
    private bool _tier100Triggered;    // 100%ポップアップが既に発動済みか

    // ───────────────────────────── Unity

    private void Awake()
    {
        if (_momentumSlider == null)
            _momentumSlider = GetComponentInChildren<Slider>();     // 子オブジェクトからSliderを自動取得

        // 各ティアポップアップの初期状態を設定
        SetupTier(_tier25Popup);
        SetupTier(_tier50Popup);
        SetupTier(_tier75Popup);
        SetupTier(_tier100Popup);
    }

    private void OnEnable()
    {
        StartLoop();    // 有効化時にスライダーのループアニメーションを開始
    }

    private void OnDisable()
    {
        StopLoop();     // 無効化時にすべてのアニメーションを停止
    }

    // ───────────────────────────── setup

    private void SetupTier(TierPopup tier)
    {
        if (tier == null || tier.entries == null) return;

        foreach (var e in tier.entries)
        {
            if (e == null || e.root == null) continue;

            e.startPos = e.root.anchoredPosition;   // 初期位置を記録
            e.startScale = e.root.localScale;        // 初期スケールを記録

            if (e.canvasGroup == null)
                e.canvasGroup = e.root.GetComponent<CanvasGroup>();

            if (e.canvasGroup != null)
                e.canvasGroup.alpha = 0f;   // 最初は透明に設定

            e.root.gameObject.SetActive(false); // 初期状態では非表示
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

        _loopSequence?.Kill();  // 既存のシーケンスがあれば停止

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

        _loopSequence.SetLoops(-1, LoopType.Restart);   // 無限ループ設定
        _loopSequence.OnStepComplete(OnLoopStepComplete);

        if (_useUnscaledTime)
            _loopSequence.SetUpdate(true);  // Time.timeScale = 0 でも動作させる
    }

    private Tween CreateSegmentTween(float targetPercent)
    {
        return _momentumSlider
            .DOValue(targetPercent, _segmentDuration)
            .SetEase(Ease.Linear)
            .OnUpdate(OnSliderUpdated); // スライダー更新のたびにコールバックを呼ぶ
    }

    private void StopLoop()
    {
        if (_loopSequence != null && _loopSequence.IsActive())
            _loopSequence.Kill();   // メインループを停止

        // 各ティアポップアップのアニメーションも停止
        KillTier(_tier25Popup);
        KillTier(_tier50Popup);
        KillTier(_tier75Popup);
        KillTier(_tier100Popup);
    }

    private void OnLoopStepComplete()
    {
        // Called after 0→25→50→75→100 + pauses + hold-at-max finishes
        ResetTierFlags();   // 次のループのためにフラグをリセット

        _momentumSlider.value = 0f;
        _lastPercent = 0f;
        UpdateValueText();
    }

    // ───────────────────────────── slider update

    private void OnSliderUpdated()
    {
        UpdateValueText();

        float currentPercent = _momentumSlider.value;

        // 各閾値を超えたかチェックしてポップアップを発動
        CheckTier(25f, ref _tier25Triggered, _tier25Popup);
        CheckTier(50f, ref _tier50Triggered, _tier50Popup);
        CheckTier(75f, ref _tier75Triggered, _tier75Popup);
        CheckTier(100f, ref _tier100Triggered, _tier100Popup);

        _lastPercent = currentPercent;  // 次フレームの比較用に現在値を保存
    }

    private void UpdateValueText()
    {
        if (_valueText == null || _momentumSlider == null) return;

        float current = _momentumSlider.value;
        float max = _momentumSlider.maxValue;

        if (_showAsPercent)
        {
            float percent = (max > 0f) ? (current / max) * 100f : 0f;  // 0〜100%に換算
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

        // 前フレームより低く、今フレームで閾値以上になった瞬間のみ発動
        if (!flag && _lastPercent < threshold && currentPercent >= threshold)
        {
            flag = true;

            // buff popups
            PlayTier(tier);         // バフポップアップを表示

            // NEW: gauge effects
            PlayGaugeFxForThreshold(threshold); // ゲージエフェクトを再生
        }
    }

    private void ResetTierFlags()
    {
        // すべての発動フラグをリセット（ループ再開時に使用）
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

        KillTier(tier); // 既存アニメーションをキャンセル

        var seq = DOTween.Sequence();

        // entries are appended → play one by one
        foreach (var e in tier.entries)
        {
            if (e == null || e.root == null) continue;
            seq.Append(CreateEntrySequence(e)); // 各エントリーを順番に再生
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
            e.root.gameObject.SetActive(true);              // ポップアップを表示
            e.root.anchoredPosition = e.startPos;           // 初期位置にリセット
            e.root.localScale = e.startScale * _popupStartScale; // 小さいスケールから開始

            if (e.canvasGroup != null)
                e.canvasGroup.alpha = 0f;   // 透明から開始
        });

        // POP
        if (e.canvasGroup != null)
            s.Append(e.canvasGroup.DOFade(1f, _popupPopTime)); // フェードイン
        else
            s.AppendInterval(_popupPopTime);

        s.Join(e.root
            .DOScale(e.startScale * _popupPopScale, _popupPopTime)
            .SetEase(Ease.OutBack));    // 弾けるようなスケールアップ

        // RISE
        s.Append(e.root
            .DOAnchorPos(e.startPos + Vector2.up * _popupRiseDistance, _popupRiseTime)
            .SetEase(Ease.OutQuad));    // 上方向へ浮上
        s.Join(e.root
            .DOScale(e.startScale, _popupRiseTime)
            .SetEase(Ease.OutQuad));    // 標準スケールに戻す

        // HOLD
        s.AppendInterval(_popupHoldTime);   // 表示を維持

        // FADE OUT
        if (e.canvasGroup != null)
            s.Append(e.canvasGroup.DOFade(0f, _popupFadeOutTime));  // フェードアウト
        else
            s.AppendInterval(_popupFadeOutTime);

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
            tier.seq.Kill();    // 実行中のシーケンスを停止

        if (tier.entries == null) return;

        foreach (var e in tier.entries)
        {
            if (e == null || e.root == null) continue;
            e.root.gameObject.SetActive(false); // エントリーを非表示にリセット
        }
    }

    private void PlayGaugeFxForThreshold(float threshold)
    {
        // choose which MMF_Player to fire
        if (Mathf.Approximately(threshold, 25f))
        {
            _tier25GaugeFx?.StopFeedbacks();
            _tier25GaugeFx?.PlayFeedbacks();    // 25%エフェクトを再生
        }
        else if (Mathf.Approximately(threshold, 50f))
        {
            _tier50GaugeFx?.StopFeedbacks();
            _tier50GaugeFx?.PlayFeedbacks();    // 50%エフェクトを再生
        }
        else if (Mathf.Approximately(threshold, 75f))
        {
            _tier75GaugeFx?.StopFeedbacks();
            _tier75GaugeFx?.PlayFeedbacks();    // 75%エフェクトを再生
        }
        else if (Mathf.Approximately(threshold, 100f))
        {
            _tier100GaugeFx?.StopFeedbacks();
            _tier100GaugeFx?.PlayFeedbacks();   // 100%エフェクトを再生
        }
    }
    // ───────────────────────────── UI Button hook

    public void OnClickContinue()
    {
        TutorialManager.Instance?.CompleteTutorial(TutorialKey.Momentum); // 「続ける」ボタンでモメンタムチュートリアルを完了
    }

}
