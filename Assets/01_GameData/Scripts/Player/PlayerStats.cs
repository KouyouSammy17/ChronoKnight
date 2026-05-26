// プレイヤーのHP・スタミナなどのステータスを管理するスクリプト
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
    [SerializeField] private int _maxHP = 100; // HPの最大値
    /// <summary>Starting health for new scenes/levels</summary>
    [SerializeField] private int _startingHP = 100; // シーン開始時の初期HP

    /// <summary>Maximum stamina points the player can have</summary>
    [Header("Stamina Settings")]
    [SerializeField] private int _maxStamina = 100; // スタミナの最大値
    /// <summary>Starting stamina for new scenes/levels</summary>
    [SerializeField] private int _startingStamina = 100; // シーン開始時の初期スタミナ
    /// <summary>Stamina regeneration rate in points per second</summary>
    [SerializeField] private float _staminaRegenRate = 10f; // スタミナの毎秒回復量

    /// <summary>Event fired when health changes (passes new HP value)</summary>
    public IntEvent onHealthChanged; // HP変化時に発火するイベント（新しいHP値を渡す）
    /// <summary>Event fired when stamina changes (passes new stamina value)</summary>
    public IntEvent onStaminaChanged; // スタミナ変化時に発火するイベント

    /// <summary>Current health points</summary>
    private int _currentHP; // 現在のHP
    /// <summary>Current stamina points</summary>
    private int _currentStamina; // 現在のスタミナ
    /// <summary>Accumulator for smooth stamina regeneration</summary>
    private float _staminaRegenAccumulator; // スタミナ回復の端数蓄積用アキュムレータ

    /// <summary>Unscaled time until which damage is blocked (no-damage immunity window)</summary>
    private float _noDamageUntilTime; // ノーダメージ免疫期間の終了時刻（UnscaledTime基準）

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
        onHealthChanged ??= new IntEvent(); // イベントが未初期化なら生成
        onStaminaChanged ??= new IntEvent(); // イベントが未初期化なら生成
        ResetStats(); // ステータスを初期値にリセット
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
        if (amount <= 0 || _currentHP <= 0) return false; // ダメージが0以下またはすでに死亡中なら無効

        var recv = GetComponent<PlayerDamageReceiver>();

        if (!ignoreGates)
        {
            if (Time.unscaledTime < _noDamageUntilTime) return false; // ノーダメージ期間中はブロック
            if (recv != null && recv.IsInvulnerable) return false; // 無敵状態中はブロック
        }

        _currentHP = Mathf.Max(_currentHP - amount, 0); // HPをダメージ分減算（最低0まで）
        onHealthChanged?.Invoke(_currentHP); // HP変化イベントを発火

        // Damage penalty to momentum system
        MomentumManager.Instance?.AddMomentum(-20f); // ダメージによるモメンタムペナルティ
        MomentumManager.Instance?.BreakMaxLock(); // モメンタム最大ロックを解除
        GetComponent<MomentumBuffsManager>()?.RemoveMaxBuffIfActive(); // 最大バフをアクティブなら除去

        if (triggerHitReact && recv != null)
            recv.PlayHitReact(sourceWorldPos, extraKnockback).Forget(); // ヒットリアクションを非同期再生

        if (_currentHP == 0) Die(); // HPが0になったら死亡処理
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
            _staminaRegenAccumulator += _staminaRegenRate * Time.deltaTime; // 経過時間に応じてアキュムレータを加算
            int regenPoints = Mathf.FloorToInt(_staminaRegenAccumulator); // 整数部分を回復ポイントとして取得
            if (regenPoints > 0)
            {
                _staminaRegenAccumulator -= regenPoints; // 使用した分だけアキュムレータを減らす
                _currentStamina = Mathf.Min(_currentStamina + regenPoints, _maxStamina); // スタミナを回復（最大値を超えない）
                onStaminaChanged?.Invoke(_currentStamina); // スタミナ変化イベントを発火
            }
        }
        else
        {
            _staminaRegenAccumulator = 0f; // スタミナが最大値の場合はアキュムレータをリセット
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
