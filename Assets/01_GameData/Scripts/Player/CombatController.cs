using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Handles player combat combos. This is a copy of the upstream CombatController
/// from the ChronoKnight project. It exposes attack speed and damage multipliers
/// and controls combo timing, buffering, and hitbox activation. Attack speed
/// can be modified via SetAttackSpeedBuff, which is now driven by
/// CombatTurboManager when Turbo Mode is active.
/// </summary>
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
    }

    [Header("Combo Definition")]
    [SerializeField] private List<ComboStep> _comboSteps = new List<ComboStep>();

    [Header("References (auto-assigned)")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerAnimator _playerAnim;
    [SerializeField] private WeaponHitbox _weaponHitbox;

    // runtime
    private int _comboIndex;
    private bool _canBuffer;
    private bool _bufferedAttack;
    private bool _isActive;
    private float _damageMul = 1f;
    private float _speedBuff = 1f;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        _playerController = _playerController ?? GetComponent<PlayerController>();
        _playerAnim = _playerAnim ?? GetComponentInChildren<PlayerAnimator>();
        _weaponHitbox = _weaponHitbox ?? GetComponentInChildren<WeaponHitbox>();
    }

    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;

        if (!_isActive)
            StartComboAsync().Forget();
        else if (_canBuffer)
            _bufferedAttack = true;
    }

    /// <summary>
    /// True when a combat combo is currently active. Exposed so other systems
    /// (e.g., CombatTurboManager) can detect whether an attack is playing and
    /// avoid overriding Animator speed during combos.
    /// </summary>
    public bool IsComboActive => _isActive;

    /// <summary>
    /// Set a global damage multiplier for all combo steps.
    /// </summary>
    public void SetDamageMultiplier(float m) => _damageMul = m;

    /// <summary>
    /// Set a speed buff multiplier for attack animations. This is multiplied with
    /// each ComboStep.speedMultiplier in StartComboAsync. Turbo Mode uses this
    /// to speed up combos.
    /// </summary>
    public void SetAttackSpeedBuff(float b) => _speedBuff = b;

    /// <summary>
    /// Current attack speed buff applied via SetAttackSpeedBuff (e.g. from momentum buffs).
    /// Turbo buffs should multiply this rather than overwrite it.
    /// </summary>
    public float AttackSpeedBuff => _speedBuff;

    // called from Animator events:
    public void OnOpenComboWindow()
    {
        _canBuffer = true;
        var step = _comboSteps[_comboIndex];

        int finalDamage = Mathf.RoundToInt(step.damage * _damageMul);
        float finalMomentum = step.momentumGain * _damageMul;

        _weaponHitbox.EnableHitbox(finalDamage, finalMomentum);
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

        // --- 1) at the very start: block movement, but keep buffering ---
        _playerController.DisableInput();
        // Zero out velocity to prevent sliding
        _playerController.GetRigidbody().linearVelocity = Vector3.zero;
        // if we *were* holding a direction when the attack started,
        // seed the buffer so it never lapses while we're locked out:
        _playerController.PreloadMovementBufferFromHold();
        _playerAnim.SetApplyRootMotion(true);

        try
        {
            while (_comboIndex < _comboSteps.Count)
            {
                var step = _comboSteps[_comboIndex];

                // Zero out velocity at the start of every combo step to prevent sliding
                _playerController.GetRigidbody().linearVelocity = Vector3.zero;

                // set anim speed
                // Determine which attack‑speed buff to apply.  We do not stack
                // momentum and Turbo buffs together; instead we choose the higher
                // priority buff: Turbo overrides momentum.  When Turbo is active
                // the attack speed is fixed at 1.5×.  Otherwise we use the current
                // momentum buff (stored in _speedBuff).  This prevents combining
                // Turbo and momentum buffs (e.g., 1.2 × 1.5 → 1.8) and instead
                // applies a single multiplier.
                float finalBuff;
                var turboMgr = TurboModeManager.Instance;
                if (turboMgr != null && turboMgr.IsActive)
                {
                    finalBuff = 1.5f;
                }
                else
                {
                    finalBuff = _speedBuff;
                }
                _playerAnim.SetAttackSpeed(step.speedMultiplier * finalBuff);

                // reset buffer flag
                _bufferedAttack = false;

                // fire the attack
                _playerAnim.TriggerAttack(_comboIndex);

                // wait for the “open combo window” → “close combo window” events
                await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token);
                await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token);

                // restore from root-motion mode back to player-driven movement
                _playerAnim.SetApplyRootMotion(false);
                _playerController.EnableInput();


                // wait one frame so any “hold/release” events actually hit the buffer
                await UniTask.Yield();

                // —– NOW flush only *actually buffered* movement —–
                Vector2 moveBuf = _playerController.GetBufferedMovement();
                if (moveBuf.sqrMagnitude > 0.01f)
                    _playerController.ApplyBufferedMovement(moveBuf);
                _playerController.ClearBufferedMovement();

                if (_bufferedAttack)
                {
                    // before next swing: lock out jump/dash again
                    _playerController.DisableInput();
                    _playerAnim.SetApplyRootMotion(true);

                    _comboIndex++;
                    continue;
                }
                else
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // 3) all done: reset animator & input
            _playerAnim.SetApplyRootMotion(false);
            _playerAnim.SetAttackSpeed(1f);

            // re-enable input so normal movement can resume
            _playerController.EnableInput();

            // wait one frame so any release of the stick/button clears the buffer
            await UniTask.Yield();

            // —– FINAL FLUSH: only what’s truly buffered —–
            Vector2 finalMove = _playerController.GetBufferedMovement();
            if (finalMove.sqrMagnitude > 0.01f)
                _playerController.ApplyBufferedMovement(finalMove);
            _playerController.ClearBufferedMovement();

            // If the player is holding a direction, immediately apply it for smooth movement resumption
            if (_playerController.IsHoldingMove && _playerController.GetLastMoveInput().sqrMagnitude > 0.01f)
                _playerController.ApplyBufferedMovement(_playerController.GetLastMoveInput());

            // stop buffering now that we’re back to idle

            _isActive = false;
            _comboIndex = 0;
            _canBuffer = false;
        }
    }
}