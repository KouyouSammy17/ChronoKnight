using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class SelectSwap : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [Header("Visuals")]
    [SerializeField] private GameObject inactiveRoot;
    [SerializeField] private GameObject activeRoot;

    [Header("Tween")]
    [SerializeField] private Transform tweenTarget;         // usually the button root or a child frame
    [SerializeField] private float selectedScale = 1.05f;   // how big on select
    [SerializeField] private float selectDur = 0.08f;
    [SerializeField] private float deselectDur = 0.10f;
    [SerializeField] private Ease selectEase = Ease.OutQuad;
    [SerializeField] private Ease deselectEase = Ease.OutQuad;
    [SerializeField] private bool useUnscaledTime = true;   // animate while paused

    private Selectable _sel;
    private Tween _scaleTween;

    private void Awake()
    {
        _sel = GetComponent<Selectable>();
        if (!tweenTarget) tweenTarget = transform;

        // initial state: not selected
        SetVisual(false);
        SafeKill();
        tweenTarget.localScale = Vector3.one;
    }

    private void OnDisable() => SafeKill();
    private void OnDestroy() => SafeKill();

    public void OnSelect(BaseEventData eventData)
    {
        SetVisual(true);
        TweenTo(selectedScale, selectDur, selectEase);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetVisual(false);
        TweenTo(1f, deselectDur, deselectEase);
    }

    // Hovering with mouse should also move selection (so visuals match keyboard/gamepad)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!EventSystem.current || !_sel || !_sel.interactable) return;
        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    private void SetVisual(bool selected)
    {
        if (inactiveRoot) inactiveRoot.SetActive(!selected);
        if (activeRoot) activeRoot.SetActive(selected);
    }

    private void TweenTo(float target, float dur, Ease ease)
    {
        SafeKill();
        _scaleTween = tweenTarget
            .DOScale(target, dur)
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)            // <- works while paused
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void SafeKill()
    {
        if (_scaleTween != null && _scaleTween.IsActive())
            _scaleTween.Kill();
        _scaleTween = null;
    }
}
