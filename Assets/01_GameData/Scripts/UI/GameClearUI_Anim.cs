// ゲームクリアUIのパネル・タイトル・スター・チェックアイコンを順にアニメーション表示するスクリプト
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class GameClearUI_Anim : MonoBehaviour
{
    [Header("Auto Find (by name)")]
    [SerializeField] private bool _autoFindOnAwake = true; // Awake時に子オブジェクトを自動検索するか

    [Header("Root")]
    [SerializeField] private RectTransform _rootPanel;     // GameClearUI root
    [SerializeField] private CanvasGroup _rootGroup;       // CanvasGroup on root

    [Header("Dim")]
    [SerializeField] private CanvasGroup _dimGroup;        // Dimed (optional)

    [Header("Title")]
    [SerializeField] private RectTransform _title;         // Text_Title
    [SerializeField] private RectTransform _titleGlow;     // TextTitle_Glow (optional)

    [Header("Stars (Off/On pairs optional)")]
    [SerializeField] private Transform _starOffRoot;       // StarStage/Star_Off (if exists)
    [SerializeField] private Transform _starOnRoot;        // StarStage/Star_On  (if exists)
    [SerializeField] private RectTransform[] _starIcons;   // fallback: StarStage children icons (if no off/on)

    [Header("Checks (Toggle_Check*/On)")]
    [SerializeField] private Transform _textBoxStage;      // TextBox Stage
    [SerializeField] private RectTransform[] _checkOnIcons; // filled by AutoFind: Toggle_Check*/On

    [Header("Buttons (optional anim)")]
    [SerializeField] private RectTransform _buttons;       // Buttons

    [Header("Timing")]
    [SerializeField] private float _fadeIn = 0.18f;          // フェードイン時間
    [SerializeField] private float _panelPop = 0.22f;        // パネルポップアニメーション時間
    [SerializeField] private float _titleDelay = 0.05f;      // タイトル表示までの遅延
    [SerializeField] private float _checkStagger = 0.10f;    // チェックアイコン間の時間差
    [SerializeField] private float _starStagger = 0.12f;     // スターアイコン間の時間差

    [Header("Scale")]
    [SerializeField] private float _panelPopScale = 1.08f;   // パネルのポップスケール
    [SerializeField] private float _titlePopScale = 1.06f;   // タイトルのポップスケール
    [SerializeField] private float _checkPopScale = 1.18f;   // チェックのポップスケール
    [SerializeField] private float _starPopScale = 1.25f;    // スターのポップスケール

    [Header("Buttons Pop")]
    [SerializeField] private bool _animateButtons = true;     // ボタンをアニメーションするか
    [SerializeField] private float _buttonsPopScale = 1.04f;  // ボタンのポップスケール

    private RectTransform[] _starOffIcons; // Star_Off配下の各スターアイコン
    private RectTransform[] _starOnIcons;  // Star_On配下の各スターアイコン

    private Sequence _seq; // 全体アニメーションのSequence

    private void Awake()
    {
        if (_autoFindOnAwake) AutoFind(); // 名前ベースで子オブジェクトを自動取得
        EnsureStarArrays();               // スターアイコン配列を初期化
        HardHideInstant();                // 起動時は非表示にする
    }

    [ContextMenu("AutoFind")]
    private void AutoFind()
    {
        if (_rootPanel == null) _rootPanel = transform as RectTransform;
        if (_rootGroup == null) _rootGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        _dimGroup = _dimGroup != null ? _dimGroup : FindCanvasGroup("Dimed");

        _title = _title != null ? _title : FindRect("Text_Title");
        _titleGlow = _titleGlow != null ? _titleGlow : FindRect("TextTitle_Glow");

        var starStage = transform.Find("StarStage");
        if (starStage != null)
        {
            if (_starOffRoot == null) _starOffRoot = starStage.Find("Star_Off");
            if (_starOnRoot == null) _starOnRoot = starStage.Find("Star_On");

            // fallback (if you don't have Star_Off/Star_On)
            if ((_starOffRoot == null || _starOnRoot == null) && _starIcons == null)
            {
                var list = new List<RectTransform>();
                for (int i = 0; i < starStage.childCount; i++)
                {
                    var rt = starStage.GetChild(i) as RectTransform;
                    if (rt != null) list.Add(rt);
                }
                _starIcons = list.ToArray(); // StarStageの子を全てフォールバック用に収集
            }
        }

        if (_textBoxStage == null) _textBoxStage = transform.Find("TextBox Stage");
        if (_buttons == null) _buttons = FindRect("Buttons");

        // Collect Toggle_Check*/On icons in order
        if (_textBoxStage != null)
        {
            var checkOnList = new List<RectTransform>();

            // You have: Toggle_Check, Toggle_Check (1), Toggle_Check (2)
            for (int i = 0; i < _textBoxStage.childCount; i++)
            {
                var child = _textBoxStage.GetChild(i);
                if (!child.name.StartsWith("Toggle_Check")) continue; // Toggle_Checkで始まる子のみ対象

                var on = child.Find("On") as RectTransform;
                if (on != null) checkOnList.Add(on); // On子オブジェクトのRectTransformを収集
            }

            _checkOnIcons = checkOnList.ToArray();
        }
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

    private void EnsureStarArrays()
    {
        if (_starOffRoot != null && _starOnRoot != null)
        {
            int n = Mathf.Min(_starOffRoot.childCount, _starOnRoot.childCount); // 少ない方の数に合わせる

            _starOffIcons = new RectTransform[n];
            _starOnIcons = new RectTransform[n];

            for (int i = 0; i < n; i++)
            {
                _starOffIcons[i] = _starOffRoot.GetChild(i) as RectTransform; // OFFスターアイコンを取得
                _starOnIcons[i] = _starOnRoot.GetChild(i) as RectTransform;   // ONスターアイコンを取得
            }
        }
    }

    // ─────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────

    /// <param name="starsAchieved">0..3</param>
    /// <param name="checksAchieved">array length 0..3 (ReachedGoal, 120s, 90s)</param>
    public void Show(int starsAchieved, bool[] checksAchieved)
    {
        _seq?.Kill(); // 既存のアニメーションを停止
        EnsureStarArrays();

        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // 最前面に表示

        // Setup initial visual state
        if (_rootGroup != null)
        {
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = true;
            _rootGroup.interactable = true;
        }
        if (_rootPanel != null) _rootPanel.localScale = Vector3.zero; // パネルをスケール0から開始
        if (_dimGroup != null) _dimGroup.alpha = 0f;

        if (_title != null) _title.localScale = Vector3.one;
        if (_titleGlow != null) _titleGlow.localScale = Vector3.one;

        // checks: show/hide ON icons immediately, but if achieved we start them at scale 0 for pop
        SetupChecks(checksAchieved); // チェックアイコンの初期状態を設定

        // stars: setup state
        SetupStars(starsAchieved); // スターアイコンの初期状態を設定

        // buttons start slightly small (optional)
        if (_buttons != null && _animateButtons)
            _buttons.localScale = Vector3.one * 0.96f; // ボタンをわずかに縮小した状態から開始

        // Build sequence
        _seq = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // dim + root fade
        if (_dimGroup != null) _seq.Join(_dimGroup.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad));  // 暗幕フェードイン
        if (_rootGroup != null) _seq.Join(_rootGroup.DOFade(1f, _fadeIn).SetEase(Ease.OutQuad)); // パネルフェードイン

        // panel pop
        if (_rootPanel != null) _seq.Join(_rootPanel.DOScale(_panelPopScale, _panelPop).SetEase(Ease.OutBack)); // パネルをポップインさせる
        if (_rootPanel != null) _seq.Append(_rootPanel.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));              // 基準スケールへ収束

        // title pop
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
        // 1) Toggle checks FIRST (top→bottom)
        if (_checkOnIcons != null && _checkOnIcons.Length > 0)
        {
            for (int i = 0; i < _checkOnIcons.Length; i++)
            {
                bool achieved = (checksAchieved != null && i < checksAchieved.Length) ? checksAchieved[i] : false;
                if (!achieved) continue; // 達成していないチェックはアニメーションをスキップ

                RectTransform check = _checkOnIcons[i];
                if (check == null) continue;

                _seq.AppendInterval(_checkStagger);       // チェック間に時間差を入れる
                _seq.Append(PlayCheckTween(check));        // チェックアイコンのポップアニメーション
            }
        }

        // ────────────────────────────────────
        // 2) Stars AFTER checks (left→right)
        AppendStarTweens(starsAchieved); // スターアニメーションをSequenceに追加

        // Buttons pop
        if (_buttons != null && _animateButtons)
        {
            _seq.AppendInterval(0.08f);
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

        if (_dimGroup != null) _seq.Join(_dimGroup.DOFade(0f, 0.14f).SetEase(Ease.InQuad));    // 暗幕フェードアウト
        if (_rootGroup != null) _seq.Join(_rootGroup.DOFade(0f, 0.14f).SetEase(Ease.InQuad));  // パネルフェードアウト
        if (_rootPanel != null) _seq.Join(_rootPanel.DOScale(0f, 0.18f).SetEase(Ease.InBack));  // パネルスケールアウト

        _seq.OnComplete(() =>
        {
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
        _seq?.Kill(); // 実行中のアニメーションを即座に停止

        if (_dimGroup != null) _dimGroup.alpha = 0f;

        if (_rootGroup != null)
        {
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;
        }

        if (_rootPanel != null) _rootPanel.localScale = Vector3.zero;

        // checks off
        if (_checkOnIcons != null)
        {
            foreach (var c in _checkOnIcons)
            {
                if (!c) continue;
                c.DOKill(true);             // 実行中のTweenを完了扱いで停止
                c.gameObject.SetActive(false);
                c.localScale = Vector3.one;
                c.localRotation = Quaternion.identity;
            }
        }

        // stars reset
        SetupStars(0); // スター0個の状態にリセット

        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────
    // Internals
    // ─────────────────────────────────────────────────────

    private void SetupChecks(bool[] checksAchieved)
    {
        if (_checkOnIcons == null) return;

        for (int i = 0; i < _checkOnIcons.Length; i++)
        {
            var check = _checkOnIcons[i];
            if (!check) continue;

            bool achieved = (checksAchieved != null && i < checksAchieved.Length) ? checksAchieved[i] : false;

            check.DOKill(true);
            check.gameObject.SetActive(achieved);     // 達成したチェックのみ表示
            check.localRotation = Quaternion.identity;

            // achieved checks start from 0 for pop
            check.localScale = achieved ? Vector3.zero : Vector3.one; // 達成チェックはスケール0からポップイン
        }
    }

    private Tween PlayCheckTween(RectTransform checkOn)
    {
        var seq = DOTween.Sequence().SetUpdate(true);
        checkOn.localScale = Vector3.zero;
        checkOn.localRotation = Quaternion.identity;

        seq.Append(checkOn.DOScale(_checkPopScale, 0.16f).SetEase(Ease.OutBack));              // ポップスケールアップ
        seq.Join(checkOn.DORotate(new Vector3(0, 0, 6f), 0.08f).SetEase(Ease.OutQuad));       // 少し傾けて弾力感を演出
        seq.Append(checkOn.DORotate(Vector3.zero, 0.10f).SetEase(Ease.OutQuad));              // 元の向きへ戻す
        seq.Append(checkOn.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));                         // 基準スケールへ収束
        seq.Append(checkOn.DOPunchScale(new Vector3(0.10f, 0.10f, 0f), 0.16f, 8, 0.6f));     // 微小パンチで締める

        return seq;
    }

    private void SetupStars(int starsAchieved)
    {
        // Case A: Off/On pairs exist (preferred)
        if (_starOffIcons != null && _starOnIcons != null && _starOffIcons.Length > 0)
        {
            int n = Mathf.Min(_starOffIcons.Length, _starOnIcons.Length);
            int clamped = Mathf.Clamp(starsAchieved, 0, n);

            for (int i = 0; i < n; i++)
            {
                bool on = i < clamped; // 達成スター数以内であればON状態

                if (_starOffIcons[i] != null) _starOffIcons[i].gameObject.SetActive(!on); // OFFスターはON時非表示
                if (_starOnIcons[i] != null)
                {
                    _starOnIcons[i].gameObject.SetActive(on);
                    _starOnIcons[i].DOKill(true);
                    _starOnIcons[i].localRotation = Quaternion.identity;
                    _starOnIcons[i].localScale = on ? Vector3.zero : Vector3.one; // earned stars start hidden
                }
            }
            return;
        }

        // Case B: Single icon per star (fallback) -> we just pop the first N stars
        if (_starIcons != null && _starIcons.Length > 0)
        {
            int n = _starIcons.Length;
            int clamped = Mathf.Clamp(starsAchieved, 0, n);

            for (int i = 0; i < n; i++)
            {
                var s = _starIcons[i];
                if (!s) continue;
                s.DOKill(true);
                s.localRotation = Quaternion.identity;
                s.localScale = (i < clamped) ? Vector3.zero : Vector3.one; // 達成スターはスケール0から開始
            }
        }
    }

    private void AppendStarTweens(int starsAchieved)
    {
        // Off/On style
        if (_starOnIcons != null && _starOnIcons.Length > 0)
        {
            int n = _starOnIcons.Length;
            int clamped = Mathf.Clamp(starsAchieved, 0, n);

            for (int i = 0; i < clamped; i++)
            {
                int idx = i;
                _seq.AppendInterval(_starStagger);              // スター間に時間差を入れる
                _seq.Append(PlayStarTween(_starOnIcons[idx]));  // 各スターのポップアニメーション
            }
            return;
        }

        // Fallback style
        if (_starIcons != null && _starIcons.Length > 0)
        {
            int n = _starIcons.Length;
            int clamped = Mathf.Clamp(starsAchieved, 0, n);

            for (int i = 0; i < clamped; i++)
            {
                int idx = i;
                _seq.AppendInterval(_starStagger);
                _seq.Append(PlayStarTween(_starIcons[idx])); // フォールバック用スターアニメーション
            }
        }
    }

    private Tween PlayStarTween(RectTransform star)
    {
        var seq = DOTween.Sequence().SetUpdate(true);
        if (!star) return seq;

        star.localScale = Vector3.zero;
        star.localRotation = Quaternion.identity;

        seq.Append(star.DOScale(_starPopScale, 0.20f).SetEase(Ease.OutBack));           // スターをポップスケールアップ
        seq.Join(star.DORotate(new Vector3(0, 0, -8f), 0.10f).SetEase(Ease.OutQuad));  // 少し傾ける
        seq.Append(star.DORotate(Vector3.zero, 0.12f).SetEase(Ease.OutQuad));          // 元の向きへ戻す
        seq.Append(star.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));                     // 基準スケールへ収束
        seq.Append(star.DOPunchScale(new Vector3(0.10f, 0.10f, 0f), 0.18f, 8, 0.6f)); // 微小パンチで締める

        return seq;
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG/Show (2 stars, checks 1&2)")]
    private void DebugShow()
    {
        Show(2, new[] { true, true, false }); // エディタ上でのデバッグ表示
    }

    [ContextMenu("DEBUG/Hide")]
    private void DebugHide() => Hide();
#endif
}
