// 近接攻撃のヒット判定を担当するコンポーネント。
// MeleeRobotAI が攻撃ウィンドウ中だけ親 GameObject を有効化 / 無効化して使用する。
// 左右の拳など複数のヒットボックスを兄弟として配置した場合、
// どちらか一方がヒットした瞬間に残りも封鎖して1スイング1ダメージを保証する。
using UnityEngine;

/// <summary>
/// 近接攻撃のヒットボックス。<br/>
/// 有効中にプレイヤーと接触するとダメージを与える。<br/>
/// 同じ親の下に複数配置しても、1 スイングで 1 回しかダメージが入らないよう
/// 兄弟の <see cref="MeleeHitbox"/> を連動して封鎖する。
/// </summary>
public class MeleeHitbox : MonoBehaviour
{
    [Header("Attack Values")]
    [Tooltip("与えるダメージ量")]
    [SerializeField] private int _damage = 15;

    [Tooltip("PlayerDamageReceiver のノックバックベース値に加算する力（大きいほど吹き飛ぶ）")]
    [SerializeField] private float _extraKnockback = 4f;

    // 1 スイングに複数回ヒットさせないためのフラグ。
    // OnEnable で毎回リセットされるので、有効化のたびに新しいスイングとして扱われる。
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
        {
            // 自分のフラグを立てた後、同じ親の下にある全兄弟も封鎖する。
            // これにより左右の拳が同フレームにプレイヤーと重なっても二重ダメージにならない。
            BlockSiblings();
        }
    }

    /// <summary>
    /// 同じ親の下にある全ての MeleeHitbox（自分を含む）を封鎖する。
    /// 兄弟が先にヒットした場合も外部から呼ばれる。
    /// </summary>
    public void BlockSiblings()
    {
        _hitThisActivation = true;

        if (transform.parent == null) return;

        foreach (var sibling in transform.parent.GetComponentsInChildren<MeleeHitbox>())
            sibling._hitThisActivation = true;
    }
}
