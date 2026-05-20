using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Manages player combat mechanics including ground combos, air attacks, dash attacks, and finishers.
/// Handles attack buffering, momentum scaling, and turbo mode integration.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CombatController : MonoBehaviour
{
    /// <summary>
    /// Defines a single step in a ground combo sequence with timing and damage properties.
    /// </summary>
    [Serializable]
    public struct ComboStep
    {
        /// <summary>Name identifier for this combo step</summary>
        public string stepName;
        /// <summary>Time window during which the next attack can be buffered</summary>
        public float inputWindow;
        /// <summary>Animation speed multiplier for this attack</summary>
        public float speedMultiplier;
        /// <summary>Base damage dealt by this attack</summary>
        public int damage;
        /// <summary>Momentum points gained from hitting with this attack</summary>
        public float momentumGain;
        /// <summary>Knockback force applied to enemies</summary>
        public float knockbackForce;
    }

    // ==================== SERIALIZED CONFIGURATION ====================

    /// <summary>Sequence of attacks that form the ground combo chain</summary>
    [Header("Ground Combo Definition")]
    [SerializeField] private List<ComboStep> _comboSteps = new List<ComboStep>();

    /// <summary>Speed and damage configuration for air attacks</summary>
    [Header("Air Attack (Single)")]
    [SerializeField] private float _airAttackSpeedMult = 1.0f;
    [SerializeField] private int _airAttackDamage = 15;
    [SerializeField] private float _airAttackMomentum = 8f;
    [SerializeField] private float _airAttackKnockback = 8f;

    /// <summary>If enabled, limits air attacks to once per airtime (resets when landing)</summary>
    [Tooltip("If ON, you can only do 1 air attack per airtime (resets on landing).")]
    [SerializeField] private bool _airAttackOncePerAirtime = true;

    /// <summary>References to other player components (auto-assigned at runtime)</summary>
    [Header("References (auto-assigned)")]
    [SerializeField] private PlayerMotor _motor;
    [SerializeField] private PlayerAnimator _playerAnim;
    [SerializeField] private WeaponHitbox _weaponHitbox;

    /// <summary>Configuration for dash attack mechanics</summary>
    [Header("Dash Attack")]
    [SerializeField] private float _dashAttackSpeedMult = 1.0f;
    [SerializeField] private int _dashAttackDamage = 20;
    [SerializeField] private float _dashAttackMomentum = 10f;
    [SerializeField] private float _dashAttackKnockback = 10f;

    /// <summary>Settings for the maximum momentum AOE finisher attack (4th hit)</summary>
    [Header("Max Momentum 4th Hit (AOE Finisher)")]
    [SerializeField] private bool _enableMaxFinisher = true;
    [SerializeField] private float _finisherSpeedMult = 1.0f;
    [SerializeField] private int _finisherDamage = 45;
    [SerializeField] private float _finisherMomentumGain = 0f;
    [SerializeField] private float _finisherKnockback = 14f;
    /// <summary>Separate hitbox for AOE finisher effect (if null, uses weapon hitbox)</summary>
    [SerializeField] private WeaponHitbox _finisherHitbox;

    // ==================== RUNTIME STATE ====================

    /// <summary>Current position in the combo sequence</summary>
    private int _comboIndex;
    /// <summary>Whether the combo window is open for buffering next attack</summary>
    private bool _canBuffer;
    /// <summary>Whether an attack input was buffered during combo window</summary>
    private bool _bufferedAttack;

    /// <summary>Whether a dash attack is currently active</summary>
    private bool _dashAttackActive;
    /// <summary>Whether an attack was buffered while dash attack was playing</summary>
    private bool _dashAttackChainBuffered;
    /// <summary>Whether current attack mode is dash attack</summary>
    private bool _dashAttackMode;

    /// <summary>Whether current attack mode is air attack</summary>
    private bool _airAttackMode;

    /// <summary>Whether any attack sequence is currently active</summary>
    private bool _isActive;
    /// <summary>Damage multiplier applied to all attacks (from buffs/debuffs)</summary>
    private float _damageMul = 1f;
    /// <summary>Attack speed multiplier applied to animations</summary>
    private float _speedBuff = 1f;
    /// <summary>Whether the finisher attack is currently playing</summary>
    private bool _finisherMode;

    /// <summary>Cancellation token for async attack tasks</summary>
    private CancellationTokenSource _cts;

    /// <summary>Gate to ensure only one air attack per airtime session</summary>
    private bool _airAttackUsedThisAirtime;

    /// <summary>Whether the active combo is currently playing</summary>
    public bool IsComboActive => _isActive;
    /// <summary>Whether a dash attack is currently executing</summary>
    public bool IsDashAttackActive => _dashAttackActive;

    /// <summary>Initializes component references from the player hierarchy</summary>
    private void Awake()
    {
        _motor = _motor ?? GetComponent<PlayerMotor>();
        _playerAnim = _playerAnim ?? GetComponentInChildren<PlayerAnimator>();
        _weaponHitbox = _weaponHitbox ?? GetComponentInChildren<WeaponHitbox>();
    }

    /// <summary>
    /// Input callback for attack command from input system.
    /// Only responds to started input events.
    /// </summary>
    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        RequestAttack();
    }

    /// <summary>
    /// Processes an attack request and initiates the appropriate attack type.
    /// Handles buffering, dash attack chaining, and distinguishes between air/ground attacks.
    /// </summary>
    public void RequestAttack()
    {
        if (_motor == null || _playerAnim == null) return;

        // If dash attack is playing, allow "chain into ground combo" buffer
        if (_dashAttackActive)
        {
            _dashAttackChainBuffered = true;
            return;
        }

        // If something is already active:
        if (_isActive)
        {
            // IMPORTANT: air attack should NOT loop, so ignore buffering while air-attack mode
            if (_canBuffer && !_airAttackMode)
                _bufferedAttack = true;

            return;
        }

        bool airborne = !_motor.IsGrounded;

        // ---------- AIR ATTACK (single) ----------
        if (airborne)
        {
            if (_airAttackOncePerAirtime && _airAttackUsedThisAirtime)
                return;

            _airAttackUsedThisAirtime = true;

            StartAirAttackAsync().Forget();
            return;
        }

        // ---------- GROUND COMBO ----------
        StartComboAsync().Forget();
    }

    /// <summary>Immediately cancels the current attack sequence</summary>
    public void CancelCombo()
    {
        _cts?.Cancel();
        _weaponHitbox?.DisableHitbox();
    }

    /// <summary>Sets the damage multiplier for all subsequent attacks</summary>
    public void SetDamageMultiplier(float m) => _damageMul = m;

    /// <summary>Sets the attack speed multiplier for animations</summary>
    public void SetAttackSpeedBuff(float b) => _speedBuff = b;

    /// <summary>
    /// Called when the attack animation opens its hitbox window.
    /// Enables the appropriate hitbox based on current attack mode and applies multipliers.
    /// </summary>
    public void OnOpenComboWindow()
    {
        _canBuffer = true;

        // 0) FINISHER FIRST (AOE hitbox)
        if (_finisherMode)
        {
            int dmg = Mathf.RoundToInt(_finisherDamage * _damageMul);
            float mom = _finisherMomentumGain * _damageMul;

            var hb = (_finisherHitbox != null) ? _finisherHitbox : _weaponHitbox;
            hb.EnableHitbox(dmg, mom, _finisherKnockback);
            return;
        }

        // 1) Dash attack
        if (_dashAttackMode)
        {
            int dmg = Mathf.RoundToInt(_dashAttackDamage * _damageMul);
            float mom = _dashAttackMomentum * _damageMul;
            _weaponHitbox.EnableHitbox(dmg, mom, _dashAttackKnockback);
            return;
        }

        // 2) Air attack
        if (_airAttackMode)
        {
            int dmg = Mathf.RoundToInt(_airAttackDamage * _damageMul);
            float mom = _airAttackMomentum * _damageMul;
            _weaponHitbox.EnableHitbox(dmg, mom, _airAttackKnockback);
            return;
        }

        // 3) Ground combo
        if (_comboSteps == null || _comboSteps.Count == 0) return;
        if (_comboIndex < 0 || _comboIndex >= _comboSteps.Count) return;

        var step = _comboSteps[_comboIndex];

        int finalDamage = Mathf.RoundToInt(step.damage * _damageMul);
        float finalMomentum = step.momentumGain * _damageMul;
        float finalKnockback = step.knockbackForce;

        _weaponHitbox.EnableHitbox(finalDamage, finalMomentum, finalKnockback);
    }

    /// <summary>
    /// Called when the attack animation closes its hitbox window.
    /// Disables all active hitboxes and finalizes buffering logic.
    /// </summary>
    public void OnCloseComboWindow()
    {
        _canBuffer = false;
        _weaponHitbox.DisableHitbox();
        if (_finisherHitbox != null) _finisherHitbox.DisableHitbox();
    }

    /// <summary>
    /// Computes the attack speed compensation factor based on turbo mode status.
    /// Returns 1.5x multiplier during turbo, 1x otherwise.
    /// </summary>
    private float ComputeTurboAttackComp()
    {
        float turboAttack = 1f;
        var turbo = TurboModeManager.Instance;
        if (turbo != null && turbo.IsActive)
        {
            // Use attack compensation (playerSpeedMult) only so attacks are 1.5x, not multiplied by slow-mo cancel.
            turboAttack = turbo.AttackComp;
        }
        return turboAttack;
    }

    // -----------------------
    // AIR ATTACK (single)
    // -----------------------
    /// <summary>
    /// Initiates a single air attack. Locks player control, applies root motion,
    /// and prevents multiple air attacks per airtime (if configured).
    /// </summary>
    private async UniTaskVoid StartAirAttackAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isActive = true;
        _airAttackMode = true;
        _comboIndex = 0;
        _bufferedAttack = false;

        // Lock control for the attack
        _motor.DisableInput();

        // Stop carry-over drift (keep Y)
        var rb = _motor.GetRigidbody();
        if (rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        _playerAnim.SetApplyRootMotion(true);

        try
        {
            float turboAttack = ComputeTurboAttackComp();
            _playerAnim.SetAttackSpeed(_airAttackSpeedMult * _speedBuff * turboAttack);

            // IMPORTANT:
            // We reuse TriggerAttack(0) (Attack1 trigger) but Animator chooses the AIR clip when IsGrounded=false.
            _playerAnim.TriggerAttack(0);

            await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token);
            await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token);

            // No chaining. Ignore bufferedAttack.
        }
        catch (OperationCanceledException) { }
        finally
        {
            _airAttackMode = false;

            _playerAnim.SetApplyRootMotion(false);
            RestoreAnimBaseline();

            _motor.EnableInput();
            _motor.ClearBufferedMovement();

            _isActive = false;
            _comboIndex = 0;
            _canBuffer = false;
            _bufferedAttack = false;
            _weaponHitbox?.DisableHitbox();
        }
    }

    // -----------------------
    // GROUND COMBO
    // -----------------------
    /// <summary>
    /// Starts the ground combo sequence. Loops through combo steps with input buffering
    /// between each attack. Triggers finisher if maximum momentum is reached after the last hit.
    /// </summary>
    private async UniTaskVoid StartComboAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isActive = true;
        _airAttackMode = false;
        _comboIndex = 0;

        _motor.DisableInput();

        var rb = _motor.GetRigidbody();
        if (rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        _playerAnim.SetApplyRootMotion(true);

        try
        {
            while (_comboSteps != null && _comboIndex < _comboSteps.Count)
            {
                var step = _comboSteps[_comboIndex];

                if (rb != null)
                    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

                float turboAttack = ComputeTurboAttackComp();

                _playerAnim.SetAttackSpeed(step.speedMultiplier * _speedBuff * turboAttack);
                _bufferedAttack = false;

                _playerAnim.TriggerAttack(_comboIndex);

                await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token);
                await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token);

                if (_bufferedAttack)
                {
                    bool isLastNormalHit = (_comboIndex >= _comboSteps.Count - 1);

                    // If player tries to chain after Attack3
                    if (isLastNormalHit)
                    {
                        if (HasMaxMomentum())
                        {
                            await PlayFinisherAsync(token); // 4th AOE hit
                        }
                        break; // combo ends after finisher attempt
                    }

                    _comboIndex++;
                    continue;
                }

                break;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _playerAnim.SetApplyRootMotion(false);
            RestoreAnimBaseline();

            _motor.EnableInput();
            _motor.ClearBufferedMovement();

            _isActive = false;
            _comboIndex = 0;
            _canBuffer = false;
            _bufferedAttack = false;
            _weaponHitbox?.DisableHitbox();
        }
    }

    /// <summary>
    /// Plays the finisher attack (4th hit AOE) when maximum momentum is reached.
    /// Waits for hitbox window before returning control.
    /// </summary>
    private async UniTask PlayFinisherAsync(CancellationToken token)
    {
        _finisherMode = true;

        // stop horizontal drift (keep Y)
        var rb = _motor.GetRigidbody();
        if (rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        float turboAttack = ComputeTurboAttackComp();
        _playerAnim.SetAttackSpeed(_finisherSpeedMult * _speedBuff * turboAttack);

        // Attack4 trigger
        _playerAnim.TriggerAttack(3);

        await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token);
        await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token);

        _finisherMode = false;

    }

    // -----------------------
    // Dash Attack
    // -----------------------
    /// <summary>
    /// Initiates a dash attack. Only executes if no other attack is active.
    /// Applies root motion and locks player input during the attack.
    /// </summary>
    public void StartDashAttack()
    {
        if (_isActive) return;
        if (_dashAttackActive) return;

        _dashAttackActive = true;
        _dashAttackMode = true;
        _dashAttackChainBuffered = false;

        _motor.DisableInput();

        _playerAnim.SetApplyRootMotion(true);
        float turboAttack = ComputeTurboAttackComp();
        _playerAnim.SetAttackSpeed(_dashAttackSpeedMult * _speedBuff * turboAttack);
        _playerAnim.TriggerDashAttack();
    }

    /// <summary>
    /// Called when the dash attack animation completes.
    /// Re-enables player control and processes any buffered attack input.
    /// </summary>
    public void OnDashAttackEnd()
    {
        _weaponHitbox.DisableHitbox();
        _playerAnim.SetApplyRootMotion(false);
        RestoreAnimBaseline();

        _motor.EnableInput();

        _dashAttackActive = false;
        _dashAttackMode = false;

        if (_dashAttackChainBuffered)
        {
            _dashAttackChainBuffered = false;
            RequestAttack();
        }
    }

    /// <summary>
    /// Restores animation speed to baseline state (accounting for turbo mode if active).
    /// </summary>
    private void RestoreAnimBaseline()
    {
        if (_playerAnim == null) return;

        var turbo = TurboModeManager.Instance;
        if (turbo != null && turbo.IsActive)
            _playerAnim.RestoreBaselineSpeed();   // 1.1 during turbo
        else
            _playerAnim.SetAttackSpeed(1f);       // normal
    }

    /// <summary>Checks if the player currently has maximum momentum level</summary>
    private bool HasMaxMomentum()
    {
        var mm = MomentumManager.Instance;
        return _enableMaxFinisher && mm != null && mm.CurrentState == MomentumState.Max;
    }

    /// <summary>Resets the per-airtime air attack gate when player returns to ground</summary>
    private void Update()
    {
        // reset once-per-airtime gate when grounded again
        if (_motor != null && _motor.IsGrounded)
            _airAttackUsedThisAirtime = false;
    }
}
