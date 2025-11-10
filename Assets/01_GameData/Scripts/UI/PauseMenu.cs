using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject _contentRoot;
    [SerializeField] CanvasGroup _panelGroup;
    [SerializeField] RectTransform _panelRect;
    [SerializeField] float _fadeTime = 0.22f;
    [SerializeField] float _scaleTime = 0.22f;
    [SerializeField] GameObject _firstSelectedOnOpen;

    private Sequence _showSeq, _hideSeq;

    void Awake()
    {
        if (_contentRoot) _contentRoot.SetActive(false);
        if (_panelGroup)
        {
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }
        if (_panelRect) _panelRect.localScale = Vector3.zero;
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // CALLED BY GAMEMANAGER
    public void ShowMenu()
    {
        _hideSeq?.Kill();
        _showSeq?.Kill();

        if (_contentRoot)
        {
            _contentRoot.SetActive(true);
            _contentRoot.transform.SetAsLastSibling();
        }

        if (_panelGroup)
        {
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }
        if (_panelRect) _panelRect.localScale = Vector3.zero;

        _showSeq = DOTween.Sequence()
            .SetUpdate(true) // unscaled time (works while paused)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .Join(_panelGroup.DOFade(1f, _fadeTime).SetEase(Ease.OutQuad))
            .Join(_panelRect.DOScale(1.1f, _scaleTime).SetEase(Ease.OutBack))
            .OnComplete(() =>
            {
                if (_panelGroup)
                {
                    _panelGroup.interactable = true;
                    _panelGroup.blocksRaycasts = true;
                }
            });

        _showSeq.Play();
        FocusNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    public void HideMenu()
    {
        _showSeq?.Kill();
        _hideSeq?.Kill();

        if (_panelGroup)
        {
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }

        _hideSeq = DOTween.Sequence()
            .SetUpdate(true) // unscaled time
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .Join(_panelGroup.DOFade(0f, _fadeTime).SetEase(Ease.InQuad))
            .Join(_panelRect.DOScale(0f, _scaleTime).SetEase(Ease.InBack))
            .OnComplete(() =>
            {
                ResetSelectionState();
                if (_contentRoot) _contentRoot.SetActive(false);
            });
        _hideSeq.Play();
    }

    // NEW: instant hard hide for scene changes / title
    public void HideMenuInstant()
    {
        _showSeq?.Kill();
        _hideSeq?.Kill();

        if (_panelGroup)
        { 
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }

        if (_panelRect)
            _panelRect.localScale = Vector3.zero;

        ResetSelectionState();

        if (_contentRoot)
            _contentRoot.SetActive(false);
    }


    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Helpers
    private async UniTaskVoid FocusNextFrameAsync(CancellationToken ct)
    {
        await UniTask.NextFrame(ct);
        if (_firstSelectedOnOpen)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            EventSystem.current?.SetSelectedGameObject(_firstSelectedOnOpen);
        }
    }


    private void ResetSelectionState()
    {
        // Clear selected object so we don't reopen focused on wrong button
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (_contentRoot == null) return;

        // Reset all SelectSwap under this menu
        var swaps = _contentRoot.GetComponentsInChildren<SelectSwap>(true);
        foreach (var s in swaps)
        {
            s.ForceUnselectImmediate();
        }
    }

    // „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ
    // Buttons
    public void OnClick_Resume() => GameManager.Instance?.ResumeGame();
    public void OnClick_RestartLevel() { GameManager.Instance?.ResumeGame(); GameManager.Instance?.RestartLevel(); }
    public void OnClick_QuitToTitle()
    {
        GameManager.Instance?.LoadTitle();
    }
    public void OnClick_ResetTutorial() { GameManager.Instance?.ResetTutorial(); }
}
