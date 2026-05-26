// UIElementを拡縮・透明度でパルスアニメーションさせてハイライトするスクリプト
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UIHighlightPulse : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform _target; // frame RectTransform   // パルスさせる対象のRectTransform
    [SerializeField] private Graphic _graphic;      // Image, etc.           // 色・透明度を変化させる対象のGraphic

    [Header("Pulse Settings")]
    [SerializeField] private float _scaleMultiplier = 1.06f;    // パルス時の最大スケール倍率
    [SerializeField] private float _duration = 0.6f;            // 1回のパルスにかかる時間
    [SerializeField] private float _minAlpha = 0.65f;           // 透明度の最小値
    [SerializeField] private float _maxAlpha = 1.0f;            // 透明度の最大値

    [Header("Time")]
    [SerializeField] private bool _ignoreTimeScale = true;  // タイムスケールを無視してアニメーションするか

    private Sequence _seq;          // パルスアニメーションのシーケンス
    private Vector3 _baseScale;     // 初期スケール（リセット用）
    private Color _baseColor;       // 初期カラー（リセット用）

    private void Awake()
    {
        if (_target == null) _target = transform as RectTransform;  // 自身のRectTransformを使用
        if (_graphic == null) _graphic = GetComponent<Graphic>();   // 自身のGraphicコンポーネントを使用

        _baseScale = _target.localScale;    // 初期スケールを記録
        _baseColor = _graphic.color;        // 初期カラーを記録
    }

    private void OnEnable()
    {
        PlayPulse();    // 有効化時にパルスを開始
    }

    private void OnDisable()
    {
        KillSequence(); // シーケンスを停止

        // reset
        if (_target != null) _target.localScale = _baseScale;  // スケールをリセット
        if (_graphic != null) _graphic.color = _baseColor;     // カラーをリセット
    }

    public void PlayPulse()
    {
        if (_graphic == null || _target == null) return;

        KillSequence(); // 既存のシーケンスをキャンセル

        Color cMin = _baseColor; cMin.a = _minAlpha;    // 最小透明度のカラー
        Color cMax = _baseColor; cMax.a = _maxAlpha;    // 最大透明度のカラー

        _graphic.color = cMin;          // 最小透明度から開始
        _target.localScale = _baseScale;

        _seq = DOTween.Sequence();
        if (_ignoreTimeScale) _seq.SetUpdate(true); // works when Time.timeScale = 0

        _seq
            .Join(_target.DOScale(_baseScale * _scaleMultiplier, _duration)
                          .SetEase(Ease.OutQuad))               // スケールをゆっくり拡大
            .Join(_graphic.DOColor(cMax, _duration))            // 透明度を上げる
            .SetLoops(-1, LoopType.Yoyo);                       // ヨーヨーで無限ループ
    }

    public void StopPulse()
    {
        KillSequence();

        if (_target != null) _target.localScale = _baseScale;  // スケールを初期値に戻す
        if (_graphic != null) _graphic.color = _baseColor;     // カラーを初期値に戻す
    }

    private void KillSequence()
    {
        if (_seq != null)
        {
            _seq.Kill();    // シーケンスを強制停止
            _seq = null;
        }
    }
}
