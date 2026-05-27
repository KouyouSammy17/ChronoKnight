// プレイヤーの戦闘システム全体（コンボ・空中攻撃・ダッシュ攻撃・フィニッシャー）を管理するスクリプト
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// プレイヤーの戦闘システム（地上コンボ・空中攻撃・ダッシュ攻撃・フィニッシャー）を管理する。
/// 攻撃バッファリング、モメンタムスケーリング、ターボモード連携を担う。
/// </summary>
[RequireComponent(typeof(Collider))]
public class CombatController : MonoBehaviour
{
    /// <summary>
    /// 地上コンボシーケンスの1ステップを定義する構造体。タイミングとダメージのプロパティを持つ。
    /// </summary>
    [Serializable]
    public struct ComboStep
    {
        /// <summary>このコンボステップの識別名</summary>
        public string stepName; // コンボステップの識別名
        /// <summary>次の攻撃をバッファできる時間ウィンドウ</summary>
        public float inputWindow; // 次の攻撃をバッファできる時間ウィンドウ
        /// <summary>このステップのアニメーション速度倍率</summary>
        public float speedMultiplier; // このステップのアニメーション速度倍率
        /// <summary>この攻撃の基本ダメージ量</summary>
        public int damage; // この攻撃の基本ダメージ量
        /// <summary>命中時に得られるモメンタムポイント</summary>
        public float momentumGain; // 命中時に得られるモメンタムポイント
        /// <summary>敵に与えるノックバック力</summary>
        public float knockbackForce; // 敵に与えるノックバック力
    }

    // ==================== シリアライズ設定 ====================

    /// <summary>地上コンボチェーンを構成する攻撃ステップのリスト</summary>
    [Header("Ground Combo Definition")]
    [SerializeField] private List<ComboStep> _comboSteps = new List<ComboStep>(); // 地上コンボのステップ定義リスト

    /// <summary>空中攻撃の速度・ダメージ設定</summary>
    [Header("Air Attack (Single)")]
    [SerializeField] private float _airAttackSpeedMult = 1.0f; // 空中攻撃のアニメーション速度倍率
    [SerializeField] private int _airAttackDamage = 15; // 空中攻撃のダメージ量
    [SerializeField] private float _airAttackMomentum = 8f; // 空中攻撃命中時のモメンタム獲得量
    [SerializeField] private float _airAttackKnockback = 8f; // 空中攻撃のノックバック力

    /// <summary>有効にすると、空中攻撃を1回の滞空につき1回のみに制限する（着地でリセット）</summary>
    [Tooltip("If ON, you can only do 1 air attack per airtime (resets on landing).")]
    [SerializeField] private bool _airAttackOncePerAirtime = true; // 空中攻撃を一度の滞空中に1回のみ許可するフラグ

    /// <summary>他のプレイヤーコンポーネントへの参照（実行時に自動アサイン）</summary>
    [Header("References (auto-assigned)")]
    [SerializeField] private PlayerMotor _motor; // プレイヤーの移動コンポーネント
    [SerializeField] private PlayerAnimator _playerAnim; // プレイヤーのアニメーター
    [SerializeField] private WeaponHitbox _weaponHitbox; // 武器のヒットボックス

    /// <summary>ダッシュ攻撃の設定</summary>
    [Header("Dash Attack")]
    [SerializeField] private float _dashAttackSpeedMult = 1.0f; // ダッシュ攻撃のアニメーション速度倍率
    [SerializeField] private int _dashAttackDamage = 20; // ダッシュ攻撃のダメージ量
    [SerializeField] private float _dashAttackMomentum = 10f; // ダッシュ攻撃命中時のモメンタム獲得量
    [SerializeField] private float _dashAttackKnockback = 10f; // ダッシュ攻撃のノックバック力

    /// <summary>モメンタム最大時のAOEフィニッシャー攻撃（4撃目）の設定</summary>
    [Header("Max Momentum 4th Hit (AOE Finisher)")]
    [SerializeField] private bool _enableMaxFinisher = true; // フィニッシャー攻撃を有効にするか
    [SerializeField] private float _finisherSpeedMult = 1.0f; // フィニッシャーのアニメーション速度倍率
    [SerializeField] private int _finisherDamage = 45; // フィニッシャーのダメージ量
    [SerializeField] private float _finisherMomentumGain = 0f; // フィニッシャー命中時のモメンタム獲得量（通常0）
    [SerializeField] private float _finisherKnockback = 14f; // フィニッシャーのノックバック力
    /// <summary>AOEフィニッシャー専用の別ヒットボックス（nullの場合は武器ヒットボックスを使用）</summary>
    [SerializeField] private WeaponHitbox _finisherHitbox; // AOEフィニッシャー専用ヒットボックス（nullなら武器ヒットボックスを使用）

    // ==================== ランタイム状態 ====================

    /// <summary>コンボシーケンスの現在位置</summary>
    private int _comboIndex; // 現在のコンボステップのインデックス
    /// <summary>コンボウィンドウが開いており次の攻撃をバッファできる状態か</summary>
    private bool _canBuffer; // 次の攻撃をバッファできる状態か
    /// <summary>コンボウィンドウ中に攻撃入力がバッファされたか</summary>
    private bool _bufferedAttack; // コンボウィンドウ中に攻撃入力がバッファされたか

    /// <summary>ダッシュ攻撃が現在実行中か</summary>
    private bool _dashAttackActive; // ダッシュ攻撃が実行中か
    /// <summary>ダッシュ攻撃の再生中に次の攻撃がバッファされたか</summary>
    private bool _dashAttackChainBuffered; // ダッシュ攻撃中に次の攻撃がバッファされたか
    /// <summary>現在の攻撃モードがダッシュ攻撃か</summary>
    private bool _dashAttackMode; // 現在ダッシュ攻撃モードか

    /// <summary>現在の攻撃モードが空中攻撃か</summary>
    private bool _airAttackMode; // 現在空中攻撃モードか

    /// <summary>何らかの攻撃シーケンスが現在実行中か</summary>
    private bool _isActive; // 何らかの攻撃シーケンスが進行中か
    /// <summary>全攻撃に適用するダメージ倍率（バフ・デバフから算出）</summary>
    private float _damageMul = 1f; // 全攻撃に適用するダメージ倍率
    /// <summary>攻撃アニメーションに適用する速度バフ倍率</summary>
    private float _speedBuff = 1f; // 攻撃アニメーションの速度バフ倍率
    /// <summary>フィニッシャー攻撃が現在再生中か</summary>
    private bool _finisherMode; // フィニッシャー攻撃が実行中か

    /// <summary>非同期攻撃タスクのキャンセルトークン</summary>
    private CancellationTokenSource _cts; // 非同期攻撃処理のキャンセルトークン

    /// <summary>1回の滞空につき空中攻撃を1回に制限するゲートフラグ</summary>
    private bool _airAttackUsedThisAirtime; // 今回の滞空で空中攻撃を使用済みか

    /// <summary>現在コンボが実行中かどうか</summary>
    public bool IsComboActive => _isActive;
    /// <summary>現在ダッシュ攻撃が実行中かどうか</summary>
    public bool IsDashAttackActive => _dashAttackActive;

    /// <summary>プレイヤー階層からコンポーネント参照を初期化する</summary>
    private void Awake()
    {
        _motor = _motor ?? GetComponent<PlayerMotor>(); // モーターを自動取得
        _playerAnim = _playerAnim ?? GetComponentInChildren<PlayerAnimator>(); // アニメーターを子から自動取得
        _weaponHitbox = _weaponHitbox ?? GetComponentInChildren<WeaponHitbox>(); // 武器ヒットボックスを子から自動取得
    }

    /// <summary>
    /// 入力システムからの攻撃コマンドを受け取るコールバック。
    /// started イベントにのみ反応する。
    /// </summary>
    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        RequestAttack();
    }

    /// <summary>
    /// 攻撃リクエストを処理し、適切な攻撃タイプを開始する。
    /// バッファリング・ダッシュ攻撃の連携・空中/地上攻撃の判別を行う。
    /// </summary>
    public void RequestAttack()
    {
        if (_motor == null || _playerAnim == null) return;

        // ダッシュ攻撃の再生中は「地上コンボへの連携」バッファを許可する
        if (_dashAttackActive)
        {
            _dashAttackChainBuffered = true; // ダッシュ攻撃中のコンボ連携をバッファ
            return;
        }

        // 既に何かが実行中の場合：
        if (_isActive)
        {
            // 重要：空中攻撃はループしないため、空中攻撃モード中はバッファリングを無視する
            if (_canBuffer && !_airAttackMode)
                _bufferedAttack = true; // ウィンドウ内なら次の攻撃をバッファ

            return;
        }

        bool airborne = !_motor.IsGrounded; // 地上にいないなら空中判定

        // ---------- 空中攻撃（シングル） ----------
        if (airborne)
        {
            if (_airAttackOncePerAirtime && _airAttackUsedThisAirtime)
                return; // 今回の滞空で既に空中攻撃を使用済みなら無視

            _airAttackUsedThisAirtime = true; // 空中攻撃使用フラグを立てる

            StartAirAttackAsync().Forget();
            return;
        }

        // ---------- 地上コンボ ----------
        StartComboAsync().Forget(); // 地上コンボを開始
    }

    /// <summary>現在の攻撃シーケンスを即時キャンセルする</summary>
    public void CancelCombo()
    {
        _cts?.Cancel();
        _weaponHitbox?.DisableHitbox();
    }

    /// <summary>以降の全攻撃に適用するダメージ倍率を設定する</summary>
    public void SetDamageMultiplier(float m) => _damageMul = m;

    /// <summary>アニメーションに適用する攻撃速度倍率を設定する</summary>
    public void SetAttackSpeedBuff(float b) => _speedBuff = b;

    /// <summary>
    /// 攻撃アニメーションがヒットボックスウィンドウを開いたときに呼ばれる。
    /// 現在の攻撃モードに応じた適切なヒットボックスを有効化し、倍率を適用する。
    /// </summary>
    public void OnOpenComboWindow()
    {
        _canBuffer = true; // コンボウィンドウを開く（次の入力をバッファ可能にする）

        // 0) フィニッシャー優先（AOEヒットボックス）
        if (_finisherMode)
        {
            int dmg = Mathf.RoundToInt(_finisherDamage * _damageMul); // ダメージ倍率を適用
            float mom = _finisherMomentumGain * _damageMul; // モメンタム倍率を適用

            var hb = (_finisherHitbox != null) ? _finisherHitbox : _weaponHitbox; // フィニッシャー専用ヒットボックスを優先
            hb.EnableHitbox(dmg, mom, _finisherKnockback);
            return;
        }

        // 1) ダッシュ攻撃
        if (_dashAttackMode)
        {
            int dmg = Mathf.RoundToInt(_dashAttackDamage * _damageMul); // ダッシュ攻撃のダメージを計算
            float mom = _dashAttackMomentum * _damageMul; // ダッシュ攻撃のモメンタムを計算
            _weaponHitbox.EnableHitbox(dmg, mom, _dashAttackKnockback);
            return;
        }

        // 2) 空中攻撃
        if (_airAttackMode)
        {
            int dmg = Mathf.RoundToInt(_airAttackDamage * _damageMul); // 空中攻撃のダメージを計算
            float mom = _airAttackMomentum * _damageMul; // 空中攻撃のモメンタムを計算
            _weaponHitbox.EnableHitbox(dmg, mom, _airAttackKnockback);
            return;
        }

        // 3) 地上コンボ
        if (_comboSteps == null || _comboSteps.Count == 0) return; // コンボステップが未定義なら何もしない
        if (_comboIndex < 0 || _comboIndex >= _comboSteps.Count) return; // インデックスが範囲外なら無視

        var step = _comboSteps[_comboIndex]; // 現在のコンボステップを取得

        int finalDamage = Mathf.RoundToInt(step.damage * _damageMul); // 最終ダメージ値を計算
        float finalMomentum = step.momentumGain * _damageMul; // 最終モメンタム量を計算
        float finalKnockback = step.knockbackForce;

        _weaponHitbox.EnableHitbox(finalDamage, finalMomentum, finalKnockback);
    }

    /// <summary>
    /// 攻撃アニメーションがヒットボックスウィンドウを閉じたときに呼ばれる。
    /// 全アクティブなヒットボックスを無効化し、バッファリングを終了する。
    /// </summary>
    public void OnCloseComboWindow()
    {
        _canBuffer = false;
        _weaponHitbox.DisableHitbox();
        if (_finisherHitbox != null) _finisherHitbox.DisableHitbox();
    }

    /// <summary>
    /// ターボモードの状態に基づいて攻撃速度の補正係数を算出する。
    /// ターボ中は1.5倍、通常時は1倍を返す。
    /// </summary>
    private float ComputeTurboAttackComp()
    {
        float turboAttack = 1f; // デフォルトは等倍
        var turbo = TurboModeManager.Instance;
        if (turbo != null && turbo.IsActive)
        {
            // 攻撃補正値（playerSpeedMult）のみ使用して攻撃を1.5倍にする。スロー打ち消しは含まない。
            turboAttack = turbo.AttackComp; // ターボ中は攻撃補正値を使用（スロー打ち消しは含まない）
        }
        return turboAttack;
    }

    // -----------------------
    // 空中攻撃（シングル）
    // -----------------------
    /// <summary>
    /// 単発の空中攻撃を開始する。プレイヤーの操作をロックし、ルートモーションを適用し、
    /// 設定に応じて1回の滞空につき1回のみ攻撃を許可する。
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

        // 攻撃中に操作をロック
        _motor.DisableInput(); // 攻撃中は入力をロック

        // キャリーオーバーの水平ドリフトを止める（Y軸は維持）
        var rb = _motor.GetRigidbody();
        if (rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); // 水平速度をリセット（Y軸は維持）

        _playerAnim.SetApplyRootMotion(true); // ルートモーションを有効化

        try
        {
            float turboAttack = ComputeTurboAttackComp(); // ターボ補正を計算
            _playerAnim.SetAttackSpeed(_airAttackSpeedMult * _speedBuff * turboAttack); // アニメーション速度を設定

            // 重要：
            // TriggerAttack(0)（Attack1トリガー）を再利用するが、IsGrounded=false のとき Animator が空中用クリップを選択する。
            _playerAnim.TriggerAttack(0); // 空中攻撃アニメーションを再生（Animatorが空中用クリップを選択）

            await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token); // ヒットウィンドウが開くまで待機
            await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token); // ヒットウィンドウが閉じるまで待機

            // 連携なし。バッファされた攻撃入力を無視する。
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
    // 地上コンボ
    // -----------------------
    /// <summary>
    /// 地上コンボシーケンスを開始する。各攻撃の間で入力バッファリングを行いながらコンボステップを繰り返す。
    /// 最終ヒット後にモメンタムが最大値であればフィニッシャーを発動する。
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

                    // Attack3の後に連携しようとした場合
                    if (isLastNormalHit)
                    {
                        if (HasMaxMomentum())
                        {
                            await PlayFinisherAsync(token); // 4撃目（AOE）モメンタム最大時にフィニッシャーを実行
                        }
                        break; // フィニッシャー試行後にコンボ終了
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
    /// モメンタム最大時にフィニッシャー攻撃（4撃目AOE）を再生する。
    /// ヒットボックスウィンドウを待ってから制御を返す。
    /// </summary>
    private async UniTask PlayFinisherAsync(CancellationToken token)
    {
        _finisherMode = true; // フィニッシャーモードを有効化

        // 水平ドリフトを止める（Y軸は維持）
        var rb = _motor.GetRigidbody();
        if (rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); // 水平速度をリセット

        float turboAttack = ComputeTurboAttackComp(); // ターボ補正を計算
        _playerAnim.SetAttackSpeed(_finisherSpeedMult * _speedBuff * turboAttack); // フィニッシャー速度を設定

        // Attack4トリガー
        _playerAnim.TriggerAttack(3); // 4撃目（フィニッシャー）アニメーションをトリガー

        await UniTask.WaitUntil(() => _canBuffer, cancellationToken: token); // AOEヒットウィンドウ開始まで待機
        await UniTask.WaitUntil(() => !_canBuffer, cancellationToken: token); // AOEヒットウィンドウ終了まで待機

        _finisherMode = false; // フィニッシャーモードを解除

    }

    // -----------------------
    // ダッシュ攻撃
    // -----------------------
    /// <summary>
    /// ダッシュ攻撃を開始する。他の攻撃が実行中でない場合のみ実行される。
    /// 攻撃中はルートモーションを適用し、プレイヤーの入力をロックする。
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
    /// ダッシュ攻撃アニメーションが完了したときに呼ばれる。
    /// プレイヤーの操作を再開し、バッファされた攻撃入力を処理する。
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
    /// アニメーション速度をベースライン状態に戻す（ターボモードが有効な場合はそれを考慮する）。
    /// </summary>
    private void RestoreAnimBaseline()
    {
        if (_playerAnim == null) return;

        var turbo = TurboModeManager.Instance;
        if (turbo != null && turbo.IsActive)
            _playerAnim.RestoreBaselineSpeed();   // ターボ中はベースライン速度（1.1倍）に戻す
        else
            _playerAnim.SetAttackSpeed(1f);       // 通常時は等倍速度に戻す
    }

    /// <summary>プレイヤーが現在モメンタム最大状態かどうかを確認する</summary>
    private bool HasMaxMomentum()
    {
        var mm = MomentumManager.Instance;
        return _enableMaxFinisher && mm != null && mm.CurrentState == MomentumState.Max; // フィニッシャーが有効かつモメンタムが最大状態か確認
    }

    /// <summary>プレイヤーが地面に戻ったとき、1回の滞空あたりの空中攻撃制限をリセットする</summary>
    private void Update()
    {
        // 着地時に「1回の滞空につき1回」のゲートをリセット
        if (_motor != null && _motor.IsGrounded)
            _airAttackUsedThisAirtime = false; // 着地時に空中攻撃の使用フラグをリセット
    }
}
