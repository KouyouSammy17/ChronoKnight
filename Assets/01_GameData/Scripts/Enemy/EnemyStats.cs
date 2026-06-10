// 敵のHP管理・ダメージ処理・死亡演出を制御するスクリプト
using UnityEngine.Events;
using UnityEngine;
using TGRobotsWheeled;    // TGDroidStateManagerのために必要
                          // （AIが別の名前空間にある場合はそちらもインポートすること）

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyStats : MonoBehaviour
{
    [SerializeField] private int _maxHP = 50;           // 敵の最大HP
    [SerializeField] private float _deathDelay = 1.5f;     // 死亡後に残留する時間（秒）
    public UnityEvent OnDied; // ← 追加                  // 死亡時に発火するイベント
    private int _currentHP;                             // 現在のHP
    private Rigidbody _rb;                              // 物理演算コンポーネント

    private void Awake()
    {
        _currentHP = _maxHP;            // 開始時にHPを最大値で初期化
        _rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// ダメージを与えたいときに呼び出す。HPがゼロになったら死亡処理を実行する。
    /// </summary>
    public void TakeDamage(int dmg)
    {
        _currentHP = Mathf.Max(_currentHP - dmg, 0);   // HPが0未満にならないようにクランプ
        Debug.Log($"Enemy took {dmg} damage, HP now {_currentHP}");

        // AIに短いよろめきを発生させる（IStaggerable を実装している全AI型に対応）
        GetComponent<IStaggerable>()?.Stagger();

        if (_currentHP == 0)
            Die();  // HPが0になったら死亡処理を実行
    }

    private void Die()
    {
        OnDied?.Invoke(); // Destroyより先にイベントを発火する

        // 1) アセットのステートマシンを「スリープ」状態へ移行する
        var droidSM = GetComponent<TGDroidStateManager>();
        if (droidSM != null)
            droidSM.State = TGDroidStateManager.TDroidState.Sleep;  // ドロイドをスリープ状態へ移行

        // 2) 追跡・射撃を停止する（SciFiRobotAI / MeleeRobotAI 両対応）
        var sciFiAI = GetComponent<SciFiRobotAI>();
        if (sciFiAI != null)
            sciFiAI.enabled = false;    // SF ロボット AI を無効化して追跡・射撃を停止

        var meleeAI = GetComponent<MeleeRobotAI>();
        if (meleeAI != null)
            meleeAI.enabled = false;    // 近接ロボット AI を無効化して追跡・攻撃を停止

        // 3) 物理演算を停止してコライダーを無効化する
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;  // 速度をリセット
            _rb.isKinematic = true;             // 物理演算を無効化して静止させる
        }
        var col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;    // コライダーを無効化して当たり判定をなくす

        // 4) 最後に短い遅延後にゲームオブジェクトを削除する
        Destroy(gameObject, _deathDelay);   // 短い遅延後にゲームオブジェクトを削除
    }
}
