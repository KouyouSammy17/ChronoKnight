// ロボット敵が発射する弾丸の挙動とダメージ処理を管理するスクリプト
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class RobotBullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public int Damage = 20;       // how much HP to take    // プレイヤーに与えるダメージ量
    public float Speed = 15f;      // travel speed           // 弾の飛行速度
    public float LifeTime = 5f;       // auto-destroy after this many seconds  // 弾が自動消滅するまでの秒数

    private Rigidbody _rb;  // 物理演算コンポーネント

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;      // bullets fly straight      // 重力を無効化して直線飛行させる
        _rb.isKinematic = false;
        _rb.linearVelocity = transform.forward * Speed;           // 前方向へ速度を設定

        // destroy after Lifetime so stray bullets don't pile up
        Destroy(gameObject, LifeTime);  // 一定時間後に弾を自動削除
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1) Hit the player?
        if (other.CompareTag("Player"))
        {
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null)
                stats.TakeEnemyDamage(Damage, transform.position);            // invokes onHealthChanged, GameOver at zero HP :contentReference[oaicite:5]{index=5}

            Destroy(gameObject);    // 命中後すぐに弾を消す
            return;
        }

        // 2) Hitting 'solid' world geometry (e.g. your Ground or Walls)
        //    Make sure those colliders have a specific layer like 'Environment'
        if (other.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            Destroy(gameObject);    // 壁や地面に当たったら弾を消す
        }

    }
}
