using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class UIPlayerBars : MonoBehaviour
{
    [SerializeField] private Slider _hpSlider;
    [SerializeField] private Slider _staminaSlider;
    [SerializeField] private float _tweenDuration = 0.3f;

    private PlayerStats _stats;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe();
    }

    private void Start()
    {
        // First‐time setup
        Initialize();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re–initialize on every scene load (incl. RestartLevel)
        Initialize();
    }

    private void Initialize()
    {
        Unsubscribe();

        _stats = Object.FindAnyObjectByType<PlayerStats>();
        if (_stats == null) return;

        // Match slider ranges
        _hpSlider.maxValue = _stats.MaxHP;
        _staminaSlider.maxValue = _stats.MaxStamina;

        // Snap bars to current stats
        UpdateHP(_stats.CurrentHP);
        UpdateStamina(_stats.CurrentStamina);

        // Subscribe to further changes
        _stats.onHealthChanged.AddListener(UpdateHP);
        _stats.onStaminaChanged.AddListener(UpdateStamina);
    }

    private void Unsubscribe()
    {
        if (_stats != null)
        {
            _stats.onHealthChanged.RemoveListener(UpdateHP);
            _stats.onStaminaChanged.RemoveListener(UpdateStamina);
            _stats = null;
        }
    }

    private void UpdateHP(int hp)
    {
        _hpSlider.DOValue(hp, _tweenDuration).SetEase(Ease.OutQuad);
    }

    private void UpdateStamina(int sta)
    {
        _staminaSlider.DOValue(sta, _tweenDuration).SetEase(Ease.OutQuad);
    }
}