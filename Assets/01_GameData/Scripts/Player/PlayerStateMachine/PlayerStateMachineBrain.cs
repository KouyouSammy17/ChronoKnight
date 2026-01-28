using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;

[DisallowMultipleComponent]
public class PlayerStateMachineBrain : MonoBehaviour
{
    [Header("Auto References (leave empty to auto-find)")]
    [SerializeField] private PlayerMotor _motor;
    [SerializeField] private PlayerInputRouter _input;
    [SerializeField] private CombatController _combat;
    [SerializeField] private PlayerDamageReceiver _damage;
    [SerializeField] private PlayerStats _stats;
    [SerializeField] private PlayerAnimator _anim;
   

    [Header("Debug")]
    [SerializeField] private PlayerStateID _current = PlayerStateID.Grounded;
    [SerializeField] private PlayerModeID _modeCurrent = PlayerModeID.Normal;

    // FEEL
    public MMStateMachine<PlayerStateID> MovementState { get; private set; }
    public MMStateMachine<PlayerModeID> ModeState { get; private set; }

    // accessors
    public PlayerMotor Motor => _motor;
    public PlayerAnimator Anim => _anim;
    public PlayerInputRouter Input => _input;
    public CombatController Combat => _combat;
    public PlayerDamageReceiver Damage => _damage;
    public PlayerStats Stats => _stats;

    public PlayerModeID CurrentMode => _modeCurrent;

    // movement states
    private readonly Dictionary<PlayerStateID, IPlayerState> _states = new();
    private IPlayerState _active;

    // mode states
    private readonly Dictionary<PlayerModeID, IPlayerModeState> _modeStates = new();
    private IPlayerModeState _modeActive;

    private void Awake()
    {
        _motor = _motor ?? GetComponent<PlayerMotor>();
        _input = _input ?? GetComponent<PlayerInputRouter>();
        _combat = _combat ?? GetComponent<CombatController>();
        _damage = _damage ?? GetComponent<PlayerDamageReceiver>();
        _stats = _stats ?? GetComponent<PlayerStats>();
        _anim = _anim ?? GetComponentInChildren<PlayerAnimator>();

        MovementState = new MMStateMachine<PlayerStateID>(this.gameObject, true);
        ModeState = new MMStateMachine<PlayerModeID>(this.gameObject, true);

        // Movement
        Register(new PlayerState_Grounded());
        Register(new PlayerState_Airborne());
        Register(new PlayerState_WallSlide());
        Register(new PlayerState_Dash());
        Register(new PlayerState_Attack());
        Register(new PlayerState_Hurt());
        Register(new PlayerState_Dead());
        Register(new PlayerState_Knockdown());
        Register(new PlayerState_DashAttack());
        Register(new PlayerState_TurboStart());
        Register(new PlayerState_Dead());
        Register(new PlayerState_Win());


        // Mode
        RegisterMode(new PlayerMode_Normal());
        RegisterMode(new PlayerMode_Turbo());
    }

    private void Start()
    {
        ChangeState(_motor != null && _motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne, true);

        // Initial mode mirror
        bool turboActive = TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;
        ChangeMode(turboActive ? PlayerModeID.Turbo : PlayerModeID.Normal, true);
    }

    private void Update()
    {
        // ─────────────────────────────────────────────
        // 1) Turbo input -> start Turbo via TurboModeManager
        // ─────────────────────────────────────────────
        if (Input != null && Input.ConsumeTurboPressed())
        {
            var turbo = TurboModeManager.Instance;
            if (turbo != null && turbo.CanStartTurbo())
            {
                ChangeState(PlayerStateID.TurboStart);
            }
            // else: ignore input (or play a “cooldown” SFX/UI flash)
        }


        // ─────────────────────────────────────────────
        // 2) Mirror runtime Turbo -> ModeState (Normal/Turbo)
        // ─────────────────────────────────────────────
        bool turboNow = TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;
        ChangeMode(turboNow ? PlayerModeID.Turbo : PlayerModeID.Normal);

        // optional mode tick (UI/VFX hooks live here)
        _modeActive?.Tick(this);

        // ─────────────────────────────────────────────
        // 3) Movement state tick + transitions
        // ─────────────────────────────────────────────
        _active?.Tick(this);
        EvaluateTransitions();
    }

    private void FixedUpdate()
    {
        _active?.FixedTick(this);
    }

    // ─────────────────────────────────────────────
    // Movement state machine
    // ─────────────────────────────────────────────
    private void Register(IPlayerState state) => _states[state.ID] = state;

    public void ChangeState(PlayerStateID next, bool force = false)
    {
        if (!force && _current == next) return;

        _active?.Exit(this);

        _current = next;
        MovementState.ChangeState(next);

        _active = _states.TryGetValue(next, out var s) ? s : null;
        _active?.Enter(this);
    }

    private void EvaluateTransitions()
    {
        if (_current == PlayerStateID.Win) return;

        // DEAD overrides everything — also stop turbo so timeScale resets
        if (_stats != null && _stats.CurrentHP <= 0)
        {
            if (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
                TurboModeManager.Instance.StopTurbo();

            ChangeMode(PlayerModeID.Normal);

            if (_current != PlayerStateID.Dead) ChangeState(PlayerStateID.Dead);
            return;
        }

        if (_damage != null && _damage.IsInHitStun)
        {
            if (_current != PlayerStateID.Hurt) ChangeState(PlayerStateID.Hurt);
            return;
        }

        if (_combat != null && _combat.IsComboActive)
        {
            if (_current != PlayerStateID.Attack) ChangeState(PlayerStateID.Attack);
            return;
        }

        // Dash ends -> fall back
        if (_current == PlayerStateID.Dash && _motor != null && !_motor.IsDashing)
        {
            ChangeState(_motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne);
            return;
        }
        
        // Dash attack: if player attacks during dash, go to DashAttack
        if (_motor != null && _motor.IsDashing && _input != null && _input.ConsumeAttackPressed())
        {
            ChangeState(PlayerStateID.DashAttack);
            return;
        }
       
        if (_damage != null && _damage.IsKnockedDown)
        {
            if (_current != PlayerStateID.Knockdown) ChangeState(PlayerStateID.Knockdown);
            return;
        }

        // Attack/Hurt end -> fall back
        if ((_current == PlayerStateID.Attack && (_combat == null || !_combat.IsComboActive)) ||
            (_current == PlayerStateID.Hurt && (_damage == null || !_damage.IsInHitStun)))
        {
            ChangeState(_motor != null && _motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne);
            return;
        }

        // WallSlide logic
        if (_motor != null)
        {
            if (_current == PlayerStateID.Airborne && _motor.ShouldWallSlide)
            {
                ChangeState(PlayerStateID.WallSlide);
                return;
            }

            if (_current == PlayerStateID.WallSlide && !_motor.ShouldWallSlide)
            {
                ChangeState(_motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne);
                return;
            }
        }

        // Grounded/Airborne swap
        if (_motor != null)
        {
            if (_current == PlayerStateID.Grounded && !_motor.IsGrounded)
                ChangeState(PlayerStateID.Airborne);
            else if (_current == PlayerStateID.Airborne && _motor.IsGrounded)
                ChangeState(PlayerStateID.Grounded);
        }
    }

    // ─────────────────────────────────────────────
    // Mode state machine (Normal/Turbo)
    // ─────────────────────────────────────────────
    private void RegisterMode(IPlayerModeState state) => _modeStates[state.ID] = state;

    public void ChangeMode(PlayerModeID next, bool force = false)
    {
        if (!force && _modeCurrent == next) return;

        _modeActive?.Exit(this);

        _modeCurrent = next;
        ModeState.ChangeState(next);

        _modeActive = _modeStates.TryGetValue(next, out var s) ? s : null;
        _modeActive?.Enter(this);
    }

    public void ResetAfterRespawn(bool forceSnapYawRight = true)
    {
        // 0) Stop turbo and normalize mode (so DT / fixedDT go back to normal)
        if (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
            TurboModeManager.Instance.StopTurbo();
        ChangeMode(PlayerModeID.Normal, true);

        // 1) Clear stateful gameplay systems
        Combat?.CancelCombo();

        // 2) Clear buffered inputs so we don't "auto attack / auto jump" after respawn
        Input?.ClearPressedBuffers();
        Motor?.ClearBufferedMovement();

        // 3) Reset motor runtime flags (dash, wall jump locks, etc.)
        // Use OnRespawnSnap if you're already teleporting + SyncTransforms in GameManager
        Motor?.OnRespawnSnap(); // keeps jump buffer behavior you designed
        Motor?.SetHitReactLock(false);
        Motor?.SetAirComboHang(false);
        Motor?.SetFrozen(false);
        Motor?.CancelDash();
        Motor?.StopHorizontalInstant();
        GetComponent<PlayerDamageReceiver>()?.CancelForRespawn();

        // 4) Reset animator flags (hurt/down/rootmotion/attack speed/etc.)
        Anim?.ResetForRespawn();

        // 5) Force the movement state back to grounded
        ChangeState(PlayerStateID.Grounded, true);

        // Optional: force facing yaw to a known default on respawn
        if (forceSnapYawRight)
            Motor?.ForceFacingYaw(90f, snap: true);
    }
}
