// ポーズメニューの表示・非表示アニメーションとボタンイベントを管理するスクリプト
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject _contentRoot;         // メニュー全体のルートオブジェクト
    [SerializeField] CanvasGroup _panelGroup;         // フェード制御用CanvasGroup
    [SerializeField] RectTransform _panelRect;        // スケールアニメーション用RectTransform
    [SerializeField] float _fadeTime = 0.22f;         // フェードアニメーションの時間
    [SerializeField] float _scaleTime = 0.22f;        // スケールアニメーションの時間
    [SerializeField] GameObject _firstSelectedOnOpen; // メニュー表示時に最初にフォーカスするボタン

    private Sequence _showSeq, _hideSeq; // 表示・非表示アニメーションのSequence

    void Awake()
    {
        if (_contentRoot) _contentRoot.SetActive(false); // 起動時はメニューを非表示にする
        if (_panelGroup)
        {
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }
        if (_panelRect) _panelRect.localScale = Vector3.zero; // パネルをスケール0で初期化
    }

    // ──────────────────────────────────────────────────────────────────
    // GAMEMANAGERから呼び出される
    public void ShowMenu()
    {
        _hideSeq?.Kill(); // 非表示アニメーション中なら停止
        _showSeq?.Kill();

        if (_contentRoot)
        {
            _contentRoot.SetActive(true);
            _contentRoot.transform.SetAsLastSibling(); // 最前面に表示
        }

        if (_panelGroup)
        {
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }
        if (_panelRect) _panelRect.localScale = Vector3.zero;

        _showSeq = DOTween.Sequence()
            .SetUpdate(true) // 非スケール時間（ポーズ中も動作する）
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .Join(_panelGroup.DOFade(1f, _fadeTime).SetEase(Ease.OutQuad))         // フェードイン
            .Join(_panelRect.DOScale(1.1f, _scaleTime).SetEase(Ease.OutBack))      // スケールポップイン
            .OnComplete(() =>
            {
                if (_panelGroup)
                {
                    _panelGroup.interactable = true;      // アニメーション完了後にインタラクション有効化
                    _panelGroup.blocksRaycasts = true;
                }
            });

        _showSeq.Play();
        FocusNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget(); // 次フレームにフォーカスを設定
    }

    public void HideMenu()
    {
        _showSeq?.Kill();
        _hideSeq?.Kill();

        if (_panelGroup)
        {
            _panelGroup.interactable = false;   // 非表示中はインタラクション無効
            _panelGroup.blocksRaycasts = false;
        }

        _hideSeq = DOTween.Sequence()
            .SetUpdate(true) // 非スケール時間
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .Join(_panelGroup.DOFade(0f, _fadeTime).SetEase(Ease.InQuad))     // フェードアウト
            .Join(_panelRect.DOScale(0f, _scaleTime).SetEase(Ease.InBack))    // スケールアウト
            .OnComplete(() =>
            {
                ResetSelectionState(); // 選択状態をリセット
                if (_contentRoot) _contentRoot.SetActive(false); // 非表示完了後にオブジェクトを無効化
            });
        _hideSeq.Play();
    }

    // シーン切り替え・タイトルへの遷移時に即座に非表示にする
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
            _panelRect.localScale = Vector3.zero; // アニメーションなしで即座にスケールをゼロにする

        ResetSelectionState();

        if (_contentRoot)
            _contentRoot.SetActive(false);
    }


    // ──────────────────────────────────────────────────────────────────
    // ヘルパー
    private async UniTaskVoid FocusNextFrameAsync(CancellationToken ct)
    {
        await UniTask.NextFrame(ct); // 次フレームまで待機（UIの初期化が完了してから選択する）
        if (_firstSelectedOnOpen)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            EventSystem.current?.SetSelectedGameObject(_firstSelectedOnOpen); // 最初のボタンにフォーカスを当てる
        }
    }


    private void ResetSelectionState()
    {
        // 選択オブジェクトをクリアして、再表示時に誤ったボタンにフォーカスしないようにする
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null); // 選択中のボタンをクリア
        }

        if (_contentRoot == null) return;

        // このメニュー配下の全SelectSwapをリセットする
        var swaps = _contentRoot.GetComponentsInChildren<SelectSwap>(true);
        foreach (var s in swaps)
        {
            s.ForceUnselectImmediate(); // 子要素のSelectSwapを全て非選択状態に戻す
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // ボタン
    public void OnClick_Resume() => GameManager.Instance?.ResumeGame(); // 再開ボタン
    public void OnClick_RestartLevel() { GameManager.Instance?.ResumeGame(); GameManager.Instance?.RestartLevel(); } // リスタートボタン
    public void OnClick_QuitToTitle()
    {
        GameManager.Instance?.LoadTitle(); // タイトルへ戻るボタン
    }
    public void OnClick_ResetTutorial() { GameManager.Instance?.ResetTutorial(); } // チュートリアルリセットボタン
}
