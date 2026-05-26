// 攻撃アニメーション中の武器ヒットボックスを管理するスクリプト
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// Manages the player's weapon hitbox during attack animations.
/// Enabled/disabled dynamically based on combat window, deals damage on enemy collision.
/// Integrates with momentum system and feedback effects on hit.
/// </summary>
public class WeaponHitbox : MonoBehaviour
{
    /// <summary>Damage value to apply when this hitbox connects</summary>
    [HideInInspector] public int Damage; // ヒット時に適用するダメージ量
    /// <summary>Momentum points to award the player when this hitbox connects</summary>
    [HideInInspector] public float MomentumGain = 0f; // ヒット時にプレイヤーに与えるモメンタムポイント

    /// <summary>Collider component (trigger) for hit detection</summary>
    private Collider _collider; // ヒット検出用トリガーコライダー

    /// <summary>Feedback effect to play when the hitbox successfully hits an enemy</summary>
    [Header("Hit Feedback (FEEL)")]
    [SerializeField] private MMFeedbacks _enemyHitFeedback; // 敵にヒットした時に再生するフィードバックエフェクト

    /// <summary>Initializes the hitbox collider (starts disabled)</summary>
    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.enabled = false; // default: off デフォルトは無効（攻撃ウィンドウ中のみ有効化）
    }

    /// <summary>
    /// Activates the hitbox with specified damage and momentum values.
    /// Called at the start of an attack's hit window.
    /// </summary>
    /// <param name="damage">Damage to apply on hit</param>
    /// <param name="momentumGain">Momentum points to award on hit</param>
    /// <param name="finalKnockback">Knockback force (stored for enemy processing)</param>
    public void EnableHitbox(int damage, float momentumGain, float finalKnockback)
    {
        Damage = damage; // ヒット時のダメージを設定
        MomentumGain = momentumGain; // ヒット時のモメンタム獲得量を設定
        _collider.enabled = true; // ヒットボックスを有効化
    }

    /// <summary>
    /// Deactivates the hitbox.
    /// Called when the attack's hit window closes.
    /// </summary>
    public void DisableHitbox()
    {
        _collider.enabled = false;
    }

    /// <summary>
    /// Triggers when this hitbox collides with an enemy.
    /// Applies damage, awards momentum, and plays feedback effects.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy")) // 衝突したオブジェクトが敵タグを持つか確認
        {
            var enemyStats = other.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                // Apply damage to enemy
                enemyStats.TakeDamage(Damage); // 敵にダメージを与える

                // Award momentum to player for successful hit
                MomentumManager.Instance.AddMomentum(MomentumGain); // プレイヤーにモメンタムを付与

                // Play hit feedback effect
                _enemyHitFeedback?.PlayFeedbacks(); // ヒットエフェクト（振動・エフェクト等）を再生
            }
        }
    }
}
