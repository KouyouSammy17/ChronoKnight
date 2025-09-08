using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class IntEvent : UnityEvent<int> { }

public class PlayerStats : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int _maxHP = 100;
    [SerializeField] private int _startingHP = 100;

    [Header("Stamina Settings")]
    [SerializeField] private int _maxStamina = 100;
    [SerializeField] private int _startingStamina = 100;
    [SerializeField] private float _staminaRegenRate = 10f;  // points per second

    public IntEvent onHealthChanged;
    public IntEvent onStaminaChanged;

    private int _currentHP;
    private int _currentStamina;
    private float _staminaRegenAccumulator;

    public int CurrentHP => _currentHP;
    public int MaxHP => _maxHP;
    public int CurrentStamina => _currentStamina;
    public int MaxStamina => _maxStamina;

    private void Awake()
    {
        ResetStats();
    }

    private void Update()
    {
        RegenerateStamina();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || _currentHP <= 0) return;

        var recv = GetComponent<PlayerDamageReceiver>();
        if (recv != null && recv.IsInvulnerable) return;

        _currentHP = Mathf.Max(_currentHP - amount, 0);
        onHealthChanged?.Invoke(_currentHP);

        // Momentum penalties you already have stay as-isÅc
        MomentumManager.Instance?.AddMomentum(-20f);
        MomentumManager.Instance?.BreakMaxLock();
        GetComponent<MomentumBuffsManager>()?.RemoveMaxBuffIfActive();

        // Kick the hit reaction (no source position known here)
        recv?.PlayHitReact(null, 0f).Forget();

        if (_currentHP == 0) Die();
    }

    public bool SpendStamina(int cost)
    {
        if (cost <= 0) return true;
        if (_currentStamina < cost) return false;
        _currentStamina -= cost;
        onStaminaChanged?.Invoke(_currentStamina);
        return true;
    }

    private void RegenerateStamina()
    {
        if (_currentStamina < _maxStamina)
        {
            _staminaRegenAccumulator += _staminaRegenRate * Time.deltaTime;
            int regenPoints = Mathf.FloorToInt(_staminaRegenAccumulator);
            if (regenPoints > 0)
            {
                _staminaRegenAccumulator -= regenPoints;
                _currentStamina = Mathf.Min(_currentStamina + regenPoints, _maxStamina);
                onStaminaChanged?.Invoke(_currentStamina);
            }
        }
        else
        {
            _staminaRegenAccumulator = 0f;
        }
    }

    /// <summary>
    /// Resets both HP & Stamina (and the regenÅ]accumulator) to starting values,
    /// then pushes UI events.
    /// Call this from GameManager when a scene loads or you RestartLevel().
    /// </summary>
    public void ResetStats()
    {
        _currentHP = Mathf.Clamp(_startingHP, 0, _maxHP);
        _currentStamina = Mathf.Clamp(_startingStamina, 0, _maxStamina);
        _staminaRegenAccumulator = 0f;
        onHealthChanged?.Invoke(_currentHP);
        onStaminaChanged?.Invoke(_currentStamina);
    }

    private void Die()
    {
        GameManager.Instance.GameOver();
    }

    
}
