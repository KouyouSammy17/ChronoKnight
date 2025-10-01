using UnityEngine;

/// <summary>
/// Global UI Manager singleton, persists across scenes.
/// Controls result UIs, player HUD, and title menu.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Result UIs")]
    [SerializeField] private GameObject _gameClearUI;
    [SerializeField] private GameObject _gameOverUI;

    [Header("Player UI / HUD")]
    [SerializeField, Tooltip("HPバー、燃料、モメンタム等の親オブジェクト（HUD全体）")]
    private GameObject _playerUIRoot;

    [Header("Title UI")]
    [SerializeField, Tooltip("タイトルメニュー全体(Canvasなど)")]
    private GameObject _titleUIRoot;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初期状態
        if (_gameClearUI != null) _gameClearUI.SetActive(false);
        if (_gameOverUI != null) _gameOverUI.SetActive(false);
        if (_playerUIRoot != null) _playerUIRoot.SetActive(false);
        if (_titleUIRoot != null) _titleUIRoot.SetActive(false); // 最初は Titleシーンで GameManager が true にする
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

    public void ResetAllUI()
    {
        if (_gameClearUI != null) _gameClearUI.SetActive(false);
        if (_gameOverUI != null) _gameOverUI.SetActive(false);
        // HUDとタイトルUIはシーンごとの状態で制御する
    }

    // ─── Player HUD ─────────────────────────────
    public void ShowPlayerUI(bool active)
    {
        if (_playerUIRoot != null) _playerUIRoot.SetActive(active);
    }

    // ─── Title UI ───────────────────────────────
    public void ShowTitleUI(bool active)
    {
        if (_titleUIRoot != null) _titleUIRoot.SetActive(active);
    }
}
