// ボタンのホバー・選択・プレス時にスケールやテキストスライドのアニメーションを行うスクリプト
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using MoreMountains.Tools;

public class SquareTextButtonAnimator :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    ISelectHandler,
    IDeselectHandler,
    ISubmitHandler
{
    [Header("参照")]
    [SerializeField] private RectTransform _root;     // ボタンルート（スケール対象）
    [SerializeField] private RectTransform _text;     // TMPテキストのRect（任意）
    [SerializeField] private CanvasGroup _canvasGroup; // 透明度制御用CanvasGroup

    [Header("時間設定")]
    [SerializeField] private bool _ignoreTimeScale = true; // DOTweenで非スケール時間を使用する

    [Header("SE")]
    [SerializeField] private AudioClip _selectSE;
    [SerializeField] private AudioClip _pressSE;
    [SerializeField, Range(0f, 2f)] private float _seVolume = 1f;
    [SerializeField, Range(-3f, 3f)] private float _sePitch = 1f;

    [Header("Scale")]
    [SerializeField] private float _hoverScale = 1.08f;  // ホバー時のスケール
    [SerializeField] private float _scaleTime = 0.12f;   // スケールアニメーション時間

    [Header("Press Punch")]
    [SerializeField] private float _punchStrength = 0.08f;  // プレス時のパンチ強さ
    [SerializeField] private float _punchDuration = 0.15f;  // パンチアニメーション時間

    [Header("Text Slide (optional)")]
    [SerializeField] private float _textSlideX = 6f;      // テキストのスライド距離（ピクセル）
    [SerializeField] private float _textSlideTime = 0.08f; // テキストスライド時間

    [Header("Disabled")]
    [SerializeField] private float _disabledAlpha = 0.5f;  // 無効時のアルファ値
    [SerializeField] private float _disabledScale = 0.95f; // 無効時のスケール

    private bool _interactable = true; // インタラクト可能かどうかのフラグ

    // マウスホバーとOnSelectが同時に鳴るのを防ぐ
    private float _lastSelectSoundTime = -999f;
    private const float SELECT_SOUND_INTERVAL = 0.05f;

    private void Reset()
    {
        _root = transform as RectTransform;
        _text = GetComponentInChildren<TMP_Text>()?.rectTransform; // 子のTMPテキストを自動取得
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (_root == null) _root = transform as RectTransform;
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>(); // なければ追加
    }

    private Tween U(Tween t) => t.SetUpdate(_ignoreTimeScale); // ヘルパー：非スケール時間を設定する

    // ─────────────────────────────────────────────
    // 公開API
    // ─────────────────────────────────────────────

    public void SetInteractable(bool value)
    {
        _interactable = value; // インタラクト状態を更新

        _root.DOKill();
        _canvasGroup.DOKill();

        if (value)
        {
            U(_canvasGroup.DOFade(1f, 0.15f));                      // インタラクト有効時はフェードイン
            U(_root.DOScale(1f, 0.2f).SetEase(Ease.OutBack));       // 通常スケールへポップイン
        }
        else
        {
            U(_canvasGroup.DOFade(_disabledAlpha, 0.15f));           // 無効時は半透明にフェードアウト
            U(_root.DOScale(_disabledScale, 0.15f).SetEase(Ease.InQuad)); // 無効時はわずかに縮小
        }
    }

    // ─────────────────────────────────────────────
    // ホバー / 選択
    // ─────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_interactable) return; // インタラクト不可なら無視
        HoverIn();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_interactable) return;
        HoverOut();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_interactable) return;
        HoverIn(); // キーボード・ゲームパッドの選択もホバーと同じ演出
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!_interactable) return;
        HoverOut();
    }

    private void HoverIn()
    {
        PlaySelectSE(); // 選択音を再生
        _root.DOKill(); // 前のTweenをキャンセル
        U(_root.DOScale(_hoverScale, _scaleTime).SetEase(Ease.OutCubic)); // ホバースケールへアニメーション
    }

    private void HoverOut()
    {
        _root.DOKill();
        U(_root.DOScale(1f, _scaleTime).SetEase(Ease.OutCubic)); // 通常スケールへ戻す
    }

    // ─────────────────────────────────────────────
    // プレス / 決定
    // ─────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_interactable) return;
        Press(); // マウスクリック時にプレスアニメーションを再生
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!_interactable) return;
        Press(); // キーボード・ゲームパッドの決定ボタンでもプレスアニメーションを再生
    }

    private void Press()
    {
        PlayPressSE(); // プレス音を再生

        _root.DOKill();
        U(_root.DOPunchScale(
            Vector3.one * _punchStrength,
            _punchDuration,
            vibrato: 10,
            elasticity: 0.8f // 弾力性で跳ね返り感を演出
        ));

        if (_text != null)
        {
            _text.DOKill();
            U(_text.DOLocalMoveX(_textSlideX, _textSlideTime)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => U(_text.DOLocalMoveX(0f, _textSlideTime)))); // テキストを右へスライドして元に戻す
        }
    }

    /// <summary>
    /// 選択音を再生する。マウスホバーとOnSelectが同時に鳴るのを防ぐため、連続再生を短時間制限する。
    /// </summary>
    private void PlaySelectSE()
    {
        if (_selectSE == null) return;

        if (Time.unscaledTime - _lastSelectSoundTime < SELECT_SOUND_INTERVAL)
            return;

        _lastSelectSoundTime = Time.unscaledTime;
        MMSoundManagerSoundPlayEvent.Trigger(
            _selectSE,
            MMSoundManager.MMSoundManagerTracks.UI,
            Vector3.zero,
            volume: _seVolume,
            pitch: _sePitch);
    }

    /// <summary>
    /// プレス音を再生する。DOTweenのパンチアニメーションと組み合わせて、クリック感を強調する。
    /// </summary>
    private void PlayPressSE()
    {
        if (_pressSE == null) return;
        MMSoundManagerSoundPlayEvent.Trigger(
            _pressSE,
            MMSoundManager.MMSoundManagerTracks.UI,
            Vector3.zero,
            volume: _seVolume,
            pitch: _sePitch);
    }
}
