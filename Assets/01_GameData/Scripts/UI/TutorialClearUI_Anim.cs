using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TutorialClearUI_Anim : MonoBehaviour
{
    [Header("Auto Find (by name)")]
    [SerializeField] private bool _autoFindOnAwake = true;

    [Header("Root")]
    [SerializeField] private RectTransform _rootPanel;     // TutorialClearUI (this) or a child panel
    [SerializeField] private CanvasGroup _rootGroup;       // CanvasGroup on root

    [Header("Dim")]
    [SerializeField] private CanvasGroup _dimGroup;        // Dimed (optional)

    [Header("Title")]
    [SerializeField] private RectTransform _title;         // Text_Title
    [SerializeField] private RectTransform _titleGlow;     // TextTitle_Glow (optional)

    [Header("SubTitle / TextBox / Buttons (optional)")]
    [SerializeField] private RectTransform _textBoxStage;  // TextBox Stage
    [SerializeField] private RectTransform _buttons;       // Buttons

    [Header("Stars")]
    [SerializeField] private Transform _starOffRoot;       // StarStage/Star_Off
    [SerializeField] private Transform _starOnRoot;        // StarStage/Star_On

    [Header("Toggle Check")]
    [SerializeField] private RectTransform _toggleOn;      // Toggle_Check/On (the check image)

    [Header("Timing")]
    [SerializeField] private float _fadeIn = 0.18f;
    [SerializeField] private float _panelPop = 0.22f;
    [SerializeField] private float _titleDelay = 0.05f;
    [SerializeField] private float _starStagger = 0.12f;

    [Header("Scale")]
    [SerializeField] private float _panelPopScale = 1.08f;
    [SerializeField] private float _titlePopScale = 1.06f;
    [SerializeField] private float _starPopScale = 1.25f;
    [SerializeField] private float _togglePopScale = 1.2f;
    
    [Header("Navigation")]
    [SerializeField] private GameObject _firstSelectedOnOpen;

    private RectTransform[] _starOffIcons;
    private RectTransform[] _starOnIcons;

    private Sequence _seq;

    private void Awake()
    {
        if (_autoFindOnAwake) AutoFind();

        EnsureArrays();
        HardHideInstant();
    }

    [ContextMenu("AutoFind")]
    private void AutoFind()
    {
        // Root
        if (_rootPanel == null) _rootPanel = transform as RectTransform;
        if (_rootGroup == null) _rootGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        // Named finds (safe even if missing)
        _dimGroup = _dimGroup != null ? _dimGroup : FindCanvasGroup("Dimed");

        _title = _title != null ? _title : FindRect("Text_Title");
        _titleGlow = _titleGlow != null ? _titleGlow : FindRect("TextTitle_Glow");

        _textBoxStage = _textBoxStage != null ? _textBoxStage : FindRect("TextBox Stage");
        _buttons = _buttons != null ? _buttons : FindRect("Buttons");

        if (_starOffRoot == null) _starOffRoot = FindTransform("StarStage/Star_Off");
        if (_starOnRoot == null) _starOnRoot = FindTransform("StarStage/Star_On");

        if (_toggleOn == null) _toggleOn = FindRect("Toggle_Check/On");

        EnsureArrays();
    }

    private RectTransform FindRect(string path)
    {
        var t = transform.Find(path);
        return t ? t as RectTransform : null;
    }

    private Transform FindTransform(string path)
    {
        return transform.Find(path);
    }

    private CanvasGroup FindCanvasGroup(string path)
    {
        var t = transform.Find(path);
        if (!t) return null;
        return t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>();
    }

    private void EnsureArrays()
    {
        if (_starOffRoot != null)
        {
            int n = _starOffRoot.childCount;
            _starOffIcons = new RectTransform[n];
            for (int i = 0; i < n; i++) _starOffIcons[i] = _starOffRoot.GetChild(i) as RectTransform;
        }

        if (_starOnRoot != null)
        {
            int n = _starOnRoot.childCount;
            _starOnIcons = new RectTransform[n];
            for (int i = 0; i < n; i++) _starOnIcons[i] = _starOnRoot.GetChild(i) as RectTransform;
        }
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    public void Show(int starsAchieved, bool toggleAchieved)
    {
        _seq?.Kill();
        EnsureArrays();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ResetSelectionState();
        EventSystem.current?.SetSelectedGameObject(null);

        // setup visible state (instant setup)
        SetStarsState(starsAchieved, instant: true);
        SetToggleState(toggleAchieved, instant: true);

        // panel start hidden
        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = true;
        _rootGroup.interactable = true;

        _rootPanel.localScale = Vector3.zero;

        if (_dimGroup != null) _dimGroup.alpha = 0f;

        if (_title != null) _title.localScale = Vector3.one;
        if (_titleGlow != null) _titleGlow.localScale = Vector3.one;

        if (_textBoxStage != null) _textBoxStage.localScale = Vector3.one;
        if (_buttons != null) _buttons.localScale = Vector3.one;

        _seq = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // dim fade
        if (_dimGroup != null) _seq.Join(_dimGroup.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad));

        // root fade + pop
        _seq.Join(_rootGroup.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad));
        _seq.Join(_rootPanel.DOScale(_panelPopScale, _panelPop).SetEase(Ease.OutBack));
        _seq.Append(_rootPanel.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));

        // title
        _seq.AppendInterval(_titleDelay);
        if (_title != null)
        {
            _seq.Append(_title.DOScale(_titlePopScale, 0.18f).SetEase(Ease.OutBack));
            _seq.Append(_title.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));
        }
        if (_titleGlow != null)
        {
            _seq.Join(_titleGlow.DOScale(1.03f, 0.18f).SetEase(Ease.OutQuad));
            _seq.Append(_titleGlow.DOScale(1f, 0.12f).SetEase(Ease.OutQuad));
        }

        // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
        // 1) Toggle FIRST
        _seq.AppendInterval(0.10f);
        if (toggleAchieved && _toggleOn != null)
        {
            _seq.AppendCallback(RevealToggle);
            _seq.Append(PlayToggleTween(_toggleOn));
        }

        // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
        // 2) Stars AFTER toggle
        int starSlots = Mathf.Min(_starOffIcons?.Length ?? 0, _starOnIcons?.Length ?? 0);
        int clamped = Mathf.Clamp(starsAchieved, 0, starSlots);

        for (int i = 0; i < clamped; i++)
        {
            int idx = i;
            _seq.AppendInterval(_starStagger);
            _seq.AppendCallback(() => RevealStar(idx));
            _seq.Append(PlayStarTween(_starOnIcons[idx]));
        }
        _seq.Play();
        
    }


    public void Hide()
    {
        _seq?.Kill();

        _seq = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        if (_dimGroup != null) _seq.Join(_dimGroup.DOFade(0f, 0.14f).SetEase(Ease.InQuad));
        _seq.Join(_rootGroup.DOFade(0f, 0.14f).SetEase(Ease.InQuad));
        _seq.Join(_rootPanel.DOScale(0f, 0.18f).SetEase(Ease.InBack));
        _seq.OnComplete(() =>
        {
            ResetSelectionState();
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;
            gameObject.SetActive(false);
        });

        _seq.Play();
    }

    public void HardHideInstant()
    {
        _seq?.Kill();

        if (_dimGroup != null) _dimGroup.alpha = 0f;

        if (_rootGroup != null)
        {
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;
        }

        if (_rootPanel != null) _rootPanel.localScale = Vector3.zero;

        SetStarsState(0, instant: true);
        SetToggleState(false, instant: true);
        ResetSelectionState();
        gameObject.SetActive(false);
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Stars / Toggle
    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    private void SetStarsState(int achieved, bool instant)
    {
        int n = Mathf.Min(_starOffIcons?.Length ?? 0, _starOnIcons?.Length ?? 0);
        achieved = Mathf.Clamp(achieved, 0, n);

        for (int i = 0; i < n; i++)
        {
            bool on = i < achieved;

            if (_starOffIcons[i] != null) _starOffIcons[i].gameObject.SetActive(!on);
            if (_starOnIcons[i] != null)
            {
                _starOnIcons[i].gameObject.SetActive(on);
                _starOnIcons[i].DOKill(true);

                if (on && instant) _starOnIcons[i].localScale = Vector3.zero;
                else _starOnIcons[i].localScale = Vector3.one;

                _starOnIcons[i].localRotation = Quaternion.identity;
            }
        }
    }

    private void RevealStar(int i)
    {
        if (_starOffIcons == null || _starOnIcons == null) return;
        if (i < 0 || i >= _starOnIcons.Length) return;

        if (_starOffIcons[i] != null) _starOffIcons[i].gameObject.SetActive(false);
        if (_starOnIcons[i] != null)
        {
            _starOnIcons[i].gameObject.SetActive(true);
            _starOnIcons[i].localScale = Vector3.zero;
            _starOnIcons[i].localRotation = Quaternion.identity;
        }
    }

    private Tween PlayStarTween(RectTransform starOn)
    {
        if (starOn == null) return DOTween.Sequence().SetUpdate(true);

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(starOn.DOScale(_starPopScale, 0.20f).SetEase(Ease.OutBack));
        seq.Join(starOn.DORotate(new Vector3(0, 0, -8f), 0.10f).SetEase(Ease.OutQuad));
        seq.Append(starOn.DORotate(Vector3.zero, 0.12f).SetEase(Ease.OutQuad));
        seq.Append(starOn.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));
        seq.Append(starOn.DOPunchScale(new Vector3(0.10f, 0.10f, 0f), 0.18f, 8, 0.6f));
        return seq;
    }

    private void SetToggleState(bool achieved, bool instant)
    {
        if (_toggleOn == null) return;

        _toggleOn.gameObject.SetActive(achieved);
        _toggleOn.DOKill(true);

        if (achieved && instant) _toggleOn.localScale = Vector3.zero;
        else _toggleOn.localScale = Vector3.one;

        _toggleOn.localRotation = Quaternion.identity;
    }

    private void RevealToggle()
    {
        if (_toggleOn == null) return;
        _toggleOn.gameObject.SetActive(true);
        _toggleOn.localScale = Vector3.zero;
        _toggleOn.localRotation = Quaternion.identity;
    }

    private Tween PlayToggleTween(RectTransform checkOn)
    {
        if (checkOn == null) return DOTween.Sequence().SetUpdate(true);

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(checkOn.DOScale(_togglePopScale, 0.18f).SetEase(Ease.OutBack));
        seq.Join(checkOn.DORotate(new Vector3(0, 0, 6f), 0.10f).SetEase(Ease.OutQuad));
        seq.Append(checkOn.DORotate(Vector3.zero, 0.10f).SetEase(Ease.OutQuad));
        seq.Append(checkOn.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));
        seq.Append(checkOn.DOPunchScale(new Vector3(0.12f, 0.12f, 0f), 0.16f, 8, 0.6f));
        return seq;
    }

    private void ResetSelectionState()
    {
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // Optional: reset your hover/select visuals if you use SelectSwap (same as PauseMenu)
        var swaps = GetComponentsInChildren<SelectSwap>(true);
        foreach (var s in swaps)
        {
            s.ForceUnselectImmediate();
        }
    }


#if UNITY_EDITOR
    [ContextMenu("DEBUG Show: 3 stars + toggle")]
    private void DebugShowAll() => Show(3, true);

    [ContextMenu("DEBUG Show: 1 star")]
    private void DebugShowOne() => Show(1, false);

    [ContextMenu("DEBUG Hide")]
    private void DebugHide() => Hide();
#endif
}
