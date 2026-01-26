using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Global UI Manager singleton, persists across scenes.
/// Controls result UIs, player HUD, and title menu.
/// </summary>
public enum TutorialKey { Move, Jump, Dash, DashJump, WallJump, Attack, Momentum, Turbo }
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Result UIs")]
    [SerializeField] private GameObject _gameClearUI;
    [SerializeField] private GameObject _gameOverUI;
    [SerializeField] private GameObject _tutorialClearUI; // new


    [Header("Player UI / HUD")]
    [SerializeField, Tooltip("HPバー、燃料、モメンタム等の親オブジェクト（HUD全体）")]
    private GameObject _playerUIRoot;

    [Header("Title UI")]
    [SerializeField, Tooltip("タイトルメニュー全体(Canvasなど)")]
    private GameObject _titleUIRoot;

    [Header("Tutorials (assign under UIManager in the Title/Bootstrap scene)")]
    [SerializeField] private List<TutorialEntry> _tutorials;

    private readonly Dictionary<TutorialKey, TutorialUIAnimator> _map = new();

    [SerializeField] private Canvas[] _rootCanvases;

    [Serializable]
    public struct TutorialEntry
    {
        public TutorialKey key;
        public TutorialUIAnimator ui;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            _map.Clear();
            foreach (var t in _tutorials)
            {
                if (t.ui != null && !_map.ContainsKey(t.key))
                    _map.Add(t.key, t.ui);
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初期状態
        if (_gameClearUI != null) _gameClearUI.SetActive(false);
        if (_gameOverUI != null) _gameOverUI.SetActive(false);
        if (_tutorialClearUI != null) _tutorialClearUI.SetActive(false);
        if (_playerUIRoot != null) _playerUIRoot.SetActive(false);
        if (_titleUIRoot != null) _titleUIRoot.SetActive(false); // 最初は Titleシーンで GameManager が true にする
    }
    public void SetUICamera(Camera cam)
    {
        if (cam == null || _rootCanvases == null) return;

        foreach (var canvas in _rootCanvases)
        {
            if (canvas == null) continue;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
        }
    }

    // ─── Result UI ───────────────────────────────
    public void ShowGameClearUI()
    {
        if (_gameClearUI != null) _gameClearUI.SetActive(true);
    }

    public void ShowGameOverUI()
    {
        if (_gameOverUI != null) _gameOverUI.SetActive(true);
    }

    public void ShowTutorialClearUI()
    {
        if (_tutorialClearUI != null) _tutorialClearUI.SetActive(true);
    }

    public void ResetAllUI()
    {
        // Result overlays
        if (_gameClearUI != null) _gameClearUI.SetActive(false);
        if (_gameOverUI != null) _gameOverUI.SetActive(false);
        if (_tutorialClearUI != null) _tutorialClearUI.SetActive(false);


        // Tutorials (hide all panels)
        HideAllTutorials();

        // NOTE: HUD / Title visibility are still controlled per-scene by GameManager.

        if (_playerUIRoot != null) _playerUIRoot.SetActive(false);
        if (_titleUIRoot != null) _titleUIRoot.SetActive(false);
    }

    // ─── Player HUD ─────────────────────────────
    public void ShowPlayerUI(bool active)
    {
        if (_playerUIRoot != null) _playerUIRoot.SetActive(active);
        _playerUIRoot.GetComponent<UIPlayerBars>()?.PlayIntroAnimation();
    }

    // ─── Title UI ───────────────────────────────
    public void ShowTitleUI(bool active)
    {
        if (_titleUIRoot != null) _titleUIRoot.SetActive(active);
    }

    // ─── Tutorial UI ───────────────────────────────
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public async UniTask ShowTutorialAsync(TutorialKey key)
    {
        if (_map.TryGetValue(key, out var ui) && ui != null)
            await ui.ShowAsync();
    }

    public async UniTask HideTutorialAsync(TutorialKey key)
    {
        if (_map.TryGetValue(key, out var ui) && ui != null)
            await ui.HideAsync();
    }

    public async UniTask TutorialSuccessAsync(TutorialKey key)
    {
        if (_map.TryGetValue(key, out var ui) && ui != null)
            await ui.MarkSuccessAndAutoHideAsync();
    }

    // Fire-and-forget convenience
    public void ShowTutorial(TutorialKey key) => ShowTutorialAsync(key).Forget();
    public void HideTutorial(TutorialKey key) => HideTutorialAsync(key).Forget();
    public void TutorialSuccess(TutorialKey key) => TutorialSuccessAsync(key).Forget();

    public void HideAllTutorials()
    {
        foreach (var kv in _map) kv.Value?.HideAsync().Forget();
    }

}
