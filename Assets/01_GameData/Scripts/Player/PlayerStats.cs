using UnityEngine;
using UnityEngine.Events;

/// <summary>Custom UnityEvent for integer value changes</summary>
[System.Serializable] public class IntEvent : UnityEvent<int> { }

/// <summary>
/// Manages player health and stamina stats.
/// Handles damage intake with gate checking, stamina regeneration, and damage events.
/// Integrates with momentum system and hit reaction triggers.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    /// <summary>Maximum health points the player can have</summary>
    [Header("Health Settings")]
    [SerializeField] private int _maxHP = 100;
    /// <summary>Starting health for new scenes/levels</summary>
    [SerializeField] private int _startingHP = 100;

    /// <summary>Maximum stamina points the player can have</summary>
    [Header("Stamina Settings")]
    [SerializeField] private int _maxStamina = 100;
    /// <summary>Starting stamina for new scenes/levels</summary>
    [SerializeField] private int _startingStamina = 100;
    /// <summary>Stamina regeneration rate in points per second</summary>
    [SerializeField] private float _staminaRegenRate = 10f;

    /// <summary>Event fired when health changes (passes new HP value)</summary>
    public IntEvent onHealthChanged;
    /// <summary>Event fired when stamina changes (passes new stamina value)</summary>
    public IntEvent onStaminaChanged;

    /// <summary>Current health points</summary>
    private int _currentHP;
    /// <summary>Current stamina points</summary>
    private int _currentStamina;
    /// <summary>Accumulator for smooth stamina regeneration</summary>
    private float _staminaRegenAccumulator;

    /// <summary>Unscaled time until which damage is blocked (no-damage immunity window)</summary>
    private float _noDamageUntilTime;

    /// <summary>Current health points</summary>
    public int CurrentHP => _currentHP;
    /// <summary>Maximum health points</summary>
    public int MaxHP => _maxHP;
    /// <summary>Current stamina points</summary>
    public int CurrentStamina => _currentStamina;
    /// <summary>Maximum stamina points</summary>
    public int MaxStamina => _maxStamina;

    /// <summary>Initializes events and resets stats to starting values</summary>
    private void Awake()
    {
        // make sure events exist
        onHealthChanged ??= new IntEvent();
        onStaminaChanged ??= new IntEvent();
        ResetStats();
    }

    /// <summary>Updates stamina regeneration each frame</summary>
    private void Update()
    {
        RegenerateStamina();
    }

    /// <summary>
    /// Creates a temporary no-damage immunity window.
    /// Uses unscaled time, so it works during pauses and other time manipulations.
    /// </summary>
    /// <param name="seconds">Duration of immunity in seconds</param>
    public void ArmNoDamageFor(float seconds)
    {
        _noDamageUntilTime = Mathf.Max(_noDamageUntilTime, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    /// <summary>
    /// Simple damage intake (hazard-like damage that triggers default hit reaction).
    /// </summary>
    /// <param name="amount">Damage amount to subtract from HP</param>
    /// <returns>True if damage was applied, false if blocked by gates</returns>
    public bool TakeDamage(int amount)
    {
        return TakeDamageInternal(amount, sourceWorldPos: null, extraKnockback: 0f,
                                  ignoreGates: false, triggerHitReact: true);
    }

    /// <summary>
    /// Damage from enemies with knockback and directional information.
    /// </summary>
    /// <param name="amount">Damage amount</param>
    /// <param name="sourceWorldPos">World position of the attacker (for knockback direction)</param>
    /// <param name="extraKnockback">Additional knockback force modifier</param>
    /// <param name="ignoreGates">Whether to bypass invulnerability/no-damage gates</param>
    /// <param name="triggerHitReact">Whether to play hit reaction animation</param>
    /// <returns>True if damage was applied</returns>
    public bool TakeEnemyDamage(int amount, Vector3 sourceWorldPos, float extraKnockback = 0f,
                            bool ignoreGates = false, bool triggerHitReact = true)
    {
        return TakeDamageInternal(amount, sourceWorldPos, extraKnockback, ignoreGates, triggerHitReact);
    }

    /// <summary>
    /// Damage from environmental hazards (typically no knockback or rotation).
    /// </summary>
    /// <param name="amount">Damage amount</param>
    /// <param name="ignoreGates">Whether to bypass damage gates</param>
    /// <param name="triggerHitReact">Whether to play hit reaction (usually false for hazards)</param>
    /// <returns>True if damage was applied</returns>
    public bool TakeHazardDamage(int amount, bool ignoreGates = false, bool triggerHitReact = false)
    {
        // hazards typically shouldn't rotate/knockback; keep triggerHitReact false by default
        return TakeDamageInternal(amount, sourceWorldPos: null, extraKnockback: 0f, ignoreGates, triggerHitReact);
    }

    /// <summary>
    /// Internal damage processing with full gate checking and momentum integration.
    /// Applies damage, momentum penalties, and triggers hit reactions.
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

        // Damage penalty to momentum system
        MomentumManager.Instance?.AddMomentum(-20f);
        MomentumManager.Instance?.BreakMaxLock();
        GetComponent<MomentumBuffsManager>()?.RemoveMaxBuffIfActive();

        if (triggerHitReact && recv != null)
            recv.PlayHitReact(sourceWorldPos, extraKnockback).Forget();

        if (_currentHP == 0) Die();
        return true;
    }

    /// <summary>
    /// Attempts to spend stamina for actions like dashing.
    /// </summary>
    /// <param name="cost">Amount of stamina to consume</param>
    /// <returns>True if stamina was available and spent, false if insufficient</returns>
    public bool SpendStamina(int cost)
    {
        if (cost <= 0) return true;
        if (_currentStamina < cost) return false;
        _currentStamina -= cost;
        onStaminaChanged?.Invoke(_currentStamina);
        return true;
    }

    /// <summary>
    /// Regenerates stamina each frame based on configured regen rate.
    /// Uses accumulator for smooth, frame-rate independent regeneration.
    /// </summary>
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
    /// Resets health and stamina to starting values.
    /// Call this when loading a scene or restarting a level.
    /// Clears immunity gates and triggers update events.
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

    /// <summary>Triggers game over sequence when HP reaches zero</summary>
    private void Die()
    {
        GameManager.Instance.GameOver();
    }
}
