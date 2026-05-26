// プレイヤーのアニメーション状態とトリガーを一元管理するスクリプト
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Manages all player animation state and triggers.
/// Handles locomotion, combat animations, turbo mode integration, and root motion.
/// Communicates with PlayerMotor and CombatController for state synchronization.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    /// <summary>Reference to the Animator component</summary>
    [SerializeField] private Animator _anim; // Animatorコンポーネントの参照
    /// <summary>Reference to the player motor for movement data</summary>
    [SerializeField] private PlayerMotor _player; // 移動データ取得用のPlayerMotor参照
    /// <summary>Reference to combat controller for hitbox window callbacks</summary>
    [SerializeField] private CombatController _combat; // ヒットボックスウィンドウコールバック用の戦闘コントローラー

    private bool _animPaused; // アニメーターが一時停止中か
    // ───────── Turbo anim control ─────────
    private AnimatorUpdateMode _defaultUpdateMode; // デフォルトのアニメーター更新モード
    private float _defaultAnimSpeed = 1f; // デフォルトのアニメーション速度

    private bool _turboAnimActive; // ターボアニメーションモードが有効か
    private float _turboBaselineSpeed = 1.1f; // others ターボ中の通常アニメーション速度（移動・待機など）
    private float _turboAttackSpeed = 1.5f;   // attacks ターボ中の攻撃アニメーション速度

    private float _requestedAnimSpeedAbs = 1f; // 要求された絶対アニメーション速度
    // Cached Animator parameter hashes
    private int _hashSpeed; // 移動速度パラメータのハッシュ
    private int _hashVerticalSpeed; // 垂直速度パラメータのハッシュ
    private int _hashIsGrounded; // 接地判定パラメータのハッシュ
    private int _hashIsJumping; // ジャンプトリガーのハッシュ
    private int _hashIsAirJumping; // 空中ジャンプトリガーのハッシュ
    private int _hashIsDashJumping; // ダッシュジャンプトリガーのハッシュ
    private int _hashWallJump; // 壁ジャンプトリガーのハッシュ
    private int _hashDash; // ダッシュトリガーのハッシュ
    private int _hashWallHangIn;    // Trigger for “hang-in” clip 壁張り付き開始トリガーのハッシュ
    private int _hashWallHangLoop;  // Bool for staying in WallHangLoop 壁張り付きループ継続フラグのハッシュ
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



    // NEW: cache the Rigidbody on your player parent
    private Rigidbody _rb; // 親オブジェクトのRigidbodyをキャッシュ

    private void Awake()
    {
        _defaultUpdateMode = _anim.updateMode; // デフォルトの更新モードを保存
        _defaultAnimSpeed = _anim.speed; // デフォルトの速度を保存

        // Names must match exactly your Animator parameters
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
        // 2) grab the Rigidbody up the hierarchy
        _rb = GetComponentInParent<Rigidbody>(); // 親階層からRigidbodyを取得
        if (_rb == null)
            Debug.LogError("PlayerAnimator: could not find a Rigidbody in parent!", this);
        ApplyAnimSpeedAbs(_defaultAnimSpeed); // 初期アニメーション速度を適用
    }

    private void LateUpdate()
    {
        if (_player == null || _anim == null) return;

        float currentSpeed = _player.GetAnimMovementSpeedNormalized(); // 正規化された移動速度を取得
        float vSpeed = _player.VerticalSpeed; // 垂直速度を取得
        bool isWallSliding = _player.IsWallSliding; // 壁スライド中か確認

        // Animator grounded should be RAW (raycast), not coyote
        bool grounded = _player.IsGroundedRaw; // コヨーテタイムを含まない純粋な接地判定

        // Extra safety: if moving upward, never call it grounded
        if (vSpeed > 0.1f) grounded = false; // 上昇中は接地扱いにしない

        // wall hang transitions (same as you had)
        if (!_justWallJumped && !_wasWallSliding && isWallSliding)
        {
            _anim.SetTrigger(_hashWallHangIn); // 壁張り付き開始アニメーションをトリガー
            _anim.SetBool(_hashWallHangLoop, true); // 壁張り付きループを有効化
        }
        else if (!_justWallJumped && _wasWallSliding && !isWallSliding)
        {
            _anim.SetBool(_hashWallHangLoop, false); // 壁から離れたらループを無効化
        }

        // IMPORTANT: use unscaled dt when animator is unscaled (Turbo)
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

    //    float baseSpeed = _defaultAnimSpeed; // important
    //    _anim.speed = _animPaused ? 0f : (baseSpeed * _globalSpeedMult * _attackSpeedMult);
    //}

    /// <summary>Called by TurboModeManager</summary>
    public void SetTurboAnimMode(bool on, float baselineSpeed = 1.1f, float attackSpeed = 1.5f)
    {
        if (_anim == null) return;

        _turboAnimActive = on;
        _turboBaselineSpeed = Mathf.Max(0.01f, baselineSpeed); // ベースライン速度を設定（最小値0.01）
        _turboAttackSpeed = Mathf.Max(0.01f, attackSpeed); // 攻撃速度を設定（最小値0.01）

        _anim.updateMode = on ? AnimatorUpdateMode.UnscaledTime : _defaultUpdateMode; // ターボ中はUnscaledTimeモードに切替

        // baseline applies to locomotion/idle/etc
        ApplyAnimSpeedAbs(on ? _turboBaselineSpeed : _defaultAnimSpeed); // ターボ有無に応じた速度を適用
    }


    
    /// <summary>
    /// Called by CombatController. This is an ABSOLUTE animator speed target in real-time.
    /// During Turbo, CombatController will set 1.5x (or step multipliers * 1.5).
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

    // This is called every frame _after_ animation is evaluated (if applyRootMotion = true)
    private void OnAnimatorMove()
    {
        if (!_anim.applyRootMotion || _rb == null)
            return;


        // grab the raw root-motion delta
        Vector3 delta = _anim.deltaPosition; // アニメーションのルートモーション移動量を取得

        // kill any Z movement
        delta.z = 0f; // 2.5D用にZ方向の移動を無効化

        // apply only X/Y
        _rb.MovePosition(_rb.position + delta); // X/Y方向のルートモーションを適用
        _rb.MoveRotation(_rb.rotation * _anim.deltaRotation); // ルートモーションの回転を適用
    }
    /// <summary>
    /// Called by PlayerController for a normal ground/double jump.
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
    /// Called by PlayerController when performing a wall-jump.
    /// Immediately clears the hang loop and goes into WallJump.
    /// </summary>
    public void TriggerWallJump()
    {
        // 1) Exit the loop immediately
        _anim.SetBool(_hashWallHangLoop, false); // 壁張り付きループを即座に終了

        // 2) Fire the WallJump trigger to transition into the WallJump state
        _anim.SetTrigger(_hashWallJump); // 壁ジャンプアニメーションをトリガー

        // 3) Prevent any “start/stop hang” logic this frame
        _justWallJumped = true; // このフレームで壁ジャンプしたフラグを立てる
        _wasWallSliding = false; // 壁スライド状態をリセット
    }

    /// <summary>
    /// Called by PlayerController when starting a dash.
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