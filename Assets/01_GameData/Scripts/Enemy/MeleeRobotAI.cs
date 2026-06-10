// 近接攻撃型ロボット敵のAI（巡回・追跡・近接攻撃）。
// TGRobotsWheeled に依存しない設計なので、任意のヒューマノイド / ウォーカー系モデルに使用できる。
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 近接攻撃型ロボット敵のAI。<br/>
/// 巡回（A-B往復）→ 感知で追跡 → 射程内で停止してスイング攻撃。<br/>
/// 子オブジェクト <see cref="_hitboxRoot"/> に付いた <see cref="MeleeHitbox"/> を
/// 攻撃ウィンドウ中だけ有効化してプレイヤーへダメージを与える。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyStats))]
public class MeleeRobotAI : MonoBehaviour, IStaggerable
{
    // ─────────────────────────────────────────────────────────────
    //  Inspector 設定
    // ─────────────────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("この半径内にプレイヤーが入ると追跡を開始する")]
    [SerializeField] private float _aggroRadius = 6f;

    [Tooltip("この距離以内に入ると攻撃を開始する")]
    [SerializeField] private float _attackRange = 2.0f;

    [Tooltip("プレイヤーに近づきすぎないための最小距離（これより近い場合は少し後退する）")]
    [SerializeField] private float _minDistance = 1.2f;

    [Header("Movement")]
    [SerializeField] private float _chaseSpeed   = 4.5f;
    [SerializeField] private float _patrolSpeed  = 1.8f;

    [Header("Patrol")]
    [Tooltip("巡回の折り返し地点 A")]
    [SerializeField] private Transform _pointA;
    [Tooltip("巡回の折り返し地点 B")]
    [SerializeField] private Transform _pointB;

    [Header("Attack Timing")]
    [Tooltip("1 回の攻撃が終わってから次を開始するまでの待機時間（秒）")]
    [SerializeField] private float _attackCooldown   = 2.00f;
    [Tooltip("攻撃アニメーション開始 〜 ヒットボックス有効までの振りかぶり時間（秒）")]
    [SerializeField] private float _attackWindup     = 0.25f;
    [Tooltip("ヒットボックスが有効な時間（秒）")]
    [SerializeField] private float _attackActiveTime = 0.20f;
    [Tooltip("ヒットボックス無効 〜 戦闘状態復帰までの硬直時間（秒）")]
    [SerializeField] private float _attackRecovery   = 0.45f;

    [Header("Hitbox")]
    [Tooltip("MeleeHitbox コンポーネントが付いた子 GameObject。デフォルトは Inactive にすること。")]
    [SerializeField] private GameObject _hitboxRoot;

    [Header("Stagger")]
    [Tooltip("よろめき継続時間（秒）")]
    [SerializeField] private float _staggerDuration = 0.45f;

    [Header("Animator")]
    [Tooltip("未設定の場合は GetComponentInChildren で自動取得する")]
    [SerializeField] private Animator _animator;

    [Tooltip("移動速度を渡す float パラメータ名（空欄でスキップ）")]
    [SerializeField] private string _animSpeedParam    = "Speed";

    [Tooltip("警戒・戦闘モードを示す bool パラメータ名（空欄でスキップ）")]
    [SerializeField] private string _animAlertParam    = "Alert";

    [Tooltip("攻撃アニメーションのトリガー名（空欄でスキップ）")]
    [SerializeField] private string _animAttackTrigger  = "Attack";

    [Tooltip("よろめきアニメーションのトリガー名（空欄でスキップ）")]
    [SerializeField] private string _animStaggerTrigger = "Stagger";

    // ─────────────────────────────────────────────────────────────
    //  内部状態
    // ─────────────────────────────────────────────────────────────

    private enum AIState { Patrol, Alert, Combat, Attacking, Staggered }
    private AIState _aiState = AIState.Patrol;

    private Transform _player;

    private float _attackCooldownTimer = 0f;
    private float _staggerTimer        = 0f;
    private Vector3 _currentPatrolTarget;

    private CancellationTokenSource _attackCts;

    // ─────────────────────────────────────────────────────────────
    //  Unity ライフサイクル
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        // 巡回の初期目標を設定する（B がなければその場で待機）
        _currentPatrolTarget = _pointB != null ? _pointB.position : transform.position;

        // ヒットボックスは必ず非表示からスタートする
        _hitboxRoot?.SetActive(false);
    }

    private void OnDisable()
    {
        // 攻撃シーケンスをキャンセルしてヒットボックスを安全に落とす
        _attackCts?.Cancel();
        _attackCts?.Dispose();
        _attackCts = null;

        _hitboxRoot?.SetActive(false);
    }

    private void Update()
    {
        // プレイヤー参照を GameManager から遅延取得する
        if (_player == null && GameManager.Instance != null)
            _player = GameManager.Instance.GetPlayer()?.transform;
        if (_player == null) return;

        // 攻撃クールダウンをカウントダウンする
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.deltaTime;

        float dist = Vector3.Distance(transform.position, _player.position);

        switch (_aiState)
        {
            // ── よろめき ─────────────────────────────────────────
            case AIState.Staggered:
                _staggerTimer -= Time.deltaTime;
                if (_staggerTimer <= 0f)
                    _aiState = AIState.Alert;   // 回復後は追跡モードへ戻る
                return;

            // ── 攻撃中（非同期メソッドに任せる）─────────────────
            case AIState.Attacking:
                return;

            // ── 巡回 ─────────────────────────────────────────────
            case AIState.Patrol:
                if (dist < _aggroRadius)
                {
                    _aiState = AIState.Alert;
                    break;
                }
                DoPatrol();
                SetAnimParams(speed: _patrolSpeed, alert: false);
                break;

            // ── 追跡（プレイヤーに接近）──────────────────────────
            case AIState.Alert:
                // 感知範囲を 25% 超えたら巡回に戻る（ヒステリシス）
                if (dist > _aggroRadius * 1.25f)
                {
                    _aiState = AIState.Patrol;
                    break;
                }
                // 攻撃射程の 0.5m 手前で戦闘モードに切り替える
                if (dist <= _attackRange + 0.5f)
                {
                    _aiState = AIState.Combat;
                    break;
                }
                DoChase();
                SetAnimParams(speed: _chaseSpeed, alert: true);
                break;

            // ── 戦闘（近距離を維持してスイング攻撃）────────────
            case AIState.Combat:
                // 射程の 1.5 倍まで離れたら追跡に戻る
                if (dist > _attackRange * 1.5f)
                {
                    _aiState = AIState.Alert;
                    break;
                }

                // 高さを無視した水平距離で移動を判断する
                Vector3 flatPlayer = new Vector3(_player.position.x, transform.position.y, _player.position.z);
                float   flatDist   = Vector3.Distance(transform.position, flatPlayer);

                // 常にプレイヤーの方向を向く
                Face(_player.position.x - transform.position.x);

                if (flatDist > _attackRange)
                {
                    // 射程外 → 接近する
                    Vector3 dir = (flatPlayer - transform.position).normalized;
                    transform.position += dir * _chaseSpeed * Time.deltaTime;
                    SetAnimParams(speed: _chaseSpeed, alert: true);
                }
                else if (flatDist < _minDistance)
                {
                    // 近すぎ → 少し後退する
                    Vector3 dir = (transform.position - flatPlayer).normalized;
                    transform.position += dir * _chaseSpeed * 0.4f * Time.deltaTime;
                    SetAnimParams(speed: _chaseSpeed * 0.4f, alert: true);
                }
                else
                {
                    // 理想距離 → 停止して攻撃タイミングを待つ
                    SetAnimParams(speed: 0f, alert: true);
                }

                // クールダウン完了 かつ 射程内なら攻撃を開始する
                if (_attackCooldownTimer <= 0f && flatDist <= _attackRange)
                    ExecuteAttackAsync(this.GetCancellationTokenOnDestroy()).Forget();

                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  攻撃シーケンス（非同期）
    // ─────────────────────────────────────────────────────────────

    private async UniTaskVoid ExecuteAttackAsync(CancellationToken destroyCt)
    {
        // 即座に Attacking に切り替えて二重起動を防ぐ
        // （UniTaskVoid は最初の await までは同期的に実行されるため、
        //   次の Update() が走るまでに確実にフラグが立つ）
        _aiState = AIState.Attacking;

        // 前回の CTS を破棄して新しいリンク済みトークンを作成する
        _attackCts?.Cancel();
        _attackCts?.Dispose();
        _attackCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCt);
        var ct = _attackCts.Token;

        try
        {
            // 攻撃アニメーションをトリガーする
            TriggerAnim(_animAttackTrigger);
            SetAnimParams(speed: 0f, alert: true);

            // ── 振りかぶり（ヒットボックス無効）──────────────────
            if (_attackWindup > 0f)
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_attackWindup),
                    DelayType.Realtime, PlayerLoopTiming.Update, ct);

            // ── ヒットボックス有効 ───────────────────────────────
            _hitboxRoot?.SetActive(true);

            if (_attackActiveTime > 0f)
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_attackActiveTime),
                    DelayType.Realtime, PlayerLoopTiming.Update, ct);

            // ── ヒットボックス無効 ──────────────────────────────
            _hitboxRoot?.SetActive(false);

            // ── 硬直（リカバリー）────────────────────────────────
            if (_attackRecovery > 0f)
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_attackRecovery),
                    DelayType.Realtime, PlayerLoopTiming.Update, ct);
        }
        catch (OperationCanceledException) { /* Stagger / Die によるキャンセル */ }
        finally
        {
            // 中断されてもヒットボックスを必ず無効化してクールダウンをセットする
            _hitboxRoot?.SetActive(false);
            _attackCooldownTimer = _attackCooldown;

            // Stagger など外部から上書きされていなければ戦闘状態に戻る
            if (_aiState == AIState.Attacking)
                _aiState = AIState.Combat;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  IStaggerable 実装
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ダメージを受けたときに <see cref="EnemyStats"/> から呼ばれる。
    /// 攻撃中なら即座にキャンセルしてよろめき状態に移行する。
    /// </summary>
    public void Stagger()
    {
        // 攻撃シーケンスをキャンセルしてヒットボックスを即座に無効化する
        _attackCts?.Cancel();
        _hitboxRoot?.SetActive(false);

        _aiState      = AIState.Staggered;
        _staggerTimer = _staggerDuration;

        TriggerAnim(_animStaggerTrigger);
    }

    // ─────────────────────────────────────────────────────────────
    //  移動補助
    // ─────────────────────────────────────────────────────────────

    private void DoPatrol()
    {
        if (_pointA == null || _pointB == null) return;

        // 高さを揃えた目標座標へ向かって移動する
        Vector3 target = new Vector3(_currentPatrolTarget.x, transform.position.y, _currentPatrolTarget.z);
        Vector3 dir    = (target - transform.position).normalized;
        transform.position += dir * _patrolSpeed * Time.deltaTime;
        Face(dir.x);

        // 折り返し地点に到達したら逆の地点へ向かう
        if (Vector3.Distance(transform.position, target) < 0.25f)
        {
            _currentPatrolTarget = (_currentPatrolTarget == _pointA.position)
                ? _pointB.position
                : _pointA.position;
        }
    }

    private void DoChase()
    {
        Vector3 target  = new Vector3(_player.position.x, transform.position.y, _player.position.z);
        float   flatDist = Vector3.Distance(transform.position, target);

        if (flatDist > _minDistance)
        {
            Vector3 dir = (target - transform.position).normalized;
            transform.position += dir * _chaseSpeed * Time.deltaTime;
        }

        Face(_player.position.x - transform.position.x);
    }

    // ─────────────────────────────────────────────────────────────
    //  アニメーター / 向き補助
    // ─────────────────────────────────────────────────────────────

    /// <summary>X 方向に応じて左右いずれかを向く（SciFiRobotAI と同じ規則）。</summary>
    private void Face(float dirX)
    {
        if      (dirX >  0.1f) transform.rotation = Quaternion.Euler(0f,  90f, 0f);
        else if (dirX < -0.1f) transform.rotation = Quaternion.Euler(0f, -90f, 0f);
    }

    private void SetAnimParams(float speed, bool alert)
    {
        if (_animator == null) return;
        if (!string.IsNullOrEmpty(_animSpeedParam))
            _animator.SetFloat(_animSpeedParam, speed);
        if (!string.IsNullOrEmpty(_animAlertParam))
            _animator.SetBool(_animAlertParam, alert);
    }

    private void TriggerAnim(string triggerName)
    {
        if (_animator != null && !string.IsNullOrEmpty(triggerName))
            _animator.SetTrigger(triggerName);
    }

    // ─────────────────────────────────────────────────────────────
    //  デバッグ可視化
    // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 感知範囲（赤）
        Gizmos.color = new Color(1f, 0f, 0f, 0.20f);
        Gizmos.DrawWireSphere(transform.position, _aggroRadius);

        // 攻撃射程（オレンジ）
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.30f);
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        // 最小距離（緑）
        Gizmos.color = new Color(0f, 1f, 0f, 0.20f);
        Gizmos.DrawWireSphere(transform.position, _minDistance);
    }
#endif
}
