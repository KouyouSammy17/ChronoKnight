using UnityEngine;
using UnityEngine.Events;

[System.Serializable] public class IntEvent : UnityEvent<int> { }

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

    // NEW: global recent-damage cooldown (unscaled time so it works during pauses)
    private float _noDamageUntilTime;

    public int CurrentHP => _currentHP;
    public int MaxHP => _maxHP;
    public int CurrentStamina => _currentStamina;
    public int MaxStamina => _maxStamina;

    private void Awake()
    {
        // make sure events exist
        onHealthChanged ??= new IntEvent();
        onStaminaChanged ??= new IntEvent();
        ResetStats();
    }

    private void Update()
    {
        RegenerateStamina();
    }

    /// <summary>
    /// Arm a short no-damage window that ignores ALL incoming damage until the given time.
    /// Uses Time.unscaledTime so it also works while the game is paused.
    /// </summary>
    public void ArmNoDamageFor(float seconds)
    {
        _noDamageUntilTime = Mathf.Max(_noDamageUntilTime, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    /// <summary>
    /// Standard damage entry point.
    /// </summary>
    public void TakeDamage(int amount) => TakeDamage(amount, ignoreGates: false, triggerHitReact: true);

    /// <summary>
    /// Overload that can bypass gates and/or skip hit-reaction (e.g., fall damage).
    /// </summary>
    public bool TakeDamage(int amount, bool ignoreGates, bool triggerHitReact)
    {
        if (amount <= 0 || _currentHP <= 0) return false;

        var recv = GetComponent<PlayerDamageReceiver>();

        if (!ignoreGates)
        {
            // recent-damage cooldown
            if (Time.unscaledTime < _noDamageUntilTime) return false;
            // global invuln flag (set by hit-react or externally)
            if (recv != null && recv.IsInvulnerable) return false;
        }

        _currentHP = Mathf.Max(_currentHP - amount, 0);
        onHealthChanged?.Invoke(_currentHP);

        // Momentum penalties (keep your existing logic)
        MomentumManager.Instance?.AddMomentum(-20f);
        MomentumManager.Instance?.BreakMaxLock();
        GetComponent<MomentumBuffsManager>()?.RemoveMaxBuffIfActive();

        // Kick the hit reaction unless explicitly skipped (e.g., fall damage)
        if (triggerHitReact) recv?.PlayHitReact(null, 0f).Forget();

        if (_currentHP == 0) Die();
        return true;
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
    /// Resets both HP & Stamina (and the regen accumulator) to starting values,
    /// then pushes UI events. Call this from GameManager when a scene loads or you RestartLevel().
    /// </summary>
    public void ResetStats()
    {
        _currentHP = Mathf.Clamp(_startingHP, 0, _maxHP);
        _currentStamina = Mathf.Clamp(_startingStamina, 0, _maxStamina);
        _staminaRegenAccumulator = 0f;
        _noDamageUntilTime = 0f;
        onHealthChanged?.Invoke(_currentHP);
        onStaminaChanged?.Invoke(_currentStamina);
    }

    private void Die()
    {
        GameManager.Instance.GameOver();
    }
}
