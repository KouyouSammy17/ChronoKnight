// シーンをまたいで全UIを一元管理するシングルトンスクリプト
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Global UI Manager singleton, persists across scenes.
/// Controls result UIs, player HUD, and title menu.
/// </summary>
public enum TutorialKey { Move, Jump, Dash, DashJump, WallJump, Attack, Momentum, Turbo } // チュートリアル種別の識別キー

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; } // シングルトンインスタンス

    [Header("Result UIs")]
    [SerializeField] private GameObject _gameClearUI;     // ゲームクリアUIのルートオブジェクト
    [SerializeField] private GameObject _gameOverUI;      // ゲームオーバーUIのルートオブジェクト
    [SerializeField] private GameObject _tutorialClearUI; // new

    // Anim controllers (optional but recommended)
    [SerializeField] private GameOverUI_Anim _gameOverAnim; // ゲームオーバーアニメーションコントローラー



    [Header("Player UI / HUD")]
    [SerializeField, Tooltip("HPバー、コア、もろもろたちの親オブジェクト（HUD全域）")]
    private GameObject _playerUIRoot; // プレイヤーHUD全体のルートオブジェクト

    [Header("Title UI")]
    [SerializeField, Tooltip("タイトルメニュー全域(Canvasなら)")]
    private GameObject _titleUIRoot; // タイトルUIのルートオブジェクト

    [Header("Tutorials (assign under UIManager in the Title/Bootstrap scene)")]
    [SerializeField] private List<TutorialEntry> _tutorials; // チュートリアルUIエントリのリスト

    private readonly Dictionary<TutorialKey, TutorialUIAnimator> _map = new(); // キーとUIアニメーターのマッピング

    [SerializeField] private Canvas[] _rootCanvases; // カメラ設定を適用する全Canvasの配列

    [Serializable]
    public struct TutorialEntry
    {
        public TutorialKey key; // チュートリアルの識別キー
        public TutorialUIAnimator ui; // 対応するUIアニメーター
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject); // シーンをまたいでこのオブジェクトを保持
            _map.Clear();
            foreach (var t in _tutorials)
            {
                if (t.ui != null && !_map.ContainsKey(t.key))
                    _map.Add(t.key, t.ui); // チュートリアルキーとUIをマッピングに登録
            }
        }
        else
        {
            Destroy(gameObject); // 重複インスタンスを破棄してシングルトンを維持
            return;
        }

        // 初期化
        if (_gameClearUI != null) _gameClearUI.SetActive(false);
        if (_gameOverUI != null) _gameOverUI.SetActive(false);
        if (_gameOverAnim != null) _gameOverAnim.HardHideInstant();       // アニメーション状態もリセット
        if (_tutorialClearUI != null) _tutorialClearUI.SetActive(false);
        if (_playerUIRoot != null) _playerUIRoot.SetActive(false);
        if (_titleUIRoot != null) _titleUIRoot.SetActive(false); // 最初は Title シーンで GameManager が true にする
    }

    public void SetUICamera(Camera cam)
    {
        if (cam == null || _rootCanvases == null) return;

        foreach (var canvas in _rootCanvases)
        {
            if (canvas == null) continue;

            canvas.renderMode = RenderMode.ScreenSpaceCamera; // スクリーンスペースカメラモードに切り替え
            canvas.worldCamera = cam;                         // 使用するカメラを設定
        }
    }

    // ─── Result UI ─────────────────────────────────────────
    public void ShowGameClearUI()
    {
        if (_gameClearUI != null) _gameClearUI.SetActive(true); // ゲームクリアUIを表示
    }

    public void ShowGameOverUI()
    {
        if (_gameOverUI != null) _gameOverUI.SetActive(true); // ゲームオーバーUIを表示
    }

    public void ShowTutorialClearUI()
    {
        if (_tutorialClearUI != null) _tutorialClearUI.SetActive(true); // チュートリアルクリアUIを表示
    }

    public void ResetAllUI()
    {
        // Result overlays
        if (_gameClearUI != null) _gameClearUI.SetActive(false);       // クリアUIを非表示
        if (_gameOverAnim != null) _gameOverAnim.HardHideInstant();    // ゲームオーバーUIをアニメーションなしで即非表示
        if (_tutorialClearUI != null) _tutorialClearUI.SetActive(false);


        // Tutorials (hide all panels)
        HideAllTutorials(); // 全チュートリアルUIを非表示

        // NOTE: HUD / Title visibility are still controlled per-scene by GameManager.

        if (_playerUIRoot != null) _playerUIRoot.SetActive(false); // プレイヤーHUDを非表示
        if (_titleUIRoot != null) _titleUIRoot.SetActive(false);   // タイトルUIを非表示
    }

    public void ShowGameOverUI_Animated()
    {
        if (_gameOverUI != null) _gameOverUI.SetActive(true);

        if (_gameOverAnim == null && _gameOverUI != null)
            _gameOverAnim = _gameOverUI.GetComponent<GameOverUI_Anim>(); // アニメーターが未設定なら動的に取得

        _gameOverAnim?.Show(); // アニメーション付きでゲームオーバーUIを表示
    }


    // ─── Player HUD ────────────────────────────────────────
    public void ShowPlayerUI(bool active)
    {
        if (_playerUIRoot != null) _playerUIRoot.SetActive(active);
        _playerUIRoot.GetComponent<UIPlayerBars>()?.PlayIntroAnimation(); // HUD表示時にバーのイントロアニメーションを再生
    }

    // ─── Title UI ──────────────────────────────────────────
    public void ShowTitleUI(bool active)
    {
        if (_titleUIRoot != null) _titleUIRoot.SetActive(active); // タイトルUIの表示・非表示を切り替え
    }

    // ─── Tutorial UI ───────────────────────────────────────
    private void OnDestroy()
    {
        if (Instance == this) Instance = null; // このインスタンスが破棄されたらシングルトン参照をクリア
    }

    public async UniTask ShowTutorialAsync(TutorialKey key)
    {
        if (_map.TryGetValue(key, out var ui) && ui != null)
            await ui.ShowAsync(); // 対応するチュートリアルUIを非同期で表示
    }

    public async UniTask HideTutorialAsync(TutorialKey key)
    {
        if (_map.TryGetValue(key, out var ui) && ui != null)
            await ui.HideAsync(); // 対応するチュートリアルUIを非同期で非表示
    }

    public async UniTask TutorialSuccessAsync(TutorialKey key)
    {
        if (_map.TryGetValue(key, out var ui) && ui != null)
            await ui.MarkSuccessAndAutoHideAsync(); // 成功マークを付けて自動的に非表示にする
    }

    // Fire-and-forget convenience
    public void ShowTutorial(TutorialKey key) => ShowTutorialAsync(key).Forget();    // 非同期を待機しない簡易版
    public void HideTutorial(TutorialKey key) => HideTutorialAsync(key).Forget();    // 非同期を待機しない簡易版
    public void TutorialSuccess(TutorialKey key) => TutorialSuccessAsync(key).Forget(); // 非同期を待機しない簡易版

    public void HideAllTutorials()
    {
        foreach (var kv in _map) kv.Value?.HideAsync().Forget(); // 全チュートリアルUIをまとめて非表示
    }

}
