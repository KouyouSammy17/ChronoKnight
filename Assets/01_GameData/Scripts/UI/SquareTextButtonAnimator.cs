using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class SquareTextButtonAnimator :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    ISelectHandler,
    IDeselectHandler,
    ISubmitHandler
{
    [Header("References")]
    [SerializeField] private RectTransform _root;     // button root (scale)
    [SerializeField] private RectTransform _text;     // TMP text rect (optional)
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Scale")]
    [SerializeField] private float _hoverScale = 1.08f;
    [SerializeField] private float _scaleTime = 0.12f;

    [Header("Press Punch")]
    [SerializeField] private float _punchStrength = 0.08f;
    [SerializeField] private float _punchDuration = 0.15f;

    [Header("Text Slide (optional)")]
    [SerializeField] private float _textSlideX = 6f;
    [SerializeField] private float _textSlideTime = 0.08f;

    [Header("Disabled")]
    [SerializeField] private float _disabledAlpha = 0.5f;
    [SerializeField] private float _disabledScale = 0.95f;

    private bool _interactable = true;

    private void Reset()
    {
        _root = transform as RectTransform;
        _text = GetComponentInChildren<TMP_Text>()?.rectTransform;
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (_root == null)
            _root = transform as RectTransform;

        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Public API
    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    public void SetInteractable(bool value)
    {
        _interactable = value;

        _root.DOKill();
        _canvasGroup.DOKill();

        if (value)
        {
            _canvasGroup.DOFade(1f, 0.15f);
            _root.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
        }
        else
        {
            _canvasGroup.DOFade(_disabledAlpha, 0.15f);
            _root.DOScale(_disabledScale, 0.15f).SetEase(Ease.InQuad);
        }
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Hover / Select
    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_interactable) return;
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
        HoverIn();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!_interactable) return;
        HoverOut();
    }

    private void HoverIn()
    {
        _root.DOKill();
        _root.DOScale(_hoverScale, _scaleTime).SetEase(Ease.OutCubic);
    }

    private void HoverOut()
    {
        _root.DOKill();
        _root.DOScale(1f, _scaleTime).SetEase(Ease.OutCubic);
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Press / Submit
    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_interactable) return;
        Press();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!_interactable) return;
        Press();
    }

    private void Press()
    {
        _root.DOKill();
        _root.DOPunchScale(
            Vector3.one * _punchStrength,
            _punchDuration,
            vibrato: 10,
            elasticity: 0.8f
        );

        if (_text != null)
        {
            _text.DOKill();
            _text.DOLocalMoveX(_textSlideX, _textSlideTime)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                    _text.DOLocalMoveX(0f, _textSlideTime)
                );
        }
    }
}
