// ボタンの選択状態に応じてビジュアルを切り替え、スケールアニメーションを行うスクリプト
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class SelectSwap : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [Header("Visuals")]
    [SerializeField] private GameObject inactiveRoot; // 非選択時に表示するオブジェクト
    [SerializeField] private GameObject activeRoot;   // 選択時に表示するオブジェクト

    [Header("Tween")]
    [SerializeField] private Transform tweenTarget;         // 通常はボタンルートまたは子フレーム
    [SerializeField] private float selectedScale = 1.05f;   // 選択時の拡大スケール
    [SerializeField] private float selectDur = 0.08f;       // 選択時のアニメーション時間
    [SerializeField] private float deselectDur = 0.10f;     // 非選択時のアニメーション時間
    [SerializeField] private Ease selectEase = Ease.OutQuad;
    [SerializeField] private Ease deselectEase = Ease.OutQuad;
    [SerializeField] private bool useUnscaledTime = true;   // ポーズ中もアニメーションする

    private Selectable _sel;   // このオブジェクトのSelectableコンポーネント
    private Tween _scaleTween; // 現在実行中のスケールTween

    private void Awake()
    {
        _sel = GetComponent<Selectable>();
        if (!tweenTarget) tweenTarget = transform; // tweenTargetが未設定なら自身を使用
        ForceUnselectImmediate(); // use the same logic
    }

    private void OnDisable() => SafeKill(); // 無効化時にTweenを停止
    private void OnDestroy() => SafeKill(); // 破棄時にTweenを停止

    public void OnSelect(BaseEventData eventData)
    {
        SetVisual(true);                                    // 選択状態のビジュアルに切り替え
        TweenTo(selectedScale, selectDur, selectEase);     // 選択スケールへアニメーション
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetVisual(false);                                       // 非選択状態のビジュアルに切り替え
        TweenTo(1f, deselectDur, deselectEase);               // 通常スケールへアニメーション
    }

    public void ForceUnselectImmediate()
    {
        SafeKill(); // 実行中のTweenを即座に停止

        if (inactiveRoot) inactiveRoot.SetActive(true);  // 非選択ビジュアルを表示
        if (activeRoot) activeRoot.SetActive(false);     // 選択ビジュアルを非表示

        if (tweenTarget)
            tweenTarget.localScale = Vector3.one; // スケールをリセット
    }

    // マウスホバーでも選択を移動させる（キーボード・ゲームパッドとビジュアルを合わせるため）
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!EventSystem.current || !_sel || !_sel.interactable) return; // インタラクト不可なら無視
        EventSystem.current.SetSelectedGameObject(gameObject); // マウスホバーでフォーカスを移動
    }

    private void SetVisual(bool selected)
    {
        if (inactiveRoot) inactiveRoot.SetActive(!selected); // 選択状態に応じて非選択ビジュアルを切り替え
        if (activeRoot) activeRoot.SetActive(selected);      // 選択状態に応じて選択ビジュアルを切り替え
    }

    private void TweenTo(float target, float dur, Ease ease)
    {
        SafeKill(); // 前のTweenを停止してから新しいTweenを開始
        _scaleTween = tweenTarget
            .DOScale(target, dur)
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)            // <- ポーズ中も動作する
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void SafeKill()
    {
        if (_scaleTween != null && _scaleTween.IsActive())
            _scaleTween.Kill(); // アクティブなTweenのみKillする
        _scaleTween = null;
    }
}
