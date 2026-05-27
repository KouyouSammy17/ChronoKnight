// 攻撃アニメーション中の武器ヒットボックスを管理するスクリプト
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// 攻撃アニメーション中のプレイヤーの武器ヒットボックスを管理する。
/// 戦闘ウィンドウに応じて動的に有効化/無効化され、敵との衝突時にダメージを与える。
/// モメンタムシステムとヒット時のフィードバックエフェクトと連携する。
/// </summary>
public class WeaponHitbox : MonoBehaviour
{
    /// <summary>ヒットボックスが命中したときに適用するダメージ量</summary>
    [HideInInspector] public int Damage; // ヒット時に適用するダメージ量
    /// <summary>ヒットボックスが命中したときにプレイヤーに与えるモメンタムポイント</summary>
    [HideInInspector] public float MomentumGain = 0f; // ヒット時にプレイヤーに与えるモメンタムポイント

    /// <summary>ヒット検出用のコライダーコンポーネント（トリガー）</summary>
    private Collider _collider; // ヒット検出用トリガーコライダー

    /// <summary>ヒットボックスが敵に命中したときに再生するフィードバックエフェクト</summary>
    [Header("Hit Feedback (FEEL)")]
    [SerializeField] private MMFeedbacks _enemyHitFeedback; // 敵にヒットした時に再生するフィードバックエフェクト

    /// <summary>ヒットボックスのコライダーを初期化する（デフォルトは無効状態）</summary>
    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.enabled = false; // デフォルトは無効（攻撃ウィンドウ中のみ有効化）
    }

    /// <summary>
    /// 指定したダメージとモメンタム値でヒットボックスを有効化する。
    /// 攻撃のヒットウィンドウ開始時に呼ばれる。
    /// </summary>
    /// <param name="damage">命中時に適用するダメージ量</param>
    /// <param name="momentumGain">命中時に付与するモメンタムポイント</param>
    /// <param name="finalKnockback">ノックバック力（敵処理用に保存）</param>
    public void EnableHitbox(int damage, float momentumGain, float finalKnockback)
    {
        Damage = damage; // ヒット時のダメージを設定
        MomentumGain = momentumGain; // ヒット時のモメンタム獲得量を設定
        _collider.enabled = true; // ヒットボックスを有効化
    }

    /// <summary>
    /// ヒットボックスを無効化する。
    /// 攻撃のヒットウィンドウが閉じたときに呼ばれる。
    /// </summary>
    public void DisableHitbox()
    {
        _collider.enabled = false;
    }

    /// <summary>
    /// このヒットボックスが敵と衝突したときに発火する。
    /// ダメージを与え、モメンタムを付与し、フィードバックエフェクトを再生する。
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy")) // 衝突したオブジェクトが敵タグを持つか確認
        {
            var enemyStats = other.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                // 敵にダメージを与える
                enemyStats.TakeDamage(Damage); // 敵にダメージを与える

                // ヒット成功でプレイヤーにモメンタムを付与する
                MomentumManager.Instance.AddMomentum(MomentumGain); // プレイヤーにモメンタムを付与

                // ヒットフィードバックエフェクトを再生する
                _enemyHitFeedback?.PlayFeedbacks(); // ヒットエフェクト（振動・エフェクト等）を再生
            }
        }
    }
}
