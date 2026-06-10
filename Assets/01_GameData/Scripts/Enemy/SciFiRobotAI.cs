// SF風ロボット敵のAI行動（巡回・警戒・戦闘）を制御するスクリプト
using UnityEngine;
using TGRobotsWheeled;    // アセットの名前空間と一致していることを確認すること

[RequireComponent(typeof(TGDroidStateManager))]
public class SciFiRobotAI : MonoBehaviour, IStaggerable
{
    public float aggroRadius = 6f;          // プレイヤーを検知する半径
    public float attackRange = 10f;         // 攻撃可能な最大距離

    public float chaseSpeed = 4f;           // 追跡時の移動速度
    public float patrolSpeed = 2f;          // 巡回時の移動速度

    public float minCombatDistance = 3f;    // 戦闘中に維持する最小距離（近づきすぎを防ぐ）

    [Header("Stagger Settings")]
    [Tooltip("How long (in seconds) the droid is unable to shoot after taking damage")]
    public float staggerDuration = 0.5f;    // よろめき状態の持続時間（秒）

    [Tooltip("Seconds between each shot")]
    public float shootInterval = 1f;        // 射撃の間隔（秒）

    private float _lastShootTime = -Mathf.Infinity; // 最後に射撃した時刻（負の無限大で初回即発射を許可）

    // AIの状態を定義する列挙型
    private enum AIState { Patrol, Alert, Combat }
    private AIState _aiState = AIState.Patrol;  // 初期状態は巡回

    private Transform _player;              // プレイヤーのTransform参照
    private TGDroidStateManager _droid;     // ドロイドのステートマネージャー

    private float _staggerTimer = 0f;       // よろめき残り時間
    private bool _isStaggered = false;      // よろめき中かどうか

    // 巡回ウェイポイント
    public Transform pointA, pointB;        // 巡回の折り返し地点A・B
    private Vector3 _currentPatrolTarget;   // 現在向かっている巡回目標

    void OnEnable() => _droid.OnShoot += OnDroidShoot;     // 射撃イベントを購読
    void OnDisable() => _droid.OnShoot -= OnDroidShoot;    // 射撃イベントの購読を解除

    void Awake()
    {
        _droid = GetComponent<TGDroidStateManager>();
        _currentPatrolTarget = pointB.position; // 最初の巡回目標をBに設定
    }

    void Update()
    {
        if (_player == null && GameManager.Instance != null)
            _player = GameManager.Instance.GetPlayer()?.transform; // プレイヤー参照をGameManagerから取得
        if (_player == null) return;

        float dist = Vector3.Distance(transform.position, _player.position); // プレイヤーとの距離を算出

        // よろめき中は完全停止（移動・射撃ともに禁止）
        if (_isStaggered)
        {
            _droid.Shooting = false;
            _staggerTimer -= Time.deltaTime;            // よろめきタイマーを減算
            if (_staggerTimer <= 0f) _isStaggered = false;  // タイマー終了でよろめき解除
            return; // このフレームの状態遷移と移動をスキップ
        }

        // --- 状態遷移 ---
        switch (_aiState)
        {
            case AIState.Patrol:
                if (dist < aggroRadius)
                    _aiState = AIState.Alert;   // 感知範囲内に入ったら警戒状態へ
                break;

            case AIState.Alert:
                if (dist <= attackRange)
                    _aiState = AIState.Combat;  // 攻撃射程内に入ったら戦闘状態へ
                else if (dist > aggroRadius * 1.2f)
                    _aiState = AIState.Patrol;  // 十分遠ざかったら巡回に戻る（過敏な切り替えを防ぐヒステリシス）
                break;

            case AIState.Combat:
                if (dist > attackRange * 1.1f)
                    _aiState = AIState.Alert;   // 射程外に出たら警戒状態に戻る
                break;
        }

        // --- ドロイド状態と行動の制御 ---
        switch (_aiState)
        {
            case AIState.Patrol:
                _droid.State = TGDroidStateManager.TDroidState.Idle;      // アイドル・巡回ブレンド
                Patrol();
                _droid.Shooting = false;    // 巡回中は射撃しない
                break;

            case AIState.Alert:
                _droid.State = TGDroidStateManager.TDroidState.Alarmed;   // 警告色・警報サウンド
                Chase();
                _droid.Shooting = false;    // 警戒追跡中は射撃しない
                break;

            case AIState.Combat:
                _droid.State = TGDroidStateManager.TDroidState.Combat;

                Vector3 flatPlayer = _player.position;
                flatPlayer.y = transform.position.y;                        // 高さ差を無視して水平距離のみ考慮
                Vector3 dir = (flatPlayer - transform.position).normalized;
                float combatDist = Vector3.Distance(transform.position, flatPlayer);

                if (combatDist > attackRange)
                {
                    transform.position += dir * chaseSpeed * Time.deltaTime;    // 射程外なら近づく
                }
                else if (combatDist < minCombatDistance)
                {
                    transform.position -= dir * chaseSpeed * Time.deltaTime;    // 近すぎる場合は後退
                }

                Face(dir.x);

                // シュート間隔を考慮
                bool canShoot = (combatDist >= minCombatDistance && combatDist <= attackRange)
                    && (Time.time - _lastShootTime >= shootInterval); // 射撃間隔と距離の両方を確認
                _droid.Shooting = canShoot;
                break;

        }
    }

    // 移動の一元管理ゲート
    private void MoveIfAllowed(Vector3 dir, float speed)
    {
        if (_isStaggered) return; // 安全対策（Update()で早期リターンしているが念のため）
        transform.position += dir * speed * Time.deltaTime;    // 方向と速度に基づいて移動
    }

    void Patrol()
    {
        Vector3 groundTarget = _currentPatrolTarget;
        groundTarget.y = transform.position.y;  // 高さを自分に合わせて水平移動のみにする

        Vector3 dir = (groundTarget - transform.position).normalized;
        MoveIfAllowed(dir, patrolSpeed);

        if (Vector3.Distance(transform.position, groundTarget) < 0.2f)
            _currentPatrolTarget = (_currentPatrolTarget == pointA.position) ? pointB.position : pointA.position; // 目標に到達したら折り返す

        Face(dir.x);
    }

    void Chase()
    {
        Vector3 target = _player.position;
        target.y = transform.position.y;   // 高さを合わせて水平追跡
        Vector3 dir = (target - transform.position).normalized;

        MoveIfAllowed(dir, chaseSpeed);
        Face(dir.x);
    }

    void Face(float x)
    {
        if (x > 0.1f) transform.rotation = Quaternion.Euler(0, 90, 0);         // 右向きに回転
        else if (x < -0.1f) transform.rotation = Quaternion.Euler(0, -90, 0);  // 左向きに回転
    }

    private void OnDroidShoot(Transform origin)
    {
        _lastShootTime = Time.time;      // 射撃時刻を記録
        _droid.Shooting = false;         // Shootingフラグを即座にオフにする
    }

    public void Stagger()
    {
        _isStaggered = true;
        _staggerTimer = staggerDuration;    // よろめき時間をセット
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, aggroRadius);     // 感知範囲を赤で可視化
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, attackRange);     // 攻撃射程を黄色で可視化
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, minCombatDistance); // 最小戦闘距離を緑で可視化
    }

}
