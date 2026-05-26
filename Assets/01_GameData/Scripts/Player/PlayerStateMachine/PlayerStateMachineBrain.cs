// プレイヤーのステートマシン全体を管理し、移動ステートとモードステートを制御するスクリプト
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;

[DisallowMultipleComponent]
public class PlayerStateMachineBrain : MonoBehaviour
{
    [Header("Auto References (leave empty to auto-find)")]
    [SerializeField] private PlayerMotor _motor; // プレイヤーのモーターコンポーネント
    [SerializeField] private PlayerInputRouter _input; // 入力ルーターコンポーネント
    [SerializeField] private CombatController _combat; // 戦闘コントローラー
    [SerializeField] private PlayerDamageReceiver _damage; // ダメージレシーバー
    [SerializeField] private PlayerStats _stats; // ステータス管理
    [SerializeField] private PlayerAnimator _anim; // アニメーター


    [Header("Debug")]
    [SerializeField] private PlayerStateID _current = PlayerStateID.Grounded; // 現在の移動ステートID（デバッグ表示用）
    [SerializeField] private PlayerModeID _modeCurrent = PlayerModeID.Normal; // 現在のモードID（デバッグ表示用）

    // FEEL
    public MMStateMachine<PlayerStateID> MovementState { get; private set; } // MoreMountains製の移動ステートマシン
    public MMStateMachine<PlayerModeID> ModeState { get; private set; } // MoreMountains製のモードステートマシン

    // accessors
    public PlayerMotor Motor => _motor; // モーターへのアクセサ
    public PlayerAnimator Anim => _anim; // アニメーターへのアクセサ
    public PlayerInputRouter Input => _input; // 入力ルーターへのアクセサ
    public CombatController Combat => _combat; // 戦闘コントローラーへのアクセサ
    public PlayerDamageReceiver Damage => _damage; // ダメージレシーバーへのアクセサ
    public PlayerStats Stats => _stats; // ステータスへのアクセサ

    public PlayerModeID CurrentMode => _modeCurrent; // 現在のモードIDを外部から参照

    // movement states
    private readonly Dictionary<PlayerStateID, IPlayerState> _states = new(); // 移動ステートの辞書（IDでアクセス）
    private IPlayerState _active; // 現在アクティブな移動ステート

    // mode states
    private readonly Dictionary<PlayerModeID, IPlayerModeState> _modeStates = new(); // モードステートの辞書
    private IPlayerModeState _modeActive; // 現在アクティブなモードステート

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
                ChangeState(PlayerStateID.TurboStart); // ターボ入力を受け付けたらTurboStartステートへ
            }
            // else: ignore input (or play a “cooldown” SFX/UI flash)
        }


        // ─────────────────────────────────────────────
        // 2) Mirror runtime Turbo -> ModeState (Normal/Turbo)
        // ─────────────────────────────────────────────
        bool turboNow = TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;
        ChangeMode(turboNow ? PlayerModeID.Turbo : PlayerModeID.Normal); // ターボ状態に合わせてモードを切り替える

        // optional mode tick (UI/VFX hooks live here)
        _modeActive?.Tick(this); // 現在のモードのTickを実行

        // ─────────────────────────────────────────────
        // 3) Movement state tick + transitions
        // ─────────────────────────────────────────────
        _active?.Tick(this); // 現在の移動ステートのTickを実行
        EvaluateTransitions(); // ステート遷移の条件を評価
        Debug.Log(_motor.MoveSpeed);
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
        if (_current == PlayerStateID.Win) return; // 勝利状態はステート遷移を行わない

        // DEAD overrides everything — also stop turbo so timeScale resets
        if (_stats != null && _stats.CurrentHP <= 0)
        {
            if (TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive)
                TurboModeManager.Instance.StopTurbo(); // 死亡時にターボを強制停止してTimeScaleをリセット

            ChangeMode(PlayerModeID.Normal); // モードをノーマルに戻す

            if (_current != PlayerStateID.Dead) ChangeState(PlayerStateID.Dead); // 死亡ステートへ遷移
            return;
        }

        if (_damage != null && _damage.IsInHitStun)
        {
            if (_current != PlayerStateID.Hurt) ChangeState(PlayerStateID.Hurt); // ヒットスタン中はHurtステートへ
            return;
        }

        if (_combat != null && _combat.IsComboActive)
        {
            if (_current != PlayerStateID.Attack) ChangeState(PlayerStateID.Attack); // コンボ実行中はAttackステートへ
            return;
        }

        // Dash ends -> fall back
        if (_current == PlayerStateID.Dash && _motor != null && !_motor.IsDashing)
        {
            ChangeState(_motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne); // ダッシュ終了後は接地状態で判断
            return;
        }

        // Dash attack: if player attacks during dash, go to DashAttack
        if (_motor != null && _motor.IsDashing && _input != null && _input.ConsumeAttackPressed())
        {
            ChangeState(PlayerStateID.DashAttack); // ダッシュ中に攻撃入力でDashAttackステートへ
            return;
        }

        if (_damage != null && _damage.IsKnockedDown)
        {
            if (_current != PlayerStateID.Knockdown) ChangeState(PlayerStateID.Knockdown); // ノックダウン中はKnockdownステートへ
            return;
        }

        // Attack/Hurt end -> fall back
        if ((_current == PlayerStateID.Attack && (_combat == null || !_combat.IsComboActive)) ||
            (_current == PlayerStateID.Hurt && (_damage == null || !_damage.IsInHitStun)))
        {
            ChangeState(_motor != null && _motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne); // 攻撃・ヒット終了後に移動ステートへ戻る
            return;
        }

        // WallSlide logic
        if (_motor != null)
        {
            if (_current == PlayerStateID.Airborne && _motor.ShouldWallSlide)
            {
                ChangeState(PlayerStateID.WallSlide); // 空中で壁スライド条件を満たしたらWallSlideへ
                return;
            }

            if (_current == PlayerStateID.WallSlide && !_motor.ShouldWallSlide)
            {
                ChangeState(_motor.IsGrounded ? PlayerStateID.Grounded : PlayerStateID.Airborne); // 壁から離れたら接地状態で判断
                return;
            }
        }

        // Grounded/Airborne swap
        if (_motor != null)
        {
            if (_current == PlayerStateID.Grounded && !_motor.IsGrounded)
                ChangeState(PlayerStateID.Airborne); // 離陸したらAirborneへ
            else if (_current == PlayerStateID.Airborne && _motor.IsGrounded)
                ChangeState(PlayerStateID.Grounded); // 着地したらGroundedへ
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
            TurboModeManager.Instance.StopTurbo(); // ターボを停止してTimeScaleを正常に戻す
        ChangeMode(PlayerModeID.Normal, true); // モードを強制的にノーマルに戻す

        // 1) Clear stateful gameplay systems
        Combat?.CancelCombo(); // 進行中のコンボをキャンセル

        // 2) Clear buffered inputs so we don't "auto attack / auto jump" after respawn
        Input?.ClearPressedBuffers(); // リスポーン後に自動攻撃・ジャンプが起きないようにバッファをクリア
        Motor?.ClearBufferedMovement(); // バッファされた移動入力もクリア

        // 3) Reset motor runtime flags (dash, wall jump locks, etc.)
        // Use OnRespawnSnap if you're already teleporting + SyncTransforms in GameManager
        Motor?.OnRespawnSnap(); // keeps jump buffer behavior you designed テレポート後の物理状態をリセット
        Motor?.SetHitReactLock(false); // ヒットリアクションロックを解除
        Motor?.SetAirComboHang(false); // 空中コンボハングを解除
        Motor?.SetFrozen(false); // フリーズを解除
        Motor?.CancelDash(); // ダッシュをキャンセル
        Motor?.StopHorizontalInstant(); // 水平速度を即時停止
        GetComponent<PlayerDamageReceiver>()?.CancelForRespawn(); // 保留中のヒットリアクション処理をキャンセル

        // 4) Reset animator flags (hurt/down/rootmotion/attack speed/etc.)
        Anim?.ResetForRespawn(); // アニメーター状態をリスポーン用にリセット

        // 5) Force the movement state back to grounded
        ChangeState(PlayerStateID.Grounded, true); // 移動ステートを強制的にGroundedに戻す

        // Optional: force facing yaw to a known default on respawn
        if (forceSnapYawRight)
            Motor?.ForceFacingYaw(90f, snap: true); // リスポーン時に右向き（90度）にスナップ
    }
}
