// プレイヤーのアニメーション状態とトリガーを一元管理するスクリプト
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// プレイヤーの全アニメーション状態とトリガーを管理する。
/// 移動・戦闘アニメーション・ターボモード連携・ルートモーションを処理する。
/// PlayerMotorとCombatControllerと通信して状態を同期する。
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    /// <summary>Animatorコンポーネントへの参照</summary>
    [SerializeField] private Animator _anim; // Animatorコンポーネントの参照
    /// <summary>移動データ取得用のPlayerMotorへの参照</summary>
    [SerializeField] private PlayerMotor _player; // 移動データ取得用のPlayerMotor参照
    /// <summary>ヒットボックスウィンドウコールバック用の戦闘コントローラーへの参照</summary>
    [SerializeField] private CombatController _combat; // ヒットボックスウィンドウコールバック用の戦闘コントローラー

    private bool _animPaused; // アニメーターが一時停止中か
    // ───────── Turbo anim control ─────────
    private AnimatorUpdateMode _defaultUpdateMode; // デフォルトのアニメーター更新モード
    private float _defaultAnimSpeed = 1f; // デフォルトのアニメーション速度

    private bool _turboAnimActive; // ターボアニメーションモードが有効か
    private float _turboBaselineSpeed = 1.1f; // ターボ中の通常アニメーション速度（移動・待機など）
    private float _turboAttackSpeed = 1.5f;   // ターボ中の攻撃アニメーション速度

    private float _requestedAnimSpeedAbs = 1f; // 要求された絶対アニメーション速度
    // Animatorパラメータのハッシュキャッシュ
    private int _hashSpeed; // 移動速度パラメータのハッシュ
    private int _hashVerticalSpeed; // 垂直速度パラメータのハッシュ
    private int _hashIsGrounded; // 接地判定パラメータのハッシュ
    private int _hashIsJumping; // ジャンプトリガーのハッシュ
    private int _hashIsAirJumping; // 空中ジャンプトリガーのハッシュ
    private int _hashIsDashJumping; // ダッシュジャンプトリガーのハッシュ
    private int _hashWallJump; // 壁ジャンプトリガーのハッシュ
    private int _hashDash; // ダッシュトリガーのハッシュ
    private int _hashWallHangIn;    // 壁張り付き開始トリガーのハッシュ
    private int _hashWallHangLoop;  // 壁張り付きループ継続フラグのハッシュ
    private int[] _attackHashes; // 各攻撃アニメーショントリガーのハッシュ配列
    private int _hashDashAttack; // ダッシュ攻撃トリガーのハッシュ
    private int _hashDamage; // ダメージトリガーのハッシュ
    private int _hashIsHurt; // ヒット状態フラグのハッシュ
    private bool _wasWallSliding = false; // 前フレームの壁スライド状態
    private bool _justWallJumped = false; // 直前のフレームで壁ジャンプしたか
    private int _hashKnockdown; // ノックダウントリガーのハッシュ
    private int _hashRecover; // 回復トリガーのハッシュ
    private int _hashTurboStart; // ターボ開始トリガーのハッシュ
    private int _hashDie; // 死亡トリガーのハッシュ
    private int _hashDeadLoop; // 死亡ループフラグのハッシュ
    private int _hashWin; // 勝利トリガーのハッシュ



    // プレイヤー親オブジェクトのRigidbodyをキャッシュ
    private Rigidbody _rb; // 親オブジェクトのRigidbodyをキャッシュ

    private void Awake()
    {
        _defaultUpdateMode = _anim.updateMode; // デフォルトの更新モードを保存
        _defaultAnimSpeed = _anim.speed; // デフォルトの速度を保存

        // Animatorのパラメータ名と完全に一致させる必要がある
        _hashSpeed = Animator.StringToHash("Speed"); // Animatorパラメータ名をハッシュ化してキャッシュ
        _hashVerticalSpeed = Animator.StringToHash("VerticalSpeed");
        _hashIsGrounded = Animator.StringToHash("IsGrounded");
        _hashIsJumping = Animator.StringToHash("IsJumping");
        _hashIsAirJumping = Animator.StringToHash("IsAirJumping");
        _hashIsDashJumping = Animator.StringToHash("IsDashJumping");
        _hashWallJump = Animator.StringToHash("WallJump");
        _hashDash = Animator.StringToHash("IsDashing");
        _hashWallHangIn = Animator.StringToHash("WallHangIn");
        _hashWallHangLoop = Animator.StringToHash("IsWallHanging");
        _hashDamage = Animator.StringToHash("Damage");
        _hashIsHurt = Animator.StringToHash("IsHurt");
        _hashKnockdown = Animator.StringToHash("Knockdown");
        _hashRecover = Animator.StringToHash("Recover");
        _attackHashes = new[]
       {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3"),
            Animator.StringToHash("Attack4")
        };
        _hashDashAttack = Animator.StringToHash("DashAttack");
        _hashTurboStart = Animator.StringToHash("TurboStart");
        _hashDie = Animator.StringToHash("Die");
        _hashDeadLoop = Animator.StringToHash("IsDead");
        _hashWin = Animator.StringToHash("Win");
        // 2) 親階層からRigidbodyを取得
        _rb = GetComponentInParent<Rigidbody>(); // 親階層からRigidbodyを取得
        if (_rb == null)
            Debug.LogError("PlayerAnimator: could not find a Rigidbody in parent!", this);
        ApplyAnimSpeedAbs(_defaultAnimSpeed); // 初期アニメーション速度を適用

        // 新モデルのAnimatorインスペクターで「Apply Root Motion」が有効になっていても
        // 起動時に必ずfalseに初期化する。OnAnimatorMove内で攻撃中のみtrueに切り替えるため。
        // これをしないと、ゲーム開始直後からルートモーションが誤適用されて移動が速くなる。
        _anim.applyRootMotion = false;
    }

    private void LateUpdate()
    {
        if (_player == null || _anim == null) return;

        float currentSpeed = _player.GetAnimMovementSpeedNormalized(); // 正規化された移動速度を取得
        float vSpeed = _player.VerticalSpeed; // 垂直速度を取得
        bool isWallSliding = _player.IsWallSliding; // 壁スライド中か確認

        // Animatorの接地判定にはコヨーテタイムを含まない純粋な値（RAW）を使う
        bool grounded = _player.IsGroundedRaw; // コヨーテタイムを含まない純粋な接地判定

        // 念のための安全策：上昇中は決して接地扱いにしない
        if (vSpeed > 0.1f) grounded = false; // 上昇中は接地扱いにしない

        // 壁張り付き遷移
        if (!_justWallJumped && !_wasWallSliding && isWallSliding)
        {
            _anim.SetTrigger(_hashWallHangIn); // 壁張り付き開始アニメーションをトリガー
            _anim.SetBool(_hashWallHangLoop, true); // 壁張り付きループを有効化
        }
        else if (!_justWallJumped && _wasWallSliding && !isWallSliding)
        {
            _anim.SetBool(_hashWallHangLoop, false); // 壁から離れたらループを無効化
        }

        // 重要：AnimatorがUnscaledモード（ターボ）のときはUnscaled時間を使用する
        float dt = (_anim.updateMode == AnimatorUpdateMode.UnscaledTime)
            ? Time.unscaledDeltaTime // ターボ中はUnscaled時間を使用
            : Time.deltaTime;

        _anim.SetFloat(_hashSpeed, currentSpeed, 0.1f, dt); // 速度パラメータを滑らかに更新
        _anim.SetFloat(_hashVerticalSpeed, vSpeed); // 垂直速度パラメータを更新
        _anim.SetBool(_hashIsGrounded, grounded); // 接地フラグを更新

        _justWallJumped = false; // 壁ジャンプフラグをフレーム末にリセット
        _wasWallSliding = isWallSliding; // 今フレームの壁スライド状態を次フレーム比較用に保存
    }

    //private void ApplyAnimSpeed()
    //{
    //    if (_anim == null) return;

    //    float baseSpeed = _defaultAnimSpeed; // 重要
    //    _anim.speed = _animPaused ? 0f : (baseSpeed * _globalSpeedMult * _attackSpeedMult);
    //}

    /// <summary>TurboModeManagerから呼ばれる</summary>
    public void SetTurboAnimMode(bool on, float baselineSpeed = 1.1f, float attackSpeed = 1.5f)
    {
        if (_anim == null) return;

        _turboAnimActive = on;
        _turboBaselineSpeed = Mathf.Max(0.01f, baselineSpeed); // ベースライン速度を設定（最小値0.01）
        _turboAttackSpeed = Mathf.Max(0.01f, attackSpeed); // 攻撃速度を設定（最小値0.01）

        _anim.updateMode = on ? AnimatorUpdateMode.UnscaledTime : _defaultUpdateMode; // ターボ中はUnscaledTimeモードに切替

        // ベースライン速度は移動・待機アニメーションなどに適用
        ApplyAnimSpeedAbs(on ? _turboBaselineSpeed : _defaultAnimSpeed); // ターボ有無に応じた速度を適用
    }



    /// <summary>
    /// CombatControllerから呼ばれる。リアルタイムにおけるAnimatorの絶対速度目標を設定する。
    /// ターボ中、CombatControllerは1.5倍（またはステップ倍率×1.5）を設定する。
    /// </summary>
    public void SetAttackSpeed(float absoluteMultiplier)
    {
        absoluteMultiplier = Mathf.Max(0.01f, absoluteMultiplier);
        ApplyAnimSpeedAbs(absoluteMultiplier);
    }
    private void ApplyAnimSpeedAbs(float absSpeed)
    {
        _requestedAnimSpeedAbs = Mathf.Max(0.01f, absSpeed);
        if (_anim == null) return;

        _anim.speed = _animPaused ? 0f : _requestedAnimSpeedAbs;
    }

    // applyRootMotion=trueのとき、アニメーション評価後に毎フレーム呼ばれる
    private void OnAnimatorMove()
    {
        if (!_anim.applyRootMotion || _rb == null)
            return;


        // ルートモーションの生の移動量を取得
        Vector3 delta = _anim.deltaPosition; // アニメーションのルートモーション移動量を取得

        // Z方向の移動を無効化
        delta.z = 0f; // 2.5D用にZ方向の移動を無効化

        // X/Yのみ適用
        _rb.MovePosition(_rb.position + delta); // X/Y方向のルートモーションを適用
        _rb.MoveRotation(_rb.rotation * _anim.deltaRotation); // ルートモーションの回転を適用
    }
    /// <summary>
    /// 通常の地上/二段ジャンプ時にPlayerControllerから呼ばれる。
    /// </summary>
    public void TriggerJump()
    {
        _anim.SetTrigger(_hashIsJumping);
    }

    public void TriggerAirJump()
    {
        _anim.SetTrigger(_hashIsAirJumping);
    }

    public void TriggerDashJump()
    {
        _anim.SetTrigger(_hashIsDashJumping);
    }
    /// <summary>
    /// 壁ジャンプ時にPlayerControllerから呼ばれる。
    /// 張り付きループを即座に終了してWallJumpへ移行する。
    /// </summary>
    public void TriggerWallJump()
    {
        // 1) ループを即座に終了
        _anim.SetBool(_hashWallHangLoop, false); // 壁張り付きループを即座に終了

        // 2) WallJumpトリガーを発火してWallJump状態へ遷移
        _anim.SetTrigger(_hashWallJump); // 壁ジャンプアニメーションをトリガー

        // 3) このフレームで「張り付き開始/終了」ロジックが走らないようにする
        _justWallJumped = true; // このフレームで壁ジャンプしたフラグを立てる
        _wasWallSliding = false; // 壁スライド状態をリセット
    }

    /// <summary>
    /// ダッシュ開始時にPlayerControllerから呼ばれる。
    /// </summary>
    public void TriggerDash()
    {
        _anim.SetTrigger(_hashDash);
    }

    public void SetHurt(bool on)
    {
        _anim.SetBool(_hashIsHurt, on);
    }

    public void TriggerKnockdown() => _anim.SetTrigger(_hashKnockdown);
    public void TriggerRecover() => _anim.SetTrigger(_hashRecover);
    public void TriggerDamage()
    {
        _anim.SetTrigger(_hashDamage);
    }

    public void OnOpenComboWindow() => _combat.OnOpenComboWindow();
    public void OnCloseComboWindow() => _combat.OnCloseComboWindow();
    public void OnCloseDashAttackWindow() => _combat.OnDashAttackEnd();
    public void TriggerAttack(int idx)
    {
        if (idx < 0 || idx >= _attackHashes.Length)
            throw new ArgumentOutOfRangeException(nameof(idx)); // インデックスが範囲外なら例外を投げる
        _anim.SetTrigger(_attackHashes[idx]); // 対応する攻撃アニメーショントリガーを発火
    }

    public void TriggerDashAttack()
    {
        _anim.SetTrigger(_hashDashAttack);
    }

    public void TriggerTurboStart()
    {
        _anim.SetTrigger(_hashTurboStart);
    }
    public void TriggerDie()
    {
        _anim.SetTrigger(_hashDie);
    }

    public void SetDeadLoop(bool on)
    {
        _anim.SetBool(_hashDeadLoop, on);
    }

    public void TriggerWin()
    {
        _anim.SetTrigger(_hashWin);
    }
  

    public void SetApplyRootMotion(bool on)
    {
        _anim.applyRootMotion = on;
    }

    public void PauseAnimator()
    {
        _animPaused = true;
        ApplyAnimSpeedAbs(_requestedAnimSpeedAbs);
    }

    public void ResumeAnimator()
    {
        _animPaused = false;
        ApplyAnimSpeedAbs(_requestedAnimSpeedAbs);
    }

    public UniTask WaitForCurrentAnimationEnd(CancellationToken ct = default)
    {
        return UniTask.WaitUntil(
            () => _anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f,
            cancellationToken: ct
        );
    }

    public void RestoreBaselineSpeed()
    {
        if (_turboAnimActive) ApplyAnimSpeedAbs(_turboBaselineSpeed); // ターボ中はターボ基準速度に戻す
        else ApplyAnimSpeedAbs(_defaultAnimSpeed); // 通常時はデフォルト速度に戻す
    }

    public void ResetTurboAnim()
    {
        if (_anim == null) return;

        _turboAnimActive = false; // ターボアニメーションモードを解除
        _anim.updateMode = _defaultUpdateMode; // 更新モードをデフォルトに戻す

        ApplyAnimSpeedAbs(_defaultAnimSpeed); // アニメーション速度をデフォルトに戻す
        _animPaused = false; // 一時停止フラグをリセット
    }

    public void ResetForRespawn()
    {
        SetHurt(false);
       
        // if you have death loop bool / dead flag
        SetDeadLoop(false);

        // if you use root motion during attacks
        SetApplyRootMotion(false);

        // reset attack speed
        SetAttackSpeed(1f);

        RestoreBaselineSpeed();
    }
}