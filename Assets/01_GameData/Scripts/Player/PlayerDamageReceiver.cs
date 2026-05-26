// プレイヤーがダメージを受けたときのヒットリアクションとノックダウン処理を管理するスクリプト
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Handles all player damage reception and hit reactions.
/// Manages invulnerability frames, knockback application, and knockdown sequences.
/// Supports air and ground damage with customizable reactions and turbo compensation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMotor), typeof(PlayerStats))]
public class PlayerDamageReceiver : MonoBehaviour
{
    /// <summary>Duration of hitstun on ground hit</summary>
    [Header("Ground Hit Reaction")]
    [SerializeField] private float _hitStun = 0.25f; // 地上ヒット時のヒットスタン持続時間
    /// <summary>Knockback force applied on ground hit</summary>
    [SerializeField] private float _knockback = 8f; // 地上ヒット時のノックバック力
    /// <summary>Invulnerability frame duration after hit</summary>
    [SerializeField] private float _iframes = 0.5f; // ヒット後の無敵時間
    /// <summary>Whether invulnerability frame timing ignores TimeScale (Turbo mode)</summary>
    [SerializeField] private bool _ignoreTimeScale = true; // 無敵時間をTimeScaleに依存しないか（ターボモード対応）

    /// <summary>Whether to cancel horizontal velocity on hit</summary>
    [Header("Velocity Handling")]
    [SerializeField] private bool _cancelHorizontalVelocityOnHit = true; // ヒット時に水平速度をキャンセルするか
    /// <summary>Whether to preserve upward velocity on hit</summary>
    [SerializeField] private bool _keepUpwardVelocity = true; // ヒット時に上方向の速度を維持するか

    /// <summary>Whether being hit in the air triggers knockdown sequence</summary>
    [Header("Air Hit (Kirby / Smash style)")]
    [SerializeField] private bool _airDamageKnockdown = true; // 空中でダメージを受けたときにノックダウンシーケンスに移行するか
    /// <summary>Upward launch force when hit in air</summary>
    [SerializeField] private float _airLaunchUp = 6f; // 空中ヒット時の上方向打ち上げ力
    /// <summary>Horizontal knockback multiplier for air hits</summary>
    [SerializeField] private float _airLaunchHorizontalMultiplier = 1.0f; // 空中ヒット時の水平ノックバック倍率

    /// <summary>Whether to apply additional downward acceleration during air knockdown</summary>
    [Header("Optional Slam Down")]
    [SerializeField] private bool _useSlamDown = true; // スラムダウン（強制落下加速）を使用するか
    /// <summary>Delay before slam-down acceleration applies</summary>
    [SerializeField] private float _slamDelay = 0.08f; // スラムダウン加速が始まるまでの遅延
    /// <summary>Downward acceleration force during slam</summary>
    [SerializeField] private float _slamDownAccel = 45f; // スラムダウン時の下方向加速力

    /// <summary>Minimum tumble time in air before landing sequence</summary>
    [Header("Landing Knockdown Sequence")]
    [SerializeField] private float _minAirTumbleTime = 0.10f; // 着地前の最低滞空（もんどり）時間
    /// <summary>Delay between impact and knockdown trigger</summary>
    [SerializeField] private float _impactToDownDelay = 0.12f; // 着地からノックダウントリガーまでの遅延
    /// <summary>Recovery animation duration after knockdown</summary>
    [SerializeField] private float _recoverTime = 0.5f; // ノックダウン後の回復アニメーション時間

    /// <summary>Whether player should face the attacker on hit</summary>
    [Header("Facing")]
    [SerializeField] private bool _faceAttackerOnHit = true; // ヒット時に攻撃者の方向を向くか

    /// <summary>Reference to player motor component</summary>
    private PlayerMotor _motor; // プレイヤーのモーターコンポーネント
    /// <summary>Reference to player animator component</summary>
    private PlayerAnimator _anim; // プレイヤーのアニメーターコンポーネント
    /// <summary>Reference to rigidbody for physics</summary>
    private Rigidbody _rb; // 物理演算用リジッドボディ
    /// <summary>Reference to combat controller for cancellation</summary>
    private CombatController _combat; // コンボキャンセル用の戦闘コントローラー

    /// <summary>Cancellation token for hit reaction sequences</summary>
    private CancellationTokenSource _hitCts; // ヒットリアクション処理のキャンセルトークン
    /// <summary>Cancellation token for invulnerability timing</summary>
    private CancellationTokenSource _invulnCts; // 無敵時間タイマーのキャンセルトークン

    /// <summary>Whether player is currently invulnerable</summary>
    private bool _invuln; // 現在無敵状態か
    /// <summary>Whether player is currently invulnerable</summary>
    public bool IsInvulnerable => _invuln;
    /// <summary>Whether player is in active hitstun from a hit</summary>
    public bool IsInHitStun { get; private set; }
    /// <summary>Whether player is in knockdown state (air-to-ground sequence)</summary>
    public bool IsKnockedDown { get; private set; }

    /// <summary>Initializes component references from the player hierarchy</summary>
    private void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _anim = GetComponentInChildren<PlayerAnimator>();
        _combat = GetComponent<CombatController>();
        _rb = _motor != null ? _motor.GetRigidbody() : GetComponent<Rigidbody>();
    }

    /// <summary>Cleanup on component disable to cancel pending async tasks</summary>
    private void OnDisable()
    {
        _hitCts?.Cancel();
        _hitCts?.Dispose();
        _hitCts = null;

        _invulnCts?.Cancel();
        _invulnCts?.Dispose();
        _invulnCts = null;


        _invuln = false;
        IsInHitStun = false;
        IsKnockedDown = false;

        _anim?.SetHurt(false);
        _motor?.EnableInput();
    }

    /// <summary>Sets the invulnerability state manually</summary>
    public void SetInvulnerable(bool v) => _invuln = v;

    /// <summary>
    /// Plays the hit reaction sequence.
    /// Handles both ground and air knockdown, applies knockback, and manages invulnerability.
    /// </summary>
    /// <param name="sourceWorldPos">Optional world position of the attacker for knockback direction</param>
    /// <param name="extraForce">Additional force multiplier for knockback (from damage, buffs, etc.)</param>
    public async UniTaskVoid PlayHitReact(Vector3? sourceWorldPos = null, float extraForce = 0f)
    {
        if (_motor == null || _rb == null) return;
        if (_invuln) return; // 無敵中はダメージを受け付けない

        // cancel previous reaction
        _hitCts?.Cancel(); // 前のヒットリアクションをキャンセル
        _hitCts?.Dispose();
        _hitCts = new CancellationTokenSource();
        var ct = _hitCts.Token;

        _invuln = true; // 無敵状態に設定
        IsInHitStun = true;
        IsKnockedDown = false;

        // lock control
        _motor.DisableInput(); // ヒット中は入力をロック
        _combat?.CancelCombo(); // 進行中のコンボをキャンセル
        _motor.GetComponent<PlayerStateMachineBrain>()?.Input?.ClearPressedBuffers(); // バッファされた入力をクリア

        try
        {
            // IMPORTANT: use RAW grounded (no coyote) for damage logic
            bool inAir = !_motor.IsGroundedRaw; // コヨーテタイムを除いた純粋な空中判定

            if (_airDamageKnockdown && inAir)
            {
                await PlayAirKnockdownSequence(sourceWorldPos, extraForce, ct); // 空中ノックダウンシーケンスを実行
                await DelaySeconds(_iframes, ct); // 無敵時間を待機
                return;
            }

            // ground hit
            ApplyKnockback(sourceWorldPos, extraForce, verticalLaunch: 0f, horizontalMultiplier: 1f); // 地上ノックバックを適用

            _anim?.SetHurt(true); // ヒットアニメーション状態を有効化
            _anim?.TriggerDamage(); // ダメージアニメーションをトリガー

            await DelaySeconds(_hitStun, ct); // ヒットスタン時間を待機

            _anim?.SetHurt(false); // ヒットアニメーション状態を解除
            IsInHitStun = false;
            _motor.EnableInput(); // 入力を再開

            await DelaySeconds(_iframes, ct); // 無敵時間を待機
        }
        catch (OperationCanceledException) { }
        finally
        {
            // cleanup state
            IsInHitStun = false;
            _invuln = false; // 無敵状態を解除

            _hitCts?.Dispose();
            _hitCts = null;
        }
    }

    /// <summary>
    /// Plays the air knockdown sequence: launch up, slam down, then knockdown on landing.
    /// </summary>
    private async UniTask PlayAirKnockdownSequence(Vector3? sourceWorldPos, float extraForce, CancellationToken ct)
    {
        IsKnockedDown = true;

        float attackerSideX = GetAttackerSideX(sourceWorldPos);

        if (_faceAttackerOnHit)
        {
            float yaw = (attackerSideX > 0f) ? 90f : -90f;
            _motor.ForceFacingYaw(yaw);
        }

        ApplyKnockback(sourceWorldPos, extraForce,
            verticalLaunch: _airLaunchUp,
            horizontalMultiplier: _airLaunchHorizontalMultiplier);

        _anim?.SetHurt(true); // ヒットアニメーション状態を有効化
        _anim?.TriggerDamage(); // ダメージアニメーションをトリガー

        // minimum�gtumble time�h
        await DelaySeconds(_minAirTumbleTime, ct);

        if (_useSlamDown)
        {
            await DelaySeconds(_slamDelay, ct); // スラムダウン開始前の遅延を待機
            if (!ct.IsCancellationRequested)
            {
                float slamAccel = _slamDownAccel;

                // Acceleration DOES get weaker under timeScale, so compensate only for slow-mo.
                var turbo = TurboModeManager.Instance;
                if (turbo != null && turbo.IsActive)
                    slamAccel *= turbo.RealTimeComp; // = 1/slowFactor スロー中は加速力をTimeScaleで補正

                _rb.AddForce(Vector3.down * slamAccel, ForceMode.Acceleration); // 下方向に強制落下加速を適用
            }
        }

        // IMPORTANT: wait for RAW grounded so coyote doesn't instantly trigger
        await UniTask.WaitUntil(() => _motor.IsGroundedRaw, PlayerLoopTiming.Update, ct); // 実際に地面に着くまで待機

        // When we truly land: stop sliding
        _motor.StopHorizontalInstant(); // 着地時に水平速度を即時停止
        var v = _rb.linearVelocity;
        v.x = 0f;
        v.z = 0f;
        _rb.linearVelocity = v; // 横方向の滑りを完全にゼロにする

        // DamageLand / Knockdown
        _anim?.TriggerKnockdown(); // ノックダウンアニメーションをトリガー
        await DelaySeconds(_impactToDownDelay, ct); // 着地からノックダウンまでの遅延を待機

        _anim?.TriggerRecover(); // 起き上がりアニメーションをトリガー
        await DelaySeconds(_recoverTime, ct); // 回復アニメーションが終わるまで待機

        _anim?.SetHurt(false); // ヒットアニメーション状態を解除

        IsKnockedDown = false; // ノックダウン状態を終了
        IsInHitStun = false;

        _motor.EnableInput(); // 入力を再開
    }

    // Call this when you respawn/teleport the player so the pending WaitUntil doesn't fire later.
    public void CancelForRespawn()
    {
        _hitCts?.Cancel();
        _hitCts?.Dispose();
        _hitCts = null;

        _invuln = false;
        IsInHitStun = false;
        IsKnockedDown = false;

        _anim?.SetHurt(false);
    }


    private void ApplyKnockback(Vector3? sourceWorldPos, float extraForce, float verticalLaunch, float horizontalMultiplier)
    {
        float attackerSideX = GetAttackerSideX(sourceWorldPos); // 攻撃者の横方向（+1 or -1）を取得
        float knockDirX = -attackerSideX; // away from attacker 攻撃者と逆方向にノックバック

        float force = (_knockback + Mathf.Max(0f, extraForce)) * horizontalMultiplier; // 最終ノックバック力を計算
        // If Turbo is active, compensate slow-mo so damage impulses feel normal in real time
        var turbo = TurboModeManager.Instance;
        if (turbo != null && turbo.IsActive)
        {
            force *= turbo.KnockbackComp; // ターボ中のスロー効果をノックバック力で補正
            verticalLaunch *= turbo.KnockbackComp; // 垂直打ち上げ力も同様に補正
        }

        Vector3 v = _rb.linearVelocity;
        if (_cancelHorizontalVelocityOnHit) v.x = 0f; // 水平速度をキャンセル
        if (_keepUpwardVelocity) v.y = Mathf.Max(v.y, 0f); // 上方向の速度を維持（下向き速度は消す）
        _rb.linearVelocity = v;

        _rb.AddForce(new Vector3(knockDirX * force, verticalLaunch, 0f), ForceMode.VelocityChange); // ノックバック力を即時適用
    }

    private float GetAttackerSideX(Vector3? sourceWorldPos)
    {
        if (sourceWorldPos.HasValue)
        {
            float dx = sourceWorldPos.Value.x - transform.position.x; // 攻撃者とプレイヤーのX方向差分
            return (dx >= 0f) ? 1f : -1f; // 右なら+1、左なら-1を返す
        }

        // fallback: �gbehind you�h
        float facingX = Mathf.Sign(_motor.GetFacingDirection().x);
        if (Mathf.Abs(facingX) < 0.001f) facingX = 1f;
        return -facingX;
    }

    private UniTask DelaySeconds(float seconds, CancellationToken ct)
    {
        // _ignoreTimeScaleがtrueなら実時間で待機（ターボのスロー影響を受けない）
        return UniTask.Delay(TimeSpan.FromSeconds(seconds),
            _ignoreTimeScale ? DelayType.Realtime : DelayType.DeltaTime,
            PlayerLoopTiming.Update, ct);
    }

    public async UniTaskVoid SetInvulnerableFor(float seconds)
    {
        _invulnCts?.Cancel(); // 既存の無敵タイマーをキャンセル
        _invulnCts?.Dispose();
        _invulnCts = null;

        if (seconds <= 0f)
        {
            _invuln = false; // 0秒以下なら即座に無敵を解除
            return;
        }

        _invuln = true;

        _invulnCts = new CancellationTokenSource();
        var ct = _invulnCts.Token;

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(seconds),
                DelayType.Realtime, PlayerLoopTiming.Update, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            // Clear invulnerability and dispose token even if object still exists.
            if (this != null)
                _invuln = false;

            _invulnCts?.Dispose();
            _invulnCts = null;
        }
    }
}
