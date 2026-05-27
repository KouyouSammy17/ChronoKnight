// モメンタムゲージのUI表示・アニメーション・グロー演出を管理するスクリプト
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;
using MoreMountains.Feedbacks;

public class MomentumGaugeUI : MonoBehaviour
{
    [SerializeField] private Slider _momentumSlider;        // モメンタム値を表示するスライダー
    [SerializeField] private float _tweenDuration = 0.3f;   // スライダー変化時のTween時間
    [SerializeField] private Ease _ease = Ease.OutQuad;
    [SerializeField] private bool _animateWhilePaused = true; // ポーズ中もアニメーションするか

    // ← add this
    [Header("Visibility (optional)")]
    [SerializeField] private CanvasGroup _group;             // フェード制御用CanvasGroup
    [SerializeField] private bool _startHidden = false;      // 起動時に非表示で開始するか
    [SerializeField] private float _showHideDuration = 0.25f; // 表示・非表示フェード時間

    [Header("Show Animation (Scan-in)")]
    [SerializeField] private RectTransform _root;   // gauge root rect (usually this transform)
    [SerializeField] private bool _useScanIn = true;            // スキャンインアニメーションを使用するか
    [SerializeField] private float _fromScale = 0.90f;          // アニメーション開始時のスケール
    [SerializeField] private float _overshootScale = 1.03f;     // オーバーシュート時のスケール

    [SerializeField] private bool _useSlide = false;
    [SerializeField] private float _slideY = 18f;   // pixels

    [Header("Fill Glow (FEEL)")]
    [SerializeField] private Image _fillImage;                  // ゲージのフィル画像
    [SerializeField] private MMFeedbacks _fillGlowBurst; // FB_FillGlowBurst

    [Header("Glow (>= 50%) - FEEL")]
    [SerializeField] private GameObject _glowRoot;       // GlowImage GameObject
    [SerializeField] private CanvasGroup _glowGroup;     // GlowImage上のCanvasGroup
    [SerializeField] private MMFeedbacks _glowAppear;    // plays once when enabling
    [SerializeField] private MMFeedbacks _glowBlinkLoop; // loops while enabled
    [SerializeField, Range(0f, 1f)] private float _glowStartPct = 0.5f; // グロー開始するモメンタム割合

    [Header("Outside Glow (ONLY at 100%) - FEEL")]
    [SerializeField] private GameObject _glowOutsideRoot;        // GlowOutsideのGameObject
    [SerializeField] private CanvasGroup _glowOutsideGroup;      // GlowOutside上のCanvasGroup
    [SerializeField] private MMFeedbacks _glowOutsideAppear;     // plays once when enabling
    [SerializeField] private MMFeedbacks _glowOutsideBlinkLoop;  // loops while enabled
    [SerializeField] private float _maxEpsilon = 0.001f;         // float safety

    [Header("Tutorial Highlight")]
    [SerializeField] private GameObject _highlightRoot;      // the highlight Image object
    [SerializeField] private UIHighlightPulse _highlightPulse;

    private Tween _valueTween;         // スライダー値変化のTween
    private float _lastValue = -999f;  // 前回のモメンタム値（初回更新判定用）
    private bool _isBound = false;     // MomentumManagerへのバインド状態
    private CancellationTokenSource _cts; // 非同期処理のキャンセルトークン
    private Sequence _showSeq;         // 表示アニメーションのSequence
    private Vector3 _baseScale;        // ルートの基準スケール
    private Vector2 _baseAnchoredPos;  // ルートの基準アンカー位置
    private bool _glowOn;              // 内側グローの有効状態
    private bool _outsideGlowOn;       // 外側グローの有効状態
    private Color _baseFillColor;      // フィル画像の基準色

    private void Awake()
    {
        if (_momentumSlider == null) _momentumSlider = GetComponent<Slider>();
        if (_group == null) _group = GetComponent<CanvasGroup>();
        if (_group == null) { _group = gameObject.AddComponent<CanvasGroup>(); } // 安全なデフォルト
        if (_fillImage != null) _baseFillColor = _fillImage.color; // フィル色を保存
        if (_glowRoot != null) _glowRoot.SetActive(false);   // グローは初期非表示
        if (_glowGroup != null) _glowGroup.alpha = 0f;
        if (_glowOutsideRoot != null) _glowOutsideRoot.SetActive(false); // 外側グローも初期非表示
        if (_glowOutsideGroup != null) _glowOutsideGroup.alpha = 0f;
        _glowOn = false;
        _outsideGlowOn = false;

        if (_startHidden)
            SetVisible(false, instant: true); // 初期非表示設定に従って即座に非表示

        // ハイライトを確実に非表示にする
        if (_highlightRoot != null)
            _highlightRoot.SetActive(false);

        if (_root == null) _root = transform as RectTransform;
        _baseScale = _root.localScale;           // スケールの基準値を保存
        _baseAnchoredPos = _root.anchoredPosition; // 位置の基準値を保存
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindWhenReadyAsync(_cts.Token).Forget(); // MomentumManagerが準備できたらバインド開始
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _cts?.Cancel(); _cts?.Dispose(); _cts = null; // 非同期処理をキャンセルしてリソースを解放
        Unbind();
        _valueTween?.Kill(); _valueTween = null;
        _showSeq?.Kill();
        _showSeq = null;
        if (_fillImage != null) _fillImage.color = _baseFillColor; // フィル色を元に戻す
        _glowBlinkLoop?.StopFeedbacks();
        _glowAppear?.StopFeedbacks();
        if (_glowGroup != null) _glowGroup.alpha = 0f;
        if (_glowRoot != null) _glowRoot.SetActive(false);
        _glowOutsideBlinkLoop?.StopFeedbacks();
        _glowOutsideAppear?.StopFeedbacks();
        if (_glowOutsideGroup != null) _glowOutsideGroup.alpha = 0f;
        if (_glowOutsideRoot != null) _glowOutsideRoot.SetActive(false);
        _glowOn = false;
        _outsideGlowOn = false;
    }

    private void OnDestroy()
    {
        Unbind();
        _valueTween?.Kill(); _valueTween = null;
        _showSeq?.Kill();
        _showSeq = null;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        _cts?.Cancel(); // シーン切り替え時に古い非同期処理をキャンセル
        _cts = new CancellationTokenSource();
        BindWhenReadyAsync(_cts.Token).Forget(); // 新しいシーンでMomentumManagerへ再バインド
    }

    private async UniTaskVoid BindWhenReadyAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() => MomentumManager.Instance != null && _momentumSlider != null, cancellationToken: token); // MomentumManagerとスライダーが準備できるまで待機
        await UniTask.Yield(PlayerLoopTiming.Update, token); // 1フレーム待機して初期化が安定するのを待つ
        if (token.IsCancellationRequested) return;

        var mm = MomentumManager.Instance;
        if (mm == null) return;

        Unbind(); // 既存のバインドを解除
        _momentumSlider.wholeNumbers = false;
        _momentumSlider.minValue = 0f;
        _momentumSlider.maxValue = mm.MaxMomentum; // スライダーの最大値をMomentumManagerから取得

        mm.onMomentumChanged.AddListener(OnMomentumChanged); // モメンタム変化イベントを購読
        _isBound = true;

        _lastValue = -999f;
        OnMomentumChanged(mm.CurrentMomentum); // 現在のモメンタム値で即時反映
    }

    private void Unbind()
    {
        if (_isBound && MomentumManager.Instance != null)
            MomentumManager.Instance.onMomentumChanged.RemoveListener(OnMomentumChanged); // イベントリスナーを解除
        _isBound = false;
    }

    private void OnMomentumChanged(float m)
    {
        if (_momentumSlider == null) return;

        float target = Mathf.Clamp(m, 0f, _momentumSlider.maxValue); // 0〜最大値の範囲にクランプ

        // _lastValueを更新する前に増加を検知する
        bool increased = (_lastValue > -900f) && (target > _lastValue + 0.0001f); // モメンタムが増加したか判定

        if (Mathf.Approximately(_lastValue, target)) return; // 値が変わっていなければスキップ
        _lastValue = target;

        // スライダーのTweenを更新する
        _valueTween?.Kill();
        _valueTween = _momentumSlider
            .DOValue(target, _tweenDuration)
            .SetEase(_ease)
            .SetUpdate(_animateWhilePaused)
            .SetLink(_momentumSlider.gameObject, LinkBehaviour.KillOnDestroy);

        // モメンタム増加時にFEELバーストエフェクトを再生
        if (increased)
            _fillGlowBurst?.PlayFeedbacks(); // モメンタムが増加したときにバーストエフェクト再生

        // 50%以上でFEELループグローを有効化
        float pct = (_momentumSlider.maxValue <= 0f) ? 0f : (target / _momentumSlider.maxValue); // モメンタムの割合を計算
        SetLoopGlow(pct >= _glowStartPct); // 閾値以上でグローループを有効化

        bool isMax = (_momentumSlider.maxValue > 0f) && (target >= _momentumSlider.maxValue - _maxEpsilon); // 最大値に達したか判定
        SetOutsideGlowMaxOnly(isMax); // 最大時のみ外側グローを有効化
    }


    // ↓ タイムラインから呼び出せるヘルパー ─────────────────────────────────────
    public void TL_HideGauge() => SetVisible(false, instant: false); // タイムラインからゲージを非表示にする
    public void TL_ShowGauge() => SetVisible(true, instant: false);  // タイムラインからゲージを表示する

    private void SetVisible(bool visible, bool instant)
    {
        if (_group == null)
        {
            gameObject.SetActive(visible); // CanvasGroupがなければオブジェクトの有効状態で制御
            return;
        }

        // 前回の表示アニメーションを停止する
        _showSeq?.Kill();
        _showSeq = null;

        bool useUnscaled = _animateWhilePaused; // 設定を尊重する

        if (visible)
        {
            // 重要：レンダリングとTweenの実行にはアクティブである必要がある
            if (!gameObject.activeSelf) gameObject.SetActive(true); // Tweenを動かすにはアクティブである必要がある

            if (instant || !Application.isPlaying)
            {
                _group.alpha = 1f;
                _group.interactable = true;
                _group.blocksRaycasts = true;

                if (_root != null)
                {
                    _root.localScale = _baseScale;
                    _root.anchoredPosition = _baseAnchoredPos;
                }
                return;
            }

            _group.alpha = 0f;
            _group.interactable = true;
            _group.blocksRaycasts = true;

            if (_root == null || !_useScanIn)
            {
                _group.DOFade(1f, _showHideDuration).SetUpdate(useUnscaled); // シンプルなフェードイン
                return;
            }

            // 開始状態を設定する
            _root.localScale = _baseScale * _fromScale; // アニメーション開始スケールに設定
            if (_useSlide)
                _root.anchoredPosition = _baseAnchoredPos - new Vector2(0f, _slideY); // スライド開始位置に設定

            _showSeq = DOTween.Sequence().SetUpdate(useUnscaled);
            _showSeq.Join(_group.DOFade(1f, _showHideDuration).SetEase(Ease.OutQuad));
            _showSeq.Join(_root.DOScale(_baseScale * _overshootScale, _showHideDuration).SetEase(Ease.OutBack)); // オーバーシュートしてポップイン
            _showSeq.Append(_root.DOScale(_baseScale, 0.10f).SetEase(Ease.OutQuad)); // 基準スケールに収束

            if (_useSlide)
                _showSeq.Join(_root.DOAnchorPos(_baseAnchoredPos, _showHideDuration + 0.08f).SetEase(Ease.OutCubic)); // 基準位置へスライドイン

            _showSeq.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            return;
        }

        // 非表示
        if (instant || !Application.isPlaying)
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            // 任意：即座に無効化する場合はコメントを外す
            // gameObject.SetActive(false);
            return;
        }

        _group.interactable = false;
        _group.blocksRaycasts = false;

        _group.DOFade(0f, _showHideDuration) // フェードアウト
              .SetUpdate(useUnscaled)
              .OnComplete(() =>
              {
                  // 任意：フェード後にオブジェクトを無効化する場合はコメントを外す
                  // gameObject.SetActive(false);
              });
    }


    public void ShowTutorialHighlight(bool show)
    {
        if (_highlightRoot == null) return;

        _highlightRoot.SetActive(show); // チュートリアルハイライトの表示切り替え

        if (!show && _highlightPulse != null)
        {
            _highlightPulse.StopPulse(); // 非表示時にパルスアニメーションを停止
        }
    }

    private void SetLoopGlow(bool enable)
    {
        if (_glowRoot == null) return;

        if (_glowOn == enable) return; // 状態が変わっていなければ何もしない
        _glowOn = enable;

        if (enable)
        {
            // まずオブジェクトを表示する
            _glowRoot.SetActive(true);

            // 開始アルファをリセットする（appearが常に正しく見えるように）
            if (_glowGroup != null) _glowGroup.alpha = 0f;

            // appearを1回再生してから点滅ループを開始する
            _glowAppear?.PlayFeedbacks();    // 表示アニメーションを1回再生
            _glowBlinkLoop?.PlayFeedbacks(); // 点滅ループアニメーションを開始
        }
        else
        {
            // ループを停止して非表示にする
            _glowBlinkLoop?.StopFeedbacks(); // 点滅ループを停止
            _glowAppear?.StopFeedbacks();

            if (_glowGroup != null) _glowGroup.alpha = 0f;
            _glowRoot.SetActive(false); // グローオブジェクトを非表示
        }
    }

    private void SetOutsideGlowMaxOnly(bool enable)
    {
        if (_glowOutsideRoot == null) return;

        if (_outsideGlowOn == enable) return; // 状態が変わっていなければ何もしない
        _outsideGlowOn = enable;

        if (enable)
        {
            _glowOutsideRoot.SetActive(true);
            if (_glowOutsideGroup != null) _glowOutsideGroup.alpha = 0f;

            _glowOutsideAppear?.PlayFeedbacks();    // 外側グローの表示アニメーションを再生
            _glowOutsideBlinkLoop?.PlayFeedbacks(); // 外側グローの点滅ループを開始
        }
        else
        {
            _glowOutsideBlinkLoop?.StopFeedbacks(); // 外側グローの点滅ループを停止
            _glowOutsideAppear?.StopFeedbacks();

            if (_glowOutsideGroup != null) _glowOutsideGroup.alpha = 0f;
            _glowOutsideRoot.SetActive(false); // 外側グローを非表示
        }
    }

}
