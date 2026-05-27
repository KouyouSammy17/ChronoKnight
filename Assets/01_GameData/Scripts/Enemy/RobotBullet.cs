// ロボット敵が発射する弾丸の挙動とダメージ処理を管理するスクリプト
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class RobotBullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public int Damage = 20;       // 削るHP量    // プレイヤーに与えるダメージ量
    public float Speed = 15f;      // 飛行速度    // 弾の飛行速度
    public float LifeTime = 5f;       // この秒数後に自動削除    // 弾が自動消滅するまでの秒数

    private Rigidbody _rb;  // 物理演算コンポーネント

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;      // 弾を直線飛行させる      // 重力を無効化して直線飛行させる
        _rb.isKinematic = false;
        _rb.linearVelocity = transform.forward * Speed;           // 前方向へ速度を設定

        // 迷子の弾が蓄積しないよう、LifeTime後に自動削除する
        Destroy(gameObject, LifeTime);  // 一定時間後に弾を自動削除
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1) プレイヤーに命中したか？
        if (other.CompareTag("Player"))
        {
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null)
                stats.TakeEnemyDamage(Damage, transform.position);            // onHealthChangedを呼び出し、HP0でゲームオーバー

            Destroy(gameObject);    // 命中後すぐに弾を消す
            return;
        }

        // 2) 固体のワールドジオメトリ（地面や壁など）に当たったか
        //    それらのコライダーには「Environment」などの専用レイヤーを設定すること
        if (other.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            Destroy(gameObject);    // 壁や地面に当たったら弾を消す
        }

    }
}
