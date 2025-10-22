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

    // === NEW: attack-start buffer ===
    [Header("Attack Start Buffer")]
    [SerializeField] private float _attackStartBufferTime = 0.2f;  // seconds
    private bool _attackStartBuffered;
    private CancellationTokenSource _attackStartCts;

    private void Awake()
    {
        _playerController = _playerController ?? GetComponent<PlayerController>();
        _playerAnim = _playerAnim ?? GetComponentInChildren<PlayerAnimator>();
        _weaponHitbox = _weaponHitbox ?? GetComponentInChildren<WeaponHitbox>();
    }

    private void OnDisable()
    {
        _attackStartCts?.Cancel();
        _cts?.Cancel();
    }

    // Public API
    public bool IsComboActive => _isActive;
    public void SetDamageMultiplier(float m) => _damageMul = m;
    public void SetAttackSpeedBuff(float b) => _speedBuff = b;
    public float AttackSpeedBuff => _speedBuff;

    // Animator events
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

    // Input
    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;

        // If a combo is already playing, allow buffering regardless of movement input lock
        if (_isActive)
        {
            if (_canBuffer) _bufferedAttack = true;
            return;
        }

        // Not in a combo:
        // If input is locked (e.g., hit-stun or attack root-motion), buffer a start-attack
        if (_playerController != null && !_playerController.InputEnabled)
        {
            BufferAttackStart();
            return;
        }

        // Normal start
        StartComboAsync().Forget();

        if (!TutorialProgress.IsLearned(TutorialKey.Attack))
        {
            UIManager.Instance?.TutorialSuccess(TutorialKey.Attack);
            TutorialProgress.SetLearned(TutorialKey.Attack);
        }
    }

    // Request an attack programmatically (optional external call)
    public void RequestAttack()
    {
        if (!_isActive) StartComboAsync().Forget();
        else if (_canBuffer) _bufferedAttack = true;
    }
    public void CancelCombo()
    {
        _cts?.Cancel();
    }

    // === NEW: attack-start buffering logic ===
    private void BufferAttackStart()
    {
        _attackStartBuffered = true;

        // restart watcher task
        _attackStartCts?.Cancel();
        _attackStartCts = new CancellationTokenSource();
        WatchAttackStartBuffer(_attackStartCts.Token).Forget();
    }

    private async UniTaskVoid WatchAttackStartBuffer(CancellationToken token)
    {
        float deadline = Time.time + _attackStartBufferTime;

        // Wait until either input is enabled or the timer expires
        while (Time.time < deadline && !_playerController.InputEnabled)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        if (token.IsCancellationRequested) return;

        if (_attackStartBuffered && _playerController.InputEnabled)
        {
            _attackStartBuffered = false;
            StartComboAsync().Forget();
        }
        else
        {
            // expired or still locked beyond buffer window
            _attackStartBuffered = false;
        }
    }

    // Core combo flow
    private async UniTaskVoid StartComboAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isActive = true;
        _comboIndex = 0;

        // --- 1) at the very start: block movement, but keep buffering movement ---
        _playerController.DisableInput();
        _playerController.GetRigidbody().linearVelocity = Vector3.zero;
        _playerController.PreloadMovementBufferFromHold();
        _playerAnim.SetApplyRootMotion(true);

        try
        {
            while (_comboIndex < _comboSteps.Count)
            {
                var step = _comboSteps[_comboIndex];

                // Zero horizontal drift each step start
                _playerController.GetRigidbody().linearVelocity = Vector3.zero;

                // Decide final attack speed (Turbo overrides other buffs if you do that)
                float finalBuff = _speedBuff;
                var turboMgr = TurboModeManager.Instance;
                if (turboMgr != null && turboMgr.IsActive)
                    finalBuff = turboMgr.TurboComp;

                _playerAnim.SetAttackSpeed(step.speedMultiplier * finalBuff);

                _bufferedAttack = false;
                _playerAnim.TriggerAttack(_comboIndex);

                // Wait for animator events opening/closing the hit window
                await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token);
                await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token);

                // Restore control between swings
                _playerAnim.SetApplyRootMotion(false);
                _playerController.EnableInput();

                // Let input system tick one frame so holds/releases hit our buffer
                await UniTask.Yield();

                // Flush truly buffered movement
                Vector2 moveBuf = _playerController.GetBufferedMovement();
                if (moveBuf.sqrMagnitude > 0.01f)
                    _playerController.ApplyBufferedMovement(moveBuf);
                _playerController.ClearBufferedMovement();

                if (_bufferedAttack)
                {
                    // Next swing: lock movement again & continue
                    _playerController.DisableInput();
                    _playerAnim.SetApplyRootMotion(true);
                    _comboIndex++;
                    continue;
                }
                else break;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // Reset animator & input
            _playerAnim.SetApplyRootMotion(false);
            _playerAnim.SetAttackSpeed(1f);
            _playerController.EnableInput();

            await UniTask.Yield();

            Vector2 finalMove = _playerController.GetBufferedMovement();
            if (finalMove.sqrMagnitude > 0.01f)
                _playerController.ApplyBufferedMovement(finalMove);
            _playerController.ClearBufferedMovement();

            if (_playerController.IsHoldingMove && _playerController.GetLastMoveInput().sqrMagnitude > 0.01f)
                _playerController.ApplyBufferedMovement(_playerController.GetLastMoveInput());

            _isActive = false;
            _comboIndex = 0;
            _canBuffer = false;
        }
    }
}
