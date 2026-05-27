// プレイヤーがダメージを受けたときのヒットリアクションとノックダウン処理を管理するスクリプト
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// プレイヤーのダメージ受け取りとヒットリアクションを全て処理する。
/// 無敵フレーム・ノックバック適用・ノックダウンシーケンスを管理する。
/// 空中・地上のダメージに対応し、カスタマイズ可能なリアクションとターボ補正をサポートする。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMotor), typeof(PlayerStats))]
public class PlayerDamageReceiver : MonoBehaviour
{
    /// <summary>地上ヒット時のヒットスタン持続時間</summary>
    [Header("Ground Hit Reaction")]
    [SerializeField] private float _hitStun = 0.25f; // 地上ヒット時のヒットスタン持続時間
    /// <summary>地上ヒット時に適用されるノックバック力</summary>
    [SerializeField] private float _knockback = 8f; // 地上ヒット時のノックバック力
    /// <summary>ヒット後の無敵フレーム持続時間</summary>
    [SerializeField] private float _iframes = 0.5f; // ヒット後の無敵時間
    /// <summary>無敵フレームのタイミングがTimeScaleを無視するか（ターボモード対応）</summary>
    [SerializeField] private bool _ignoreTimeScale = true; // 無敵時間をTimeScaleに依存しないか（ターボモード対応）

    /// <summary>ヒット時に水平速度をキャンセルするか</summary>
    [Header("Velocity Handling")]
    [SerializeField] private bool _cancelHorizontalVelocityOnHit = true; // ヒット時に水平速度をキャンセルするか
    /// <summary>ヒット時に上方向の速度を維持するか</summary>
    [SerializeField] private bool _keepUpwardVelocity = true; // ヒット時に上方向の速度を維持するか

    /// <summary>空中でダメージを受けたときにノックダウンシーケンスに移行するか</summary>
    [Header("Air Hit (Kirby / Smash style)")]
    [SerializeField] private bool _airDamageKnockdown = true; // 空中でダメージを受けたときにノックダウンシーケンスに移行するか
    /// <summary>空中ヒット時の上方向打ち上げ力</summary>
    [SerializeField] private float _airLaunchUp = 6f; // 空中ヒット時の上方向打ち上げ力
    /// <summary>空中ヒット時の水平ノックバック倍率</summary>
    [SerializeField] private float _airLaunchHorizontalMultiplier = 1.0f; // 空中ヒット時の水平ノックバック倍率

    /// <summary>空中ノックダウン中に追加の下方向加速を適用するか</summary>
    [Header("Optional Slam Down")]
    [SerializeField] private bool _useSlamDown = true; // スラムダウン（強制落下加速）を使用するか
    /// <summary>スラムダウン加速が始まるまでの遅延</summary>
    [SerializeField] private float _slamDelay = 0.08f; // スラムダウン加速が始まるまでの遅延
    /// <summary>スラムダウン中の下方向加速力</summary>
    [SerializeField] private float _slamDownAccel = 45f; // スラムダウン時の下方向加速力

    /// <summary>着地シーケンス前の最低空中滞空時間</summary>
    [Header("Landing Knockdown Sequence")]
    [SerializeField] private float _minAirTumbleTime = 0.10f; // 着地前の最低滞空（もんどり）時間
    /// <summary>着地からノックダウントリガーまでの遅延</summary>
    [SerializeField] private float _impactToDownDelay = 0.12f; // 着地からノックダウントリガーまでの遅延
    /// <summary>ノックダウン後の回復アニメーション時間</summary>
    [SerializeField] private float _recoverTime = 0.5f; // ノックダウン後の回復アニメーション時間

    /// <summary>ヒット時に攻撃者の方向を向くか</summary>
    [Header("Facing")]
    [SerializeField] private bool _faceAttackerOnHit = true; // ヒット時に攻撃者の方向を向くか

    /// <summary>プレイヤーのモーターコンポーネントへの参照</summary>
    private PlayerMotor _motor; // プレイヤーのモーターコンポーネント
    /// <summary>プレイヤーのアニメーターコンポーネントへの参照</summary>
    private PlayerAnimator _anim; // プレイヤーのアニメーターコンポーネント
    /// <summary>物理演算用リジッドボディへの参照</summary>
    private Rigidbody _rb; // 物理演算用リジッドボディ
    /// <summary>コンボキャンセル用の戦闘コントローラーへの参照</summary>
    private CombatController _combat; // コンボキャンセル用の戦闘コントローラー

    /// <summary>ヒットリアクションシーケンスのキャンセルトークン</summary>
    private CancellationTokenSource _hitCts; // ヒットリアクション処理のキャンセルトークン
    /// <summary>無敵時間タイマーのキャンセルトークン</summary>
    private CancellationTokenSource _invulnCts; // 無敵時間タイマーのキャンセルトークン

    /// <summary>現在プレイヤーが無敵状態かどうか</summary>
    private bool _invuln; // 現在無敵状態か
    /// <summary>現在プレイヤーが無敵状態かどうか</summary>
    public bool IsInvulnerable => _invuln;
    /// <summary>ヒットによるヒットスタン中かどうか</summary>
    public bool IsInHitStun { get; private set; }
    /// <summary>ノックダウン状態（空中→地面へのシーケンス）かどうか</summary>
    public bool IsKnockedDown { get; private set; }

    /// <summary>プレイヤー階層からコンポーネント参照を初期化する</summary>
    private void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _anim = GetComponentInChildren<PlayerAnimator>();
        _combat = GetComponent<CombatController>();
        _rb = _motor != null ? _motor.GetRigidbody() : GetComponent<Rigidbody>();
    }

    /// <summary>コンポーネント無効化時に保留中の非同期タスクをキャンセルしてクリーンアップする</summary>
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

    /// <summary>無敵状態を手動で設定する</summary>
    public void SetInvulnerable(bool v) => _invuln = v;

    /// <summary>
    /// ヒットリアクションシーケンスを再生する。
    /// 地上・空中のノックダウンを処理し、ノックバックを適用して無敵状態を管理する。
    /// </summary>
    /// <param name="sourceWorldPos">ノックバック方向の計算に使う攻撃者のワールド座標（任意）</param>
    /// <param name="extraForce">ノックバックへの追加力倍率（ダメージ・バフなどから算出）</param>
    public async UniTaskVoid PlayHitReact(Vector3? sourceWorldPos = null, float extraForce = 0f)
    {
        if (_motor == null || _rb == null) return;
        if (_invuln) return; // 無敵中はダメージを受け付けない

        // 前のリアクションをキャンセル
        _hitCts?.Cancel(); // 前のヒットリアクションをキャンセル
        _hitCts?.Dispose();
        _hitCts = new CancellationTokenSource();
        var ct = _hitCts.Token;

        _invuln = true; // 無敵状態に設定
        IsInHitStun = true;
        IsKnockedDown = false;

        // 操作をロック
        _motor.DisableInput(); // ヒット中は入力をロック
        _combat?.CancelCombo(); // 進行中のコンボをキャンセル
        _motor.GetComponent<PlayerStateMachineBrain>()?.Input?.ClearPressedBuffers(); // バッファされた入力をクリア

        try
        {
            // 重要：ダメージ処理にはコヨーテタイムを除いた純粋な接地判定を使用する
            bool inAir = !_motor.IsGroundedRaw; // コヨーテタイムを除いた純粋な空中判定

            if (_airDamageKnockdown && inAir)
            {
                await PlayAirKnockdownSequence(sourceWorldPos, extraForce, ct); // 空中ノックダウンシーケンスを実行
                await DelaySeconds(_iframes, ct); // 無敵時間を待機
                return;
            }

            // 地上ヒット
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
            // 状態をクリーンアップ
            IsInHitStun = false;
            _invuln = false; // 無敵状態を解除

            _hitCts?.Dispose();
            _hitCts = null;
        }
    }

    /// <summary>
    /// 空中ノックダウンシーケンスを再生する：上方打ち上げ → スラムダウン → 着地後ノックダウン。
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

        // 最低滞空（もんどり）時間
        await DelaySeconds(_minAirTumbleTime, ct);

        if (_useSlamDown)
        {
            await DelaySeconds(_slamDelay, ct); // スラムダウン開始前の遅延を待機
            if (!ct.IsCancellationRequested)
            {
                float slamAccel = _slamDownAccel;

                // 加速度はTimeScaleの影響で弱まるため、スロー分だけ補正する。
                var turbo = TurboModeManager.Instance;
                if (turbo != null && turbo.IsActive)
                    slamAccel *= turbo.RealTimeComp; // = 1/slowFactor スロー中は加速力をTimeScaleで補正

                _rb.AddForce(Vector3.down * slamAccel, ForceMode.Acceleration); // 下方向に強制落下加速を適用
            }
        }

        // 重要：コヨーテタイムが即座に発火しないよう、純粋な接地判定を待つ
        await UniTask.WaitUntil(() => _motor.IsGroundedRaw, PlayerLoopTiming.Update, ct); // 実際に地面に着くまで待機

        // 着地した瞬間：滑りを止める
        _motor.StopHorizontalInstant(); // 着地時に水平速度を即時停止
        var v = _rb.linearVelocity;
        v.x = 0f;
        v.z = 0f;
        _rb.linearVelocity = v; // 横方向の滑りを完全にゼロにする

        // 着地ダメージ / ノックダウン
        _anim?.TriggerKnockdown(); // ノックダウンアニメーションをトリガー
        await DelaySeconds(_impactToDownDelay, ct); // 着地からノックダウンまでの遅延を待機

        _anim?.TriggerRecover(); // 起き上がりアニメーションをトリガー
        await DelaySeconds(_recoverTime, ct); // 回復アニメーションが終わるまで待機

        _anim?.SetHurt(false); // ヒットアニメーション状態を解除

        IsKnockedDown = false; // ノックダウン状態を終了
        IsInHitStun = false;

        _motor.EnableInput(); // 入力を再開
    }

    // リスポーン・テレポート時に呼び出す。保留中のWaitUntilが後から発火しないようにする。
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
        float knockDirX = -attackerSideX; // 攻撃者と逆方向にノックバック

        float force = (_knockback + Mathf.Max(0f, extraForce)) * horizontalMultiplier; // 最終ノックバック力を計算
        // ターボが有効なら、ダメージの衝撃が実時間で自然に感じられるようスロー分を補正する
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

        // フォールバック：「背後から」と仮定する
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
            // オブジェクトが存在する場合でも無敵を解除しトークンを破棄する。
            if (this != null)
                _invuln = false;

            _invulnCts?.Dispose();
            _invulnCts = null;
        }
    }
}
