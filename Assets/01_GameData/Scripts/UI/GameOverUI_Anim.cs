// ゲームオーバーUIのパネル・タイトル・ボタンを順にアニメーション表示するスクリプト
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class GameOverUI_Anim : MonoBehaviour
{
    [Header("Auto Find (by name)")]
    [SerializeField] private bool _autoFindOnAwake = true; // Awake時に子オブジェクトを自動検索するか

    [Header("ルート")]
    [SerializeField] private RectTransform _rootPanel;     // GameOverUIのルート
    [SerializeField] private CanvasGroup _rootGroup;       // ルートのCanvasGroup

    [Header("暗幕 / グロー")]
    [SerializeField] private CanvasGroup _dimGroup;        // 暗幕（CanvasGroup）
    [SerializeField] private CanvasGroup _screenGlowGroup; // スクリーングロー（CanvasGroup・任意）

    [Header("タイトル")]
    [SerializeField] private RectTransform _title;         // Text_Title
    [SerializeField] private RectTransform _titleGlow;     // TextTitle_Glow（任意）

    [Header("リワード / ボタン")]
    [SerializeField] private RectTransform _rewards;       // Text_Rewards（任意）
    [SerializeField] private RectTransform _buttons;       // Buttons

    [Header("Timing")]
    [SerializeField] private float _fadeIn = 0.18f;        // フェードイン時間
    [SerializeField] private float _panelPop = 0.22f;      // パネルポップアニメーション時間
    [SerializeField] private float _titleDelay = 0.05f;    // タイトル表示までの遅延
    [SerializeField] private float _buttonsDelay = 0.08f;  // ボタン表示までの遅延

    [Header("Scale")]
    [SerializeField] private float _panelPopScale = 1.08f;  // パネルのポップスケール
    [SerializeField] private float _titlePopScale = 1.08f;  // タイトルのポップスケール
    [SerializeField] private float _buttonsPopScale = 1.05f; // ボタンのポップスケール

    //[Header("ナビゲーション（任意；GameManagerが代わりにフォーカスを設定できる）")]
    //[SerializeField] private GameObject _firstSelectedOnOpen;

    private Sequence _seq; // 全体アニメーションのSequence

    private void Awake()
    {
        if (_autoFindOnAwake) AutoFind(); // 名前ベースで子オブジェクトを自動取得
        HardHideInstant();                // 起動時は非表示にする
    }

    [ContextMenu("AutoFind")]
    private void AutoFind()
    {
        if (_rootPanel == null) _rootPanel = transform as RectTransform;
        if (_rootGroup == null) _rootGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        _dimGroup = _dimGroup != null ? _dimGroup : FindCanvasGroup("Dimed");
        _screenGlowGroup = _screenGlowGroup != null ? _screenGlowGroup : FindCanvasGroup("ScreenGlow");

        _title = _title != null ? _title : FindRect("Text_Title");
        // グローオブジェクトの名前は "TextTitle_Glow"
        _titleGlow = _titleGlow != null ? _titleGlow : FindRect("TextTitle_Glow");

        _rewards = _rewards != null ? _rewards : FindRect("Text_Rewards");
        _buttons = _buttons != null ? _buttons : FindRect("Buttons");
    }

    private RectTransform FindRect(string path)
    {
        var t = transform.Find(path);
        return t ? t as RectTransform : null; // 見つからない場合はnullを返す
    }

    private CanvasGroup FindCanvasGroup(string path)
    {
        var t = transform.Find(path);
        if (!t) return null;
        return t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>(); // なければ追加して返す
    }

    // ─────────────────────────────────────────────────────

    public void Show()
    {
        _seq?.Kill(); // 既存のアニメーションを停止

        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // 最前面に表示

        // 選択状態をクリアして不正な状態を避ける
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); // 選択状態をクリア

        // 初期状態を設定する
        if (_rootGroup != null)
        {
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = true;
            _rootGroup.interactable = true;
        }

        if (_rootPanel != null) _rootPanel.localScale = Vector3.zero; // パネルをスケール0から開始

        if (_dimGroup != null) _dimGroup.alpha = 0f;
        if (_screenGlowGroup != null) _screenGlowGroup.alpha = 0f;

        if (_title != null) _title.localScale = Vector3.one;
        if (_titleGlow != null) _titleGlow.localScale = Vector3.one;

        if (_rewards != null)
        {
            _rewards.localScale = Vector3.one;
            _rewards.gameObject.SetActive(true);
        }

        if (_buttons != null)
        {
            _buttons.localScale = Vector3.one;
            _buttons.gameObject.SetActive(true);
        }

        _seq = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // 暗幕 + ルートフェード
        if (_dimGroup != null) _seq.Join(_dimGroup.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad));   // 暗幕フェードイン
        if (_rootGroup != null) _seq.Join(_rootGroup.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad)); // パネルフェードイン

        // スクリーングロー「パルス」演出
        if (_screenGlowGroup != null)
        {
            _seq.Join(_screenGlowGroup.DOFade(0.85f, 0.12f).SetEase(Ease.OutQuad)); // スクリーングローを強く表示
            _seq.Append(_screenGlowGroup.DOFade(0.35f, 0.22f).SetEase(Ease.OutQuad)); // 落ち着いた明るさへフェード
        }

        // panel pop
        if (_rootPanel != null) _seq.Join(_rootPanel.DOScale(_panelPopScale, _panelPop).SetEase(Ease.OutBack)); // パネルポップイン
        if (_rootPanel != null) _seq.Append(_rootPanel.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));              // 基準スケールへ収束

        // タイトルヒット（クリア演出よりやや「重め」）
        _seq.AppendInterval(_titleDelay); // タイトル表示前に少し待つ
        if (_title != null)
        {
            _seq.Append(_title.DOScale(_titlePopScale, 0.16f).SetEase(Ease.OutBack));
            _seq.Append(_title.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));
            _seq.Append(_title.DOPunchScale(new Vector3(0.06f, 0.06f, 0f), 0.18f, 8, 0.6f)); // 衝撃感を演出するパンチ
        }

        if (_titleGlow != null)
        {
            _seq.Join(_titleGlow.DOScale(1.03f, 0.16f).SetEase(Ease.OutQuad));
            _seq.Append(_titleGlow.DOScale(1f, 0.12f).SetEase(Ease.OutQuad));
        }

        // リワードのスライドアップ（任意）
        if (_rewards != null)
        {
            Vector2 end = _rewards.anchoredPosition;
            _rewards.anchoredPosition = end + new Vector2(0f, -16f); // 下からスライドインする開始位置
            _seq.Append(_rewards.DOAnchorPos(end, 0.18f).SetEase(Ease.OutCubic)); // 正しい位置へスライド
        }

        // ボタンポップ
        _seq.AppendInterval(_buttonsDelay); // ボタン表示前に少し待つ
        if (_buttons != null)
        {
            _buttons.localScale = Vector3.one * 0.96f; // わずかに縮小した状態から開始
            _seq.Append(_buttons.DOScale(_buttonsPopScale, 0.16f).SetEase(Ease.OutBack)); // ボタンポップイン
            _seq.Append(_buttons.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));               // 基準スケールへ収束
        }

        _seq.Play();
    }

    public void Hide()
    {
        _seq?.Kill();

        _seq = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        if (_screenGlowGroup != null) _seq.Join(_screenGlowGroup.DOFade(0f, 0.10f).SetEase(Ease.InQuad)); // スクリーングローフェードアウト
        if (_dimGroup != null) _seq.Join(_dimGroup.DOFade(0f, 0.14f).SetEase(Ease.InQuad));               // 暗幕フェードアウト
        if (_rootGroup != null) _seq.Join(_rootGroup.DOFade(0f, 0.14f).SetEase(Ease.InQuad));             // パネルフェードアウト
        if (_rootPanel != null) _seq.Join(_rootPanel.DOScale(0f, 0.18f).SetEase(Ease.InBack));             // パネルスケールアウト

        _seq.OnComplete(() =>
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); // 選択状態をクリア
            if (_rootGroup != null)
            {
                _rootGroup.blocksRaycasts = false;
                _rootGroup.interactable = false;
            }
            gameObject.SetActive(false); // 非表示完了後にオブジェクトを無効化
        });

        _seq.Play();
    }

    public void HardHideInstant()
    {
        _seq?.Kill(); // アニメーションを即座に停止

        if (_screenGlowGroup != null) _screenGlowGroup.alpha = 0f;
        if (_dimGroup != null) _dimGroup.alpha = 0f;

        if (_rootGroup != null)
        {
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;
        }

        if (_rootPanel != null) _rootPanel.localScale = Vector3.zero; // スケールを即座に0にする

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    [ContextMenu("デバッグ/表示")]
    private void DebugShow() => Show(); // エディタ上でのデバッグ表示

    [ContextMenu("デバッグ/非表示")]
    private void DebugHide() => Hide();
#endif
}
