// チュートリアルクリアUIのパネル・スター・チェックアイコンをアニメーション表示するスクリプト
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TutorialClearUI_Anim : MonoBehaviour
{
    [Header("Auto Find (by name)")]
    [SerializeField] private bool _autoFindOnAwake = true; // Awake時に子オブジェクトを自動検索するか

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
    [SerializeField] private float _fadeIn = 0.18f;        // フェードイン時間
    [SerializeField] private float _panelPop = 0.22f;      // パネルポップアニメーション時間
    [SerializeField] private float _titleDelay = 0.05f;    // タイトル表示までの遅延
    [SerializeField] private float _starStagger = 0.12f;   // スターアイコン間の時間差

    [Header("Scale")]
    [SerializeField] private float _panelPopScale = 1.08f;  // パネルのポップスケール
    [SerializeField] private float _titlePopScale = 1.06f;  // タイトルのポップスケール
    [SerializeField] private float _starPopScale = 1.25f;   // スターのポップスケール
    [SerializeField] private float _togglePopScale = 1.2f;  // チェックアイコンのポップスケール

    [Header("Navigation")]
    [SerializeField] private GameObject _firstSelectedOnOpen; // 表示時に最初にフォーカスするオブジェクト

    private RectTransform[] _starOffIcons; // Star_Off配下の各スターアイコン
    private RectTransform[] _starOnIcons;  // Star_On配下の各スターアイコン

    private Sequence _seq; // 全体アニメーションのSequence

    private void Awake()
    {
        if (_autoFindOnAwake) AutoFind(); // 名前ベースで子オブジェクトを自動取得

        EnsureArrays(); // スターアイコン配列を初期化
        HardHideInstant(); // 起動時は非表示にする
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

        if (_starOffRoot == null) _starOffRoot = FindTransform("StarStage/Star_Off"); // OFFスターのルートを取得
        if (_starOnRoot == null) _starOnRoot = FindTransform("StarStage/Star_On");    // ONスターのルートを取得

        if (_toggleOn == null) _toggleOn = FindRect("Toggle_Check/On"); // チェックのON画像を取得

        EnsureArrays();
    }

    private RectTransform FindRect(string path)
    {
        var t = transform.Find(path);
        return t ? t as RectTransform : null;
    }

    private Transform FindTransform(string path)
    {
        return transform.Find(path); // パスで子Transformを検索
    }

    private CanvasGroup FindCanvasGroup(string path)
    {
        var t = transform.Find(path);
        if (!t) return null;
        return t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>(); // なければ追加
    }

    private void EnsureArrays()
    {
        if (_starOffRoot != null)
        {
            int n = _starOffRoot.childCount;
            _starOffIcons = new RectTransform[n];
            for (int i = 0; i < n; i++) _starOffIcons[i] = _starOffRoot.GetChild(i) as RectTransform; // OFFスターを配列化
        }

        if (_starOnRoot != null)
        {
            int n = _starOnRoot.childCount;
            _starOnIcons = new RectTransform[n];
            for (int i = 0; i < n; i++) _starOnIcons[i] = _starOnRoot.GetChild(i) as RectTransform; // ONスターを配列化
        }
    }

    // ─────────────────────────────────────────────────────────

    public void Show(int starsAchieved, bool toggleAchieved)
    {
        _seq?.Kill(); // 既存のアニメーションを停止
        EnsureArrays();

        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // 最前面に表示
        ResetSelectionState();
        EventSystem.current?.SetSelectedGameObject(null); // フォーカスをクリア

        // setup visible state (instant setup)
        SetStarsState(starsAchieved, instant: true);  // スター表示状態を即座に設定
        SetToggleState(toggleAchieved, instant: true); // チェック状態を即座に設定

        // panel start hidden
        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = true;
        _rootGroup.interactable = true;

        _rootPanel.localScale = Vector3.zero; // パネルをスケール0から開始

        if (_dimGroup != null) _dimGroup.alpha = 0f;

        if (_title != null) _title.localScale = Vector3.one;
        if (_titleGlow != null) _titleGlow.localScale = Vector3.one;

        if (_textBoxStage != null) _textBoxStage.localScale = Vector3.one;
        if (_buttons != null) _buttons.localScale = Vector3.one;

        _seq = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // dim fade
        if (_dimGroup != null) _seq.Join(_dimGroup.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad)); // 暗幕フェードイン

        // root fade + pop
        _seq.Join(_rootGroup.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad));                          // ルートフェードイン
        _seq.Join(_rootPanel.DOScale(_panelPopScale, _panelPop).SetEase(Ease.OutBack));            // パネルポップイン
        _seq.Append(_rootPanel.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));                         // 基準スケールへ収束

        // title
        _seq.AppendInterval(_titleDelay); // タイトル表示前に少し待つ
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

        // ────────────────────────────────────
        // 1) Toggle FIRST
        _seq.AppendInterval(0.10f); // チェック表示前に少し待つ
        if (toggleAchieved && _toggleOn != null)
        {
            _seq.AppendCallback(RevealToggle);          // チェックを表示状態にリセット
            _seq.Append(PlayToggleTween(_toggleOn));    // チェックのポップアニメーション
        }

        // ────────────────────────────────────
        // 2) Stars AFTER toggle
        int starSlots = Mathf.Min(_starOffIcons?.Length ?? 0, _starOnIcons?.Length ?? 0);
        int clamped = Mathf.Clamp(starsAchieved, 0, starSlots); // スター数を有効範囲にクランプ

        for (int i = 0; i < clamped; i++)
        {
            int idx = i;
            _seq.AppendInterval(_starStagger);                    // スター間に時間差を入れる
            _seq.AppendCallback(() => RevealStar(idx));           // スターを表示状態にリセット
            _seq.Append(PlayStarTween(_starOnIcons[idx]));        // スターのポップアニメーション
        }
        _seq.Play();

    }


    public void Hide()
    {
        _seq?.Kill();

        _seq = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        if (_dimGroup != null) _seq.Join(_dimGroup.DOFade(0f, 0.14f).SetEase(Ease.InQuad));    // 暗幕フェードアウト
        _seq.Join(_rootGroup.DOFade(0f, 0.14f).SetEase(Ease.InQuad));                          // パネルフェードアウト
        _seq.Join(_rootPanel.DOScale(0f, 0.18f).SetEase(Ease.InBack));                         // パネルスケールアウト
        _seq.OnComplete(() =>
        {
            ResetSelectionState(); // 非表示完了後に選択状態をリセット
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;
            gameObject.SetActive(false);
        });

        _seq.Play();
    }

    public void HardHideInstant()
    {
        _seq?.Kill(); // アニメーションを即座に停止

        if (_dimGroup != null) _dimGroup.alpha = 0f;

        if (_rootGroup != null)
        {
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;
        }

        if (_rootPanel != null) _rootPanel.localScale = Vector3.zero;

        SetStarsState(0, instant: true);        // スターを全て非表示にリセット
        SetToggleState(false, instant: true);   // チェックを非表示にリセット
        ResetSelectionState();
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────
    // Stars / Toggle
    // ─────────────────────────────────────────────────────────

    private void SetStarsState(int achieved, bool instant)
    {
        int n = Mathf.Min(_starOffIcons?.Length ?? 0, _starOnIcons?.Length ?? 0);
        achieved = Mathf.Clamp(achieved, 0, n);

        for (int i = 0; i < n; i++)
        {
            bool on = i < achieved; // 達成スター数以内であればON状態

            if (_starOffIcons[i] != null) _starOffIcons[i].gameObject.SetActive(!on); // OFFスターはON時非表示
            if (_starOnIcons[i] != null)
            {
                _starOnIcons[i].gameObject.SetActive(on);
                _starOnIcons[i].DOKill(true);

                if (on && instant) _starOnIcons[i].localScale = Vector3.zero; // 即座に設定する場合はスケール0にしてポップ待機
                else _starOnIcons[i].localScale = Vector3.one;

                _starOnIcons[i].localRotation = Quaternion.identity;
            }
        }
    }

    private void RevealStar(int i)
    {
        if (_starOffIcons == null || _starOnIcons == null) return;
        if (i < 0 || i >= _starOnIcons.Length) return; // 範囲外チェック

        if (_starOffIcons[i] != null) _starOffIcons[i].gameObject.SetActive(false); // OFFスターを非表示
        if (_starOnIcons[i] != null)
        {
            _starOnIcons[i].gameObject.SetActive(true);
            _starOnIcons[i].localScale = Vector3.zero;  // スケール0から開始（ポップアニメーション準備）
            _starOnIcons[i].localRotation = Quaternion.identity;
        }
    }

    private Tween PlayStarTween(RectTransform starOn)
    {
        if (starOn == null) return DOTween.Sequence().SetUpdate(true);

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(starOn.DOScale(_starPopScale, 0.20f).SetEase(Ease.OutBack));             // ポップスケールアップ
        seq.Join(starOn.DORotate(new Vector3(0, 0, -8f), 0.10f).SetEase(Ease.OutQuad));    // 少し傾ける
        seq.Append(starOn.DORotate(Vector3.zero, 0.12f).SetEase(Ease.OutQuad));            // 元の向きへ戻す
        seq.Append(starOn.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));                       // 基準スケールへ収束
        seq.Append(starOn.DOPunchScale(new Vector3(0.10f, 0.10f, 0f), 0.18f, 8, 0.6f));   // 微小パンチで締める
        return seq;
    }

    private void SetToggleState(bool achieved, bool instant)
    {
        if (_toggleOn == null) return;

        _toggleOn.gameObject.SetActive(achieved); // 達成時のみ表示
        _toggleOn.DOKill(true);

        if (achieved && instant) _toggleOn.localScale = Vector3.zero; // 即座に設定する場合はスケール0にしてポップ待機
        else _toggleOn.localScale = Vector3.one;

        _toggleOn.localRotation = Quaternion.identity;
    }

    private void RevealToggle()
    {
        if (_toggleOn == null) return;
        _toggleOn.gameObject.SetActive(true);
        _toggleOn.localScale = Vector3.zero;  // スケール0から開始（ポップアニメーション準備）
        _toggleOn.localRotation = Quaternion.identity;
    }

    private Tween PlayToggleTween(RectTransform checkOn)
    {
        if (checkOn == null) return DOTween.Sequence().SetUpdate(true);

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(checkOn.DOScale(_togglePopScale, 0.18f).SetEase(Ease.OutBack));         // ポップスケールアップ
        seq.Join(checkOn.DORotate(new Vector3(0, 0, 6f), 0.10f).SetEase(Ease.OutQuad));   // 少し傾ける
        seq.Append(checkOn.DORotate(Vector3.zero, 0.10f).SetEase(Ease.OutQuad));          // 元の向きへ戻す
        seq.Append(checkOn.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));                     // 基準スケールへ収束
        seq.Append(checkOn.DOPunchScale(new Vector3(0.12f, 0.12f, 0f), 0.16f, 8, 0.6f)); // 微小パンチで締める
        return seq;
    }

    private void ResetSelectionState()
    {
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null); // 選択中のオブジェクトをクリア
        }

        // Optional: reset your hover/select visuals if you use SelectSwap (same as PauseMenu)
        var swaps = GetComponentsInChildren<SelectSwap>(true);
        foreach (var s in swaps)
        {
            s.ForceUnselectImmediate(); // 子要素のSelectSwapを全て非選択状態に戻す
        }
    }


#if UNITY_EDITOR
    [ContextMenu("DEBUG Show: 3 stars + toggle")]
    private void DebugShowAll() => Show(3, true); // スター3つ・チェックあり でデバッグ表示

    [ContextMenu("DEBUG Show: 1 star")]
    private void DebugShowOne() => Show(1, false); // スター1つ・チェックなし でデバッグ表示

    [ContextMenu("DEBUG Hide")]
    private void DebugHide() => Hide();
#endif
}
