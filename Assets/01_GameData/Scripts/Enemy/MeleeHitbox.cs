// 近接攻撃のヒット判定を担当するコンポーネント。
// MeleeRobotAI が攻撃ウィンドウ中だけ親 GameObject を有効化 / 無効化して使用する。
using UnityEngine;

/// <summary>
/// 近接攻撃のヒットボックス。<br/>
/// 有効中にプレイヤーと接触するとダメージを与える。<br/>
/// 1 回のスイングで 1 回だけヒットするよう <c>_hitThisActivation</c> でガードしている。
/// </summary>
public class MeleeHitbox : MonoBehaviour
{
    [Header("Attack Values")]
    [Tooltip("与えるダメージ量")]
    [SerializeField] private int _damage = 15;

    [Tooltip("PlayerDamageReceiver のノックバックベース値に加算する力（大きいほど吹き飛ぶ）")]
    [SerializeField] private float _extraKnockback = 4f;

    // 1 スイングに複数回ヒットさせないためのフラグ
    // OnEnable で毎回リセットされるので、有効化のたびに新しいスイングとして扱われる
    private bool _hitThisActivation = false;

    private void OnEnable()
    {
        _hitThisActivation = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hitThisActivation) return;         // 同スイング内でのヒット済み判定
        if (!other.CompareTag("Player")) return;

        var stats = other.GetComponent<PlayerStats>();
        if (stats == null) return;

        // ノックバック方向の計算には攻撃者（ルート）のワールド座標を使用する
        Vector3 attackerPos = transform.root.position;

        bool applied = stats.TakeEnemyDamage(_damage, attackerPos, _extraKnockback);
        if (applied)
            _hitThisActivation = true;          // ダメージが通った場合のみフラグを立てる
    }
}
