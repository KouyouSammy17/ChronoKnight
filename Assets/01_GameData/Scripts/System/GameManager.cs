using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using MoreMountains.Tools; // ← MMSceneLoading / MMAdditiveSceneLoading

public enum GameState { Title, Playing, Clear, GameOver }

/// <summary>
/// One-file manager: scene flow + player spawn + UI hooks
/// Uses Feel's MMSceneLoading for transitions.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string _titleScene = "Title";
    [SerializeField] private string _firstLevel = "Level_01";

    [Header("Feel MMSceneLoading")]
    [Tooltip("OFF = MMSceneLoadingManager (non-additive). ON = MMAdditiveSceneLoadingManager.")]
    [SerializeField] private bool _useAdditive = false;
    [Tooltip("Non-additive loading scene name (must be in Build Settings).")]
    [SerializeField] private string _feelLoadingScene = "LoadingScreen";
    [Tooltip("Additive loading scene name (must be in Build Settings).")]
    [SerializeField] private string _feelAdditiveLoadingScene = "MMAdditiveLoadingScreen";
    [SerializeField, Range(0f, 1f)] private float _entryFade = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _exitFade = 0.35f;
    [SerializeField] private bool _interpolateProgress = true;

    [Header("Player Spawn")]
    [SerializeField] private PlayerController _playerPrefab; // optional: spawn if none present

    [Header("Debug")]
    [SerializeField] private bool _allowStartFromAnyScene = true;

    public GameState State { get; private set; } = GameState.Title;

    // runtime refs
    private PlayerController _player;
    private Transform _spawnPoint; // Tag: Respawn

    // ───────────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ───────────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (_allowStartFromAnyScene)
        {
            State = SceneManager.GetActiveScene().name == _titleScene ? GameState.Title : GameState.Playing;
            BindSpawnAndPlayer();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Public API (called from UI or gameplay)
    // ───────────────────────────────────────────────────────────────────────────────
    public void LoadTitle() => LoadWithFeel(_titleScene, GameState.Title);
    public void StartNewGame() => LoadWithFeel(_firstLevel, GameState.Playing);

    public void RestartLevel()
    {
        string current = SceneManager.GetActiveScene().name;
        LoadWithFeel(current, GameState.Playing);
    }

    public void LoadNextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(next);
            string name = Path.GetFileNameWithoutExtension(path);
            LoadWithFeel(name, GameState.Playing);
        }
        else
        {
            LoadTitle();
        }
    }

    public void WinLevel()
    {
        if (State != GameState.Playing) return;
        State = GameState.Clear;
        UIManager.Instance?.ResetAllUI();
        UIManager.Instance?.ShowGameClearUI();
    }

    public void GameOver()
    {
        if (State == GameState.GameOver) return;
        State = GameState.GameOver;
        UIManager.Instance?.ResetAllUI();
        UIManager.Instance?.ShowGameOverUI();
    }

    public PlayerController GetPlayer() => _player;

    public void RespawnPlayer()
    {
        if (_player == null) return;
        if (_spawnPoint != null)
            _player.transform.position = _spawnPoint.position;

        var rb = _player.GetRigidbody();
        rb.linearVelocity = Vector3.zero;                  // Unity 6 PhysX property
        _player.ResetPlayerState();
        _player.GetComponent<PlayerStats>()?.ResetStats();
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // MMSceneLoading wrapper
    // ───────────────────────────────────────────────────────────────────────────────

    private bool IsLoadingScene(string sceneName)
    {
        if (_useAdditive)
            return !string.IsNullOrEmpty(_feelAdditiveLoadingScene) && sceneName == _feelAdditiveLoadingScene;
        else
            return !string.IsNullOrEmpty(_feelLoadingScene) && sceneName == _feelLoadingScene;
    }
    private void LoadWithFeel(string sceneName, GameState targetState)
    {
        UIManager.Instance?.ResetAllUI();
        MomentumManager.Instance?.ResetAll();

        if (_useAdditive)
        {
            var settings = new MMAdditiveSceneLoadingManagerSettings
            {
                LoadingSceneName = _feelAdditiveLoadingScene,
                InterpolateProgress = _interpolateProgress,
                EntryFadeDuration = _entryFade,
                ExitFadeDuration = _exitFade
            };
            MMAdditiveSceneLoadingManager.LoadScene(sceneName, settings);
        }
        else
        {
            MMSceneLoadingManager.LoadScene(sceneName, _feelLoadingScene);
        }

        // You can keep this, it won't hurt—OnSceneLoaded re-evaluates by actual scene name
        State = targetState;
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Scene hooks
    // ───────────────────────────────────────────────────────────────────────────────
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If we just entered the MM loading scene, hide everything gameplay-related and bail out
        if (IsLoadingScene(scene.name))
        {
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.ShowTitleUI(false);
            return; // wait for the real destination scene to load next
        }

        // Title or Gameplay?
        State = scene.name == _titleScene ? GameState.Title : GameState.Playing;

        BindSpawnAndPlayer();

        if (State == GameState.Playing)
        {
            UIManager.Instance?.ShowTitleUI(false);
            UIManager.Instance?.ShowPlayerUI(true);
            _player?.GetComponent<PlayerStats>()?.ResetStats();
        }
        else // Title
        {
            UIManager.Instance?.ShowPlayerUI(false);
            UIManager.Instance?.ShowTitleUI(true);
        }
    }

    private void BindSpawnAndPlayer()
    {
        var spawnGo = GameObject.FindGameObjectWithTag("Respawn");
        _spawnPoint = spawnGo ? spawnGo.transform : null;

        _player = Object.FindFirstObjectByType<PlayerController>();
        if (_player == null && _playerPrefab != null && State == GameState.Playing)
        {
            Vector3 pos = _spawnPoint ? _spawnPoint.position : Vector3.zero;
            _player = Instantiate(_playerPrefab, pos, Quaternion.identity);
        }
    }
}
