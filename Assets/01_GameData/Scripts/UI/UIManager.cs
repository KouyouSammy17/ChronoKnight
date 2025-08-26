using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A simple singleton UI manager that persists across scene loads.
/// Attach this to a GameObject named ÅgUIManagerÅh in your first-loaded scene,
/// with a Canvas (and all relevant UI children) under it.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField, Tooltip("Game Clear UI")] private GameObject _gameClearUI;
    [SerializeField, Tooltip("Game Over UI")] private GameObject _gameOverUI;


    private void Awake()
    {
        // 1) Enforce singleton pattern
        if (Instance == null)
        {
            Instance = this;
            // 2) Prevent this object (and all its children) from being destroyed on scene load
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            // If another UIManager already exists, destroy this one
            Destroy(gameObject);
            return;
        }

        if (_gameClearUI != null)
            _gameClearUI.SetActive(false);

        if (_gameOverUI != null)
            _gameOverUI.SetActive(false);
    }

    public void ShowGameClearUI()
    {
        _gameClearUI.SetActive(true);
    }

    public void ShowGameOverUI()
    {
        _gameOverUI.SetActive(true);
    }

    public void ResetAllUI()
    {
        _gameClearUI.SetActive(false);
        _gameOverUI.SetActive(false);
    }
}
