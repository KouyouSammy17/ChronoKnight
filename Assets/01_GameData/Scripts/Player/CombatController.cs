using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

[RequireComponent(typeof(Collider))]
public class CombatController : MonoBehaviour
{
    [Serializable]
    public struct ComboStep
    {
        public string stepName;
        public float inputWindow;
        public float speedMultiplier;
        public int damage;
        public float momentumGain;
        public float knockbackForce;
    }

    [Header("Ground Combo Definition")]
    [SerializeField] private List<ComboStep> _comboSteps = new List<ComboStep>();

    [Header("Air Attack (Single)")]
    [SerializeField] private float _airAttackSpeedMult = 1.0f;
    [SerializeField] private int _airAttackDamage = 15;
    [SerializeField] private float _airAttackMomentum = 8f;
    [SerializeField] private float _airAttackKnockback = 8f;

    [Tooltip("If ON, you can only do 1 air attack per airtime (resets on landing).")]
    [SerializeField] private bool _airAttackOncePerAirtime = true;

    [Header("References (auto-assigned)")]
    [SerializeField] private PlayerMotor _motor;
    [SerializeField] private PlayerAnimator _playerAnim;
    [SerializeField] private WeaponHitbox _weaponHitbox;

    [Header("Dash Attack")]
    [SerializeField] private float _dashAttackSpeedMult = 1.0f;
    [SerializeField] private int _dashAttackDamage = 20;
    [SerializeField] private float _dashAttackMomentum = 10f;
    [SerializeField] private float _dashAttackKnockback = 10f;

    [Header("Max Momentum 4th Hit (AOE Finisher)")]
    [SerializeField] private bool _enableMaxFinisher = true;
    [SerializeField] private float _finisherSpeedMult = 1.0f;
    [SerializeField] private int _finisherDamage = 45;
    [SerializeField] private float _finisherMomentumGain = 0f;
    [SerializeField] private float _finisherKnockback = 14f;
    [SerializeField] private WeaponHitbox _finisherHitbox; // assign AOEHitbox WeaponHitbox

    // runtime
    private int _comboIndex;
    private bool _canBuffer;
    private bool _bufferedAttack;

    private bool _dashAttackActive;
    private bool _dashAttackChainBuffered;
    private bool _dashAttackMode;

    private bool _airAttackMode;

    private bool _isActive;
    private float _damageMul = 1f;
    private float _speedBuff = 1f;
    private bool _finisherMode;

    //[Header("Turbo")]
    //[SerializeField] private float _turboAttackMultiplier = 1.5f; // real-time attack speed bonus during Turbo

    private CancellationTokenSource _cts;

    // once-per-airtime gate (local, resets when grounded)
    private bool _airAttackUsedThisAirtime;

    public bool IsComboActive => _isActive;
    public bool IsDashAttackActive => _dashAttackActive;

    private void Awake()
    {
        _motor = _motor ?? GetComponent<PlayerMotor>();
        _playerAnim = _playerAnim ?? GetComponentInChildren<PlayerAnimator>();
        _weaponHitbox = _weaponHitbox ?? GetComponentInChildren<WeaponHitbox>();
    }

    // Optional: keep input callback compatibility
    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        RequestAttack();
    }

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

    public void CancelCombo()
    {
        _cts?.Cancel();
        _weaponHitbox?.DisableHitbox();
    }

    public void SetDamageMultiplier(float m) => _damageMul = m;
    public void SetAttackSpeedBuff(float b) => _speedBuff = b;

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


    public void OnCloseComboWindow()
    {
        _canBuffer = false;
        _weaponHitbox.DisableHitbox();
        if (_finisherHitbox != null) _finisherHitbox.DisableHitbox();
    }

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
    // Dash Attack (unchanged)
    // -----------------------
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

    private void RestoreAnimBaseline()
    {
        if (_playerAnim == null) return;

        var turbo = TurboModeManager.Instance;
        if (turbo != null && turbo.IsActive)
            _playerAnim.RestoreBaselineSpeed();   // 1.1 during turbo
        else
            _playerAnim.SetAttackSpeed(1f);       // normal
    }
    private bool HasMaxMomentum()
    {
        var mm = MomentumManager.Instance;
        return _enableMaxFinisher && mm != null && mm.CurrentState == MomentumState.Max;
    }

    private void Update()
    {
        // reset once-per-airtime gate when grounded again
        if (_motor != null && _motor.IsGrounded)
            _airAttackUsedThisAirtime = false;
    }
}
