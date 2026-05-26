// プレイヤーの戦闘システム全体（コンボ・空中攻撃・ダッシュ攻撃・フィニッシャー）を管理するスクリプト
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
        public string stepName; // コンボステップの識別名
        /// <summary>Time window during which the next attack can be buffered</summary>
        public float inputWindow; // 次の攻撃をバッファできる時間ウィンドウ
        /// <summary>Animation speed multiplier for this attack</summary>
        public float speedMultiplier; // このステップのアニメーション速度倍率
        /// <summary>Base damage dealt by this attack</summary>
        public int damage; // この攻撃の基本ダメージ量
        /// <summary>Momentum points gained from hitting with this attack</summary>
        public float momentumGain; // 命中時に得られるモメンタムポイント
        /// <summary>Knockback force applied to enemies</summary>
        public float knockbackForce; // 敵に与えるノックバック力
    }

    // ==================== SERIALIZED CONFIGURATION ====================

    /// <summary>Sequence of attacks that form the ground combo chain</summary>
    [Header("Ground Combo Definition")]
    [SerializeField] private List<ComboStep> _comboSteps = new List<ComboStep>(); // 地上コンボのステップ定義リスト

    /// <summary>Speed and damage configuration for air attacks</summary>
    [Header("Air Attack (Single)")]
    [SerializeField] private float _airAttackSpeedMult = 1.0f; // 空中攻撃のアニメーション速度倍率
    [SerializeField] private int _airAttackDamage = 15; // 空中攻撃のダメージ量
    [SerializeField] private float _airAttackMomentum = 8f; // 空中攻撃命中時のモメンタム獲得量
    [SerializeField] private float _airAttackKnockback = 8f; // 空中攻撃のノックバック力

    /// <summary>If enabled, limits air attacks to once per airtime (resets when landing)</summary>
    [Tooltip("If ON, you can only do 1 air attack per airtime (resets on landing).")]
    [SerializeField] private bool _airAttackOncePerAirtime = true; // 空中攻撃を一度の滞空中に1回のみ許可するフラグ

    /// <summary>References to other player components (auto-assigned at runtime)</summary>
    [Header("References (auto-assigned)")]
    [SerializeField] private PlayerMotor _motor; // プレイヤーの移動コンポーネント
    [SerializeField] private PlayerAnimator _playerAnim; // プレイヤーのアニメーター
    [SerializeField] private WeaponHitbox _weaponHitbox; // 武器のヒットボックス

    /// <summary>Configuration for dash attack mechanics</summary>
    [Header("Dash Attack")]
    [SerializeField] private float _dashAttackSpeedMult = 1.0f; // ダッシュ攻撃のアニメーション速度倍率
    [SerializeField] private int _dashAttackDamage = 20; // ダッシュ攻撃のダメージ量
    [SerializeField] private float _dashAttackMomentum = 10f; // ダッシュ攻撃命中時のモメンタム獲得量
    [SerializeField] private float _dashAttackKnockback = 10f; // ダッシュ攻撃のノックバック力

    /// <summary>Settings for the maximum momentum AOE finisher attack (4th hit)</summary>
    [Header("Max Momentum 4th Hit (AOE Finisher)")]
    [SerializeField] private bool _enableMaxFinisher = true; // フィニッシャー攻撃を有効にするか
    [SerializeField] private float _finisherSpeedMult = 1.0f; // フィニッシャーのアニメーション速度倍率
    [SerializeField] private int _finisherDamage = 45; // フィニッシャーのダメージ量
    [SerializeField] private float _finisherMomentumGain = 0f; // フィニッシャー命中時のモメンタム獲得量（通常0）
    [SerializeField] private float _finisherKnockback = 14f; // フィニッシャーのノックバック力
    /// <summary>Separate hitbox for AOE finisher effect (if null, uses weapon hitbox)</summary>
    [SerializeField] private WeaponHitbox _finisherHitbox; // AOEフィニッシャー専用ヒットボックス（nullなら武器ヒットボックスを使用）

    // ==================== RUNTIME STATE ====================

    /// <summary>Current position in the combo sequence</summary>
    private int _comboIndex; // 現在のコンボステップのインデックス
    /// <summary>Whether the combo window is open for buffering next attack</summary>
    private bool _canBuffer; // 次の攻撃をバッファできる状態か
    /// <summary>Whether an attack input was buffered during combo window</summary>
    private bool _bufferedAttack; // コンボウィンドウ中に攻撃入力がバッファされたか

    /// <summary>Whether a dash attack is currently active</summary>
    private bool _dashAttackActive; // ダッシュ攻撃が実行中か
    /// <summary>Whether an attack was buffered while dash attack was playing</summary>
    private bool _dashAttackChainBuffered; // ダッシュ攻撃中に次の攻撃がバッファされたか
    /// <summary>Whether current attack mode is dash attack</summary>
    private bool _dashAttackMode; // 現在ダッシュ攻撃モードか

    /// <summary>Whether current attack mode is air attack</summary>
    private bool _airAttackMode; // 現在空中攻撃モードか

    /// <summary>Whether any attack sequence is currently active</summary>
    private bool _isActive; // 何らかの攻撃シーケンスが進行中か
    /// <summary>Damage multiplier applied to all attacks (from buffs/debuffs)</summary>
    private float _damageMul = 1f; // 全攻撃に適用するダメージ倍率
    /// <summary>Attack speed multiplier applied to animations</summary>
    private float _speedBuff = 1f; // 攻撃アニメーションの速度バフ倍率
    /// <summary>Whether the finisher attack is currently playing</summary>
    private bool _finisherMode; // フィニッシャー攻撃が実行中か

    /// <summary>Cancellation token for async attack tasks</summary>
    private CancellationTokenSource _cts; // 非同期攻撃処理のキャンセルトークン

    /// <summary>Gate to ensure only one air attack per airtime session</summary>
    private bool _airAttackUsedThisAirtime; // 今回の滞空で空中攻撃を使用済みか

    /// <summary>Whether the active combo is currently playing</summary>
    public bool IsComboActive => _isActive;
    /// <summary>Whether a dash attack is currently executing</summary>
    public bool IsDashAttackActive => _dashAttackActive;

    /// <summary>Initializes component references from the player hierarchy</summary>
    private void Awake()
    {
        _motor = _motor ?? GetComponent<PlayerMotor>(); // モーターを自動取得
        _playerAnim = _playerAnim ?? GetComponentInChildren<PlayerAnimator>(); // アニメーターを子から自動取得
        _weaponHitbox = _weaponHitbox ?? GetComponentInChildren<WeaponHitbox>(); // 武器ヒットボックスを子から自動取得
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
            _dashAttackChainBuffered = true; // ダッシュ攻撃中のコンボ連携をバッファ
            return;
        }

        // If something is already active:
        if (_isActive)
        {
            // IMPORTANT: air attack should NOT loop, so ignore buffering while air-attack mode
            if (_canBuffer && !_airAttackMode)
                _bufferedAttack = true; // ウィンドウ内なら次の攻撃をバッファ

            return;
        }

        bool airborne = !_motor.IsGrounded; // 地上にいないなら空中判定

        // ---------- AIR ATTACK (single) ----------
        if (airborne)
        {
            if (_airAttackOncePerAirtime && _airAttackUsedThisAirtime)
                return; // 今回の滞空で既に空中攻撃を使用済みなら無視

            _airAttackUsedThisAirtime = true; // 空中攻撃使用フラグを立てる

            StartAirAttackAsync().Forget();
            return;
        }

        // ---------- GROUND COMBO ----------
        StartComboAsync().Forget(); // 地上コンボを開始
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
        _canBuffer = true; // コンボウィンドウを開く（次の入力をバッファ可能にする）

        // 0) FINISHER FIRST (AOE hitbox)
        if (_finisherMode)
        {
            int dmg = Mathf.RoundToInt(_finisherDamage * _damageMul); // ダメージ倍率を適用
            float mom = _finisherMomentumGain * _damageMul; // モメンタム倍率を適用

            var hb = (_finisherHitbox != null) ? _finisherHitbox : _weaponHitbox; // フィニッシャー専用ヒットボックスを優先
            hb.EnableHitbox(dmg, mom, _finisherKnockback);
            return;
        }

        // 1) Dash attack
        if (_dashAttackMode)
        {
            int dmg = Mathf.RoundToInt(_dashAttackDamage * _damageMul); // ダッシュ攻撃のダメージを計算
            float mom = _dashAttackMomentum * _damageMul; // ダッシュ攻撃のモメンタムを計算
            _weaponHitbox.EnableHitbox(dmg, mom, _dashAttackKnockback);
            return;
        }

        // 2) Air attack
        if (_airAttackMode)
        {
            int dmg = Mathf.RoundToInt(_airAttackDamage * _damageMul); // 空中攻撃のダメージを計算
            float mom = _airAttackMomentum * _damageMul; // 空中攻撃のモメンタムを計算
            _weaponHitbox.EnableHitbox(dmg, mom, _airAttackKnockback);
            return;
        }

        // 3) Ground combo
        if (_comboSteps == null || _comboSteps.Count == 0) return; // コンボステップが未定義なら何もしない
        if (_comboIndex < 0 || _comboIndex >= _comboSteps.Count) return; // インデックスが範囲外なら無視

        var step = _comboSteps[_comboIndex]; // 現在のコンボステップを取得

        int finalDamage = Mathf.RoundToInt(step.damage * _damageMul); // 最終ダメージ値を計算
        float finalMomentum = step.momentumGain * _damageMul; // 最終モメンタム量を計算
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
        float turboAttack = 1f; // デフォルトは等倍
        var turbo = TurboModeManager.Instance;
        if (turbo != null && turbo.IsActive)
        {
            // Use attack compensation (playerSpeedMult) only so attacks are 1.5x, not multiplied by slow-mo cancel.
            turboAttack = turbo.AttackComp; // ターボ中は攻撃補正値を使用（スロー打ち消しは含まない）
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
        _cts?.Cancel(); // 前の攻撃タスクをキャンセル
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isActive = true;
        _airAttackMode = true;
        _comboIndex = 0;
        _bufferedAttack = false;

        // Lock control for the attack
        _motor.DisableInput(); // 攻撃中は入力をロック

        // Stop carry-over drift (keep Y)
        var rb = _motor.GetRigidbody();
        if (rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); // 水平速度をリセット（Y軸は維持）

        _playerAnim.SetApplyRootMotion(true); // ルートモーションを有効化

        try
        {
            float turboAttack = ComputeTurboAttackComp(); // ターボ補正を計算
            _playerAnim.SetAttackSpeed(_airAttackSpeedMult * _speedBuff * turboAttack); // アニメーション速度を設定

            // IMPORTANT:
            // We reuse TriggerAttack(0) (Attack1 trigger) but Animator chooses the AIR clip when IsGrounded=false.
            _playerAnim.TriggerAttack(0); // 空中攻撃アニメーションを再生（Animatorが空中用クリップを選択）

            await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token); // ヒットウィンドウが開くまで待機
            await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token); // ヒットウィンドウが閉じるまで待機

            // No chaining. Ignore bufferedAttack.
        }
        catch (OperationCanceledException) { }
        finally
        {
            _airAttackMode = false; // 空中攻撃モードを終了

            _playerAnim.SetApplyRootMotion(false); // ルートモーションを無効化
            RestoreAnimBaseline(); // アニメーション速度をベースラインに戻す

            _motor.EnableInput(); // 入力を再開
            _motor.ClearBufferedMovement(); // バッファされた移動をクリア

            _isActive = false;
            _comboIndex = 0;
            _canBuffer = false;
            _bufferedAttack = false;
            _weaponHitbox?.DisableHitbox(); // ヒットボックスを無効化
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
        _cts?.Cancel(); // 前の攻撃タスクをキャンセル
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isActive = true;
        _airAttackMode = false;
        _comboIndex = 0; // コンボを最初のステップから開始

        _motor.DisableInput(); // コンボ中は入力をロック

        var rb = _motor.GetRigidbody();
        if (rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); // 攻撃開始時の水平速度をリセット

        _playerAnim.SetApplyRootMotion(true); // ルートモーションを有効化

        try
        {
            while (_comboSteps != null && _comboIndex < _comboSteps.Count) // コンボステップを順に処理
            {
                var step = _comboSteps[_comboIndex]; // 現在のステップを取得

                if (rb != null)
                    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); // 各ステップ開始時に水平速度をリセット

                float turboAttack = ComputeTurboAttackComp(); // ターボ補正を計算

                _playerAnim.SetAttackSpeed(step.speedMultiplier * _speedBuff * turboAttack); // アニメーション速度を設定
                _bufferedAttack = false; // バッファをリセット

                _playerAnim.TriggerAttack(_comboIndex); // 現在のステップの攻撃アニメーションを再生

                await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token); // ヒットウィンドウ開始まで待機
                await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token); // ヒットウィンドウ終了まで待機

                if (_bufferedAttack)
                {
                    bool isLastNormalHit = (_comboIndex >= _comboSteps.Count - 1); // 最後の通常攻撃ステップか判定

                    // If player tries to chain after Attack3
                    if (isLastNormalHit)
                    {
                        if (HasMaxMomentum())
                        {
                            await PlayFinisherAsync(token); // 4th AOE hit モメンタム最大時にフィニッシャーを実行
                        }
                        break; // combo ends after finisher attempt コンボ終了
                    }

                    _comboIndex++; // 次のコンボステップへ進む
                    continue;
                }

                break; // バッファがなければコンボ終了
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _playerAnim.SetApplyRootMotion(false); // ルートモーションを無効化
            RestoreAnimBaseline(); // アニメーション速度を元に戻す

            _motor.EnableInput(); // 入力を再開
            _motor.ClearBufferedMovement(); // バッファされた移動をクリア

            _isActive = false;
            _comboIndex = 0;
            _canBuffer = false;
            _bufferedAttack = false;
            _weaponHitbox?.DisableHitbox(); // ヒットボックスを無効化
        }
    }

    /// <summary>
    /// Plays the finisher attack (4th hit AOE) when maximum momentum is reached.
    /// Waits for hitbox window before returning control.
    /// </summary>
    private async UniTask PlayFinisherAsync(CancellationToken token)
    {
        _finisherMode = true; // フィニッシャーモードを有効化

        // stop horizontal drift (keep Y)
        var rb = _motor.GetRigidbody();
        if (rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); // 水平速度をリセット

        float turboAttack = ComputeTurboAttackComp(); // ターボ補正を計算
        _playerAnim.SetAttackSpeed(_finisherSpeedMult * _speedBuff * turboAttack); // フィニッシャー速度を設定

        // Attack4 trigger
        _playerAnim.TriggerAttack(3); // 4撃目（フィニッシャー）アニメーションをトリガー

        await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token); // AOEヒットウィンドウ開始まで待機
        await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token); // AOEヒットウィンドウ終了まで待機

        _finisherMode = false; // フィニッシャーモードを解除

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
        if (_isActive) return; // 別の攻撃が進行中なら無視
        if (_dashAttackActive) return; // ダッシュ攻撃が既に実行中なら無視

        _dashAttackActive = true;
        _dashAttackMode = true;
        _dashAttackChainBuffered = false; // 連携バッファをリセット

        _motor.DisableInput(); // ダッシュ攻撃中は入力をロック

        _playerAnim.SetApplyRootMotion(true); // ルートモーションを有効化
        float turboAttack = ComputeTurboAttackComp(); // ターボ補正を計算
        _playerAnim.SetAttackSpeed(_dashAttackSpeedMult * _speedBuff * turboAttack); // アニメーション速度を設定
        _playerAnim.TriggerDashAttack(); // ダッシュ攻撃アニメーションをトリガー
    }

    /// <summary>
    /// Called when the dash attack animation completes.
    /// Re-enables player control and processes any buffered attack input.
    /// </summary>
    public void OnDashAttackEnd()
    {
        _weaponHitbox.DisableHitbox(); // ヒットボックスを無効化
        _playerAnim.SetApplyRootMotion(false); // ルートモーションを無効化
        RestoreAnimBaseline(); // アニメーション速度をベースラインに戻す

        _motor.EnableInput(); // 入力を再開

        _dashAttackActive = false;
        _dashAttackMode = false;

        if (_dashAttackChainBuffered)
        {
            _dashAttackChainBuffered = false;
            RequestAttack(); // バッファされた攻撃を実行（地上コンボへ連携）
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
            _playerAnim.RestoreBaselineSpeed();   // 1.1 during turbo ターボ中はベースライン速度（1.1倍）に戻す
        else
            _playerAnim.SetAttackSpeed(1f);       // normal 通常時は等倍速度に戻す
    }

    /// <summary>Checks if the player currently has maximum momentum level</summary>
    private bool HasMaxMomentum()
    {
        var mm = MomentumManager.Instance;
        return _enableMaxFinisher && mm != null && mm.CurrentState == MomentumState.Max; // フィニッシャーが有効かつモメンタムが最大状態か確認
    }

    /// <summary>Resets the per-airtime air attack gate when player returns to ground</summary>
    private void Update()
    {
        // reset once-per-airtime gate when grounded again
        if (_motor != null && _motor.IsGrounded)
            _airAttackUsedThisAirtime = false; // 着地時に空中攻撃の使用フラグをリセット
    }
}
