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
    // Legacy/simple damage (treated as hazard-like; still triggers hit react by default)
    public bool TakeDamage(int amount)
    {
        return TakeDamageInternal(amount, sourceWorldPos: null, extraKnockback: 0f,
                                  ignoreGates: false, triggerHitReact: true);
    }
    public bool TakeEnemyDamage(int amount, Vector3 sourceWorldPos, float extraKnockback = 0f,
                            bool ignoreGates = false, bool triggerHitReact = true)
    {
        return TakeDamageInternal(amount, sourceWorldPos, extraKnockback, ignoreGates, triggerHitReact);
    }

    public bool TakeHazardDamage(int amount, bool ignoreGates = false, bool triggerHitReact = false)
    {
        // hazards typically shouldn't rotate/knockback; keep triggerHitReact false by default
        return TakeDamageInternal(amount, sourceWorldPos: null, extraKnockback: 0f, ignoreGates, triggerHitReact);
    }

    /// <summary>
    /// Overload that can bypass gates and/or skip hit-reaction (e.g., fall damage).
    /// </summary>
    private bool TakeDamageInternal(int amount, Vector3? sourceWorldPos, float extraKnockback,
                                  bool ignoreGates, bool triggerHitReact)
    {
        if (amount <= 0 || _currentHP <= 0) return false;

        var recv = GetComponent<PlayerDamageReceiver>();

        if (!ignoreGates)
        {
            if (Time.unscaledTime < _noDamageUntilTime) return false;
            if (recv != null && recv.IsInvulnerable) return false;
        }

        _currentHP = Mathf.Max(_currentHP - amount, 0);
        onHealthChanged?.Invoke(_currentHP);

        MomentumManager.Instance?.AddMomentum(-20f);
        MomentumManager.Instance?.BreakMaxLock();
        GetComponent<MomentumBuffsManager>()?.RemoveMaxBuffIfActive();

        if (triggerHitReact && recv != null)
            recv.PlayHitReact(sourceWorldPos, extraKnockback).Forget();

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
