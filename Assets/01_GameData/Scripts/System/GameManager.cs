using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Respawn Settings")]
    [SerializeField] private string _respawnPointTagOrName = "RespawnPoint";

    [Header("Entry Animation Lock")]
    [SerializeField] private float _entryLockDuration = 1.5f;

    [Header("Fall Death Threshold")]
    [SerializeField] private float _fallDeathY = -30f;

    private Transform _respawnPoint;
    private PlayerController _player;
    private bool _hasWon;
    private bool _isGameOver;

    public PlayerController GetPlayer() => _player;

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
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _hasWon = false;
        _isGameOver = false;

        CacheRespawnPoint();
        CachePlayer();

        if (_player == null) return;

        ResetPlayerState();
        UIManager.Instance?.ResetAllUI();
        StartCoroutine(ReEnableInputAfterDelay(_entryLockDuration));
    }

    private void Update()
    {
        if (_player == null || _hasWon || _isGameOver) return;

        if (_player.transform.position.y < _fallDeathY)
        {
            var stats = _player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.TakeDamage(20);
                if (stats.CurrentHP > 0)
                    RespawnPlayer(_player);
            }
            else
            {
                RespawnPlayer(_player);
            }
        }
    }

    private void CacheRespawnPoint()
    {
        var rpGO = GameObject.FindWithTag(_respawnPointTagOrName) ?? GameObject.Find(_respawnPointTagOrName);
        _respawnPoint = rpGO?.transform;

        if (_respawnPoint == null)
            Debug.LogWarning($"GameManager: No '{_respawnPointTagOrName}' found in scene.");
    }

    private void CachePlayer()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null)
        {
            Debug.LogWarning("GameManager: No GameObject tagged 'Player' in scene.");
            return;
        }

        _player = playerGO.GetComponent<PlayerController>();
        if (_player == null)
        {
            Debug.LogWarning("GameManager: Player GameObject is missing PlayerController.");
            return;
        }

        Debug.Log($"GameManager: Found PlayerController on '{playerGO.name}'.");
    }

    private void ResetPlayerState()
    {
        _player.DisableInput();

        var stats = _player.GetComponent<PlayerStats>();
        stats?.ResetStats();

        MomentumManager.Instance?.ResetAll();
    }

    private IEnumerator ReEnableInputAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_player != null && !_hasWon && !_isGameOver)
            _player.EnableInput();
    }

    public void WinLevel()
    {
        if (_hasWon || _isGameOver) return;
        _hasWon = true;

        _player?.DisableInput();
        UIManager.Instance?.ShowGameClearUI();
    }

    public void GameOver()
    {
        if (_hasWon || _isGameOver) return;
        _isGameOver = true;

        _player?.DisableInput();
        UIManager.Instance?.ShowGameOverUI();
    }

    public void RestartLevel()
    {
        _hasWon = false;
        _isGameOver = false;

        UIManager.Instance?.ResetAllUI();
        MomentumManager.Instance?.ResetAll();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RespawnPlayer(PlayerController player)
    {
        if (_respawnPoint == null)
        {
            Debug.LogWarning("GameManager: No respawn point assigned.");
            return;
        }

        player.transform.position = _respawnPoint.position;

        if (player.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        player.ResetPlayerState();
    }
}
