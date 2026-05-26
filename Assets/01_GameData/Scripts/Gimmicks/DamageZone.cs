// プレイヤーに触れるとダメージを与える危険エリアのスクリプト
using UnityEngine;

public class DamageZone : MonoBehaviour
{
    [SerializeField] private int _damage = 10;  // プレイヤーに与えるダメージ量

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null)
                // pass the trap world position so the player can face/knockback correctly
                stats.TakeEnemyDamage(_damage, transform.position); // トラップのワールド座標をノックバック方向の計算に使用
        }
    }
}
