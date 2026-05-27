// プレイヤーのHP・スタミナなどのステータスを管理するスクリプト
using UnityEngine;
using UnityEngine.Events;

/// <summary>整数値の変化を通知するカスタムUnityEvent</summary>
[System.Serializable] public class IntEvent : UnityEvent<int> { }

/// <summary>
/// プレイヤーのHP・スタミナのステータスを管理する。
/// ゲートチェック付きのダメージ受け取り・スタミナ回復・ダメージイベントを処理する。
/// モメンタムシステムとヒットリアクショントリガーと連携する。
/// </summary>
public class PlayerStats : MonoBehaviour
{
    /// <summary>プレイヤーが持てる最大HP</summary>
    [Header("Health Settings")]
    [SerializeField] private int _maxHP = 100; // HPの最大値
    /// <summary>新しいシーン/レベル開始時の初期HP</summary>
    [SerializeField] private int _startingHP = 100; // シーン開始時の初期HP

    /// <summary>プレイヤーが持てる最大スタミナ</summary>
    [Header("Stamina Settings")]
    [SerializeField] private int _maxStamina = 100; // スタミナの最大値
    /// <summary>新しいシーン/レベル開始時の初期スタミナ</summary>
    [SerializeField] private int _startingStamina = 100; // シーン開始時の初期スタミナ
    /// <summary>スタミナの回復量（毎秒）</summary>
    [SerializeField] private float _staminaRegenRate = 10f; // スタミナの毎秒回復量

    /// <summary>HP変化時に発火するイベント（新しいHP値を渡す）</summary>
    public IntEvent onHealthChanged; // HP変化時に発火するイベント（新しいHP値を渡す）
    /// <summary>スタミナ変化時に発火するイベント（新しいスタミナ値を渡す）</summary>
    public IntEvent onStaminaChanged; // スタミナ変化時に発火するイベント

    /// <summary>現在のHP</summary>
    private int _currentHP; // 現在のHP
    /// <summary>現在のスタミナ</summary>
    private int _currentStamina; // 現在のスタミナ
    /// <summary>スムーズなスタミナ回復のためのアキュムレータ</summary>
    private float _staminaRegenAccumulator; // スタミナ回復の端数蓄積用アキュムレータ

    /// <summary>ダメージがブロックされる終了時刻（UnscaledTime基準のノーダメージ免疫期間）</summary>
    private float _noDamageUntilTime; // ノーダメージ免疫期間の終了時刻（UnscaledTime基準）

    /// <summary>現在のHP</summary>
    public int CurrentHP => _currentHP;
    /// <summary>最大HP</summary>
    public int MaxHP => _maxHP;
    /// <summary>現在のスタミナ</summary>
    public int CurrentStamina => _currentStamina;
    /// <summary>最大スタミナ</summary>
    public int MaxStamina => _maxStamina;

    /// <summary>イベントを初期化し、ステータスを開始値にリセットする</summary>
    private void Awake()
    {
        // イベントが未初期化なら生成
        onHealthChanged ??= new IntEvent(); // イベントが未初期化なら生成
        onStaminaChanged ??= new IntEvent(); // イベントが未初期化なら生成
        ResetStats(); // ステータスを初期値にリセット
    }

    /// <summary>毎フレームスタミナ回復を更新する</summary>
    private void Update()
    {
        RegenerateStamina();
    }

    /// <summary>
    /// 一時的なノーダメージ免疫期間を設定する。
    /// UnscaledTimeを使用するため、ポーズ中やその他のTime操作中でも正常に動作する。
    /// </summary>
    /// <param name="seconds">免疫期間の秒数</param>
    public void ArmNoDamageFor(float seconds)
    {
        _noDamageUntilTime = Mathf.Max(_noDamageUntilTime, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    /// <summary>
    /// シンプルなダメージ受け取り（ハザード系のダメージ。デフォルトのヒットリアクションを発動する）。
    /// </summary>
    /// <param name="amount">HPから差し引くダメージ量</param>
    /// <returns>ダメージが適用された場合はtrue、ゲートによりブロックされた場合はfalse</returns>
    public bool TakeDamage(int amount)
    {
        return TakeDamageInternal(amount, sourceWorldPos: null, extraKnockback: 0f,
                                  ignoreGates: false, triggerHitReact: true);
    }

    /// <summary>
    /// ノックバックと方向情報を伴う敵からのダメージ。
    /// </summary>
    /// <param name="amount">ダメージ量</param>
    /// <param name="sourceWorldPos">攻撃者のワールド座標（ノックバック方向の計算に使用）</param>
    /// <param name="extraKnockback">追加のノックバック力修飾子</param>
    /// <param name="ignoreGates">無敵/ノーダメージゲートを無視するか</param>
    /// <param name="triggerHitReact">ヒットリアクションアニメーションを再生するか</param>
    /// <returns>ダメージが適用された場合はtrue</returns>
    public bool TakeEnemyDamage(int amount, Vector3 sourceWorldPos, float extraKnockback = 0f,
                            bool ignoreGates = false, bool triggerHitReact = true)
    {
        return TakeDamageInternal(amount, sourceWorldPos, extraKnockback, ignoreGates, triggerHitReact);
    }

    /// <summary>
    /// 環境ハザードからのダメージ（通常はノックバックや回転なし）。
    /// </summary>
    /// <param name="amount">ダメージ量</param>
    /// <param name="ignoreGates">ダメージゲートを無視するか</param>
    /// <param name="triggerHitReact">ヒットリアクションを再生するか（ハザードでは通常false）</param>
    /// <returns>ダメージが適用された場合はtrue</returns>
    public bool TakeHazardDamage(int amount, bool ignoreGates = false, bool triggerHitReact = false)
    {
        // ハザードは通常回転・ノックバックしない。triggerHitReactのデフォルトはfalse
        return TakeDamageInternal(amount, sourceWorldPos: null, extraKnockback: 0f, ignoreGates, triggerHitReact);
    }

    /// <summary>
    /// 全ゲートチェックとモメンタム連携を含む内部ダメージ処理。
    /// ダメージの適用・モメンタムペナルティ・ヒットリアクションのトリガーを行う。
    /// </summary>
    private bool TakeDamageInternal(int amount, Vector3? sourceWorldPos, float extraKnockback,
                                  bool ignoreGates, bool triggerHitReact)
    {
        if (amount <= 0 || _currentHP <= 0) return false; // ダメージが0以下またはすでに死亡中なら無効

        var recv = GetComponent<PlayerDamageReceiver>();

        if (!ignoreGates)
        {
            if (Time.unscaledTime < _noDamageUntilTime) return false; // ノーダメージ期間中はブロック
            if (recv != null && recv.IsInvulnerable) return false; // 無敵状態中はブロック
        }

        _currentHP = Mathf.Max(_currentHP - amount, 0); // HPをダメージ分減算（最低0まで）
        onHealthChanged?.Invoke(_currentHP); // HP変化イベントを発火

        // モメンタムシステムへのダメージペナルティ
        MomentumManager.Instance?.AddMomentum(-20f); // ダメージによるモメンタムペナルティ
        MomentumManager.Instance?.BreakMaxLock(); // モメンタム最大ロックを解除
        GetComponent<MomentumBuffsManager>()?.RemoveMaxBuffIfActive(); // 最大バフをアクティブなら除去

        if (triggerHitReact && recv != null)
            recv.PlayHitReact(sourceWorldPos, extraKnockback).Forget(); // ヒットリアクションを非同期再生

        if (_currentHP == 0) Die(); // HPが0になったら死亡処理
        return true;
    }

    /// <summary>
    /// ダッシュなどのアクションのためにスタミナを消費しようとする。
    /// </summary>
    /// <param name="cost">消費するスタミナ量</param>
    /// <returns>スタミナが足りて消費できた場合はtrue、不足の場合はfalse</returns>
    public bool SpendStamina(int cost)
    {
        if (cost <= 0) return true;
        if (_currentStamina < cost) return false;
        _currentStamina -= cost;
        onStaminaChanged?.Invoke(_currentStamina);
        return true;
    }

    /// <summary>
    /// 設定された回復量に基づいて毎フレームスタミナを回復する。
    /// アキュムレータを使用してフレームレートに依存しないスムーズな回復を実現する。
    /// </summary>
    private void RegenerateStamina()
    {
        if (_currentStamina < _maxStamina)
        {
            _staminaRegenAccumulator += _staminaRegenRate * Time.deltaTime; // 経過時間に応じてアキュムレータを加算
            int regenPoints = Mathf.FloorToInt(_staminaRegenAccumulator); // 整数部分を回復ポイントとして取得
            if (regenPoints > 0)
            {
                _staminaRegenAccumulator -= regenPoints; // 使用した分だけアキュムレータを減らす
                _currentStamina = Mathf.Min(_currentStamina + regenPoints, _maxStamina); // スタミナを回復（最大値を超えない）
                onStaminaChanged?.Invoke(_currentStamina); // スタミナ変化イベントを発火
            }
        }
        else
        {
            _staminaRegenAccumulator = 0f; // スタミナが最大値の場合はアキュムレータをリセット
        }
    }

    /// <summary>
    /// HPとスタミナを開始値にリセットする。
    /// シーン読み込み時またはレベル再起動時に呼び出す。
    /// 免疫ゲートをクリアして更新イベントを発火する。
    /// </summary>
    public void ResetStats()
    {
        _currentHP = Mathf.Clamp(_startingHP, 0, _maxHP);
        _currentStamina = Mathf.Clamp(_startingStamina, 0, _maxStamina);
        _staminaRegenAccumulator = 0f;
        _noDamageUntilTime = 0f;
        onHealthChanged?.Invoke(_currentHP);
        onStaminaChanged?.Invoke(_currentStamina);
    }

    /// <summary>HPがゼロになったときにゲームオーバーシーケンスを発動する</summary>
    private void Die()
    {
        GameManager.Instance.GameOver();
    }
}
