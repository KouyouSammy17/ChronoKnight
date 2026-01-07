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

    [Header("Combo Definition")]
    [SerializeField] private List<ComboStep> _comboSteps = new List<ComboStep>();

    [Header("References (auto-assigned)")]
    [SerializeField] private PlayerMotor _motor;
    [SerializeField] private PlayerAnimator _playerAnim;
    [SerializeField] private WeaponHitbox _weaponHitbox;

    private int _comboIndex;
    private bool _canBuffer;
    private bool _bufferedAttack;
    private bool _isActive;
    private float _damageMul = 1f;
    private float _speedBuff = 1f;
    private CancellationTokenSource _cts;

    public bool IsComboActive => _isActive;

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
        if (!_isActive)
            StartComboAsync().Forget();
        else if (_canBuffer)
            _bufferedAttack = true;
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
    }

    private async UniTaskVoid StartComboAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isActive = true;
        _comboIndex = 0;

        // Lock control for the whole combo
        _motor.DisableInput();

        // Stop carry-over drift
        var rb = _motor.GetRigidbody();
        if (rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        _playerAnim.SetApplyRootMotion(true);

        try
        {
            while (_comboIndex < _comboSteps.Count)
            {
                var step = _comboSteps[_comboIndex];

                // optional: stop drift at each swing start
                if (rb != null)
                    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

                _playerAnim.SetAttackSpeed(step.speedMultiplier * _speedBuff);
                _bufferedAttack = false;

                _playerAnim.TriggerAttack(_comboIndex);

                await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token);
                await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token);

                // IMPORTANT: don't enable input, don't apply buffered movement mid-combo
                if (_bufferedAttack)
                {
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
            _playerAnim.SetAttackSpeed(1f);

            // Unlock only at the end
            _motor.EnableInput();

            // Let locomotion states handle move input normally next frame
            _motor.ClearBufferedMovement();

            _isActive = false;
            _comboIndex = 0;
            _canBuffer = false;
            _weaponHitbox?.DisableHitbox();
        }
    }

}
