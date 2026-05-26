// 敵のHP管理・ダメージ処理・死亡演出を制御するスクリプト
using UnityEngine.Events;
using UnityEngine;
using TGRobotsWheeled;    // for TGDroidStateManager
                          // (if your AI lives in another namespace, import that too)

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyStats : MonoBehaviour
{
    [SerializeField] private int _maxHP = 50;           // 敵の最大HP
    [SerializeField] private float _deathDelay = 1.5f;     // seconds to linger
    public UnityEvent OnDied; // ← add                  // 死亡時に発火するイベント
    private int _currentHP;                             // 現在のHP
    private Rigidbody _rb;                              // 物理演算コンポーネント

    private void Awake()
    {
        _currentHP = _maxHP;            // 開始時にHPを最大値で初期化
        _rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Call this when you want to deal damage. When HP hits zero DIE.
    /// </summary>
    public void TakeDamage(int dmg)
    {
        _currentHP = Mathf.Max(_currentHP - dmg, 0);   // HPが0未満にならないようにクランプ
        Debug.Log($"Enemy took {dmg} damage, HP now {_currentHP}");

        // trigger a brief stagger on the AI
        var ai = GetComponent<SciFiRobotAI>();
        if (ai != null)
            ai.Stagger();   // ダメージを受けたAIをよろめかせる

        if (_currentHP == 0)
            Die();  // HPが0になったら死亡処理を実行
    }

    private void Die()
    {
        OnDied?.Invoke(); // ← fire BEFORE Destroy       // Destroyより先にイベントを発火する

        // 1) Switch the asset's state machine into "Sleep"
        var droidSM = GetComponent<TGDroidStateManager>();
        if (droidSM != null)
            droidSM.State = TGDroidStateManager.TDroidState.Sleep;  // ドロイドをスリープ状態へ移行

        // 2) Stop chasing/firing
        var ai = GetComponent<SciFiRobotAI>();
        if (ai != null)
            ai.enabled = false;     // AIスクリプトを無効化して追跡・射撃を停止

        // 3) Freeze physics & disable collider
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;  // 速度をリセット
            _rb.isKinematic = true;             // 物理演算を無効化して静止させる
        }
        var col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;    // コライダーを無効化して当たり判定をなくす

        // 4) Finally destroy after a short delay
        Destroy(gameObject, _deathDelay);   // 短い遅延後にゲームオブジェクトを削除
    }
}
