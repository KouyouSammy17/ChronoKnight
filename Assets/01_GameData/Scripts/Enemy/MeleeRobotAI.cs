// 近接攻撃型ロボット敵のAI（巡回・追跡・近接攻撃）。
// TGRobotsWheeled に依存しない設計なので、任意のヒューマノイド / ウォーカー系モデルに使用できる。
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 近接攻撃型ロボット敵のAI。<br/>
/// 巡回（A-B往復）→ 感知で追跡 → 射程内で停止してコンボ攻撃。<br/>
/// 左右の拳ヒットボックス（<see cref="_leftHitboxRoot"/> / <see cref="_rightHitboxRoot"/>）を
/// 各ヒットのウィンドウ中だけ有効化してプレイヤーへダメージを与える。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyStats))]
public class MeleeRobotAI : MonoBehaviour, IStaggerable
{
    // ─────────────────────────────────────────────────────────────
    //  コンボ1段分のタイミング定義
    // ─────────────────────────────────────────────────────────────

    [System.Serializable]
    private struct HitTiming
    {
        [Tooltip("前段終了（またはコンボ開始）からヒットボックスが有効になるまでの振りかぶり時間（秒）")]
        [Range(0f, 1.5f)] public float windup;

        [Tooltip("ヒットボックスが有効な時間（秒）")]
        [Range(0f, 0.5f)] public float activeTime;
    }
    // ─────────────────────────────────────────────────────────────
    //  Inspector 設定
    // ─────────────────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("この半径内にプレイヤーが入ると追跡を開始する")]
    [SerializeField] private float _aggroRadius = 6f;

    [Tooltip("プレイヤーを初めて発見したときに停止する時間（秒）。Alarmed アニメーションの長さに合わせる。")]
    [SerializeField, Range(0f, 2f)] private float _alertStopDuration = 0.6f;

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

    [Header("Combo Timing")]
    [Tooltip("ON: Animation Event でヒットボックスを制御する（推奨・アニメーションと完全同期）。\nOFF: 下の _comboHits 配列の秒数でコードから制御する。")]
    [SerializeField] private bool _useAnimationEvents = true;

    [Tooltip("_useAnimationEvents が OFF のときだけ使用。各ヒットの振りかぶり・有効時間をアニメクリップの長さに合わせて設定する。")]
    [SerializeField] private HitTiming[] _comboHits = new HitTiming[]
    {
        new HitTiming { windup = 0.25f, activeTime = 0.15f },  // Hit 1
        new HitTiming { windup = 0.30f, activeTime = 0.15f },  // Hit 2
        new HitTiming { windup = 0.35f, activeTime = 0.20f },  // Hit 3
    };

    [Tooltip("コンボ全段終了後の硬直時間（秒）。Animation Event モード時はコンボアニメ総尺 + この値だけ待つ。")]
    [SerializeField] private float _attackRecovery = 0.45f;

    [Tooltip("_useAnimationEvents が ON のとき、コンボアニメーションの総尺（秒）を入力する。Attack1+2+3 クリップの合計時間。")]
    [SerializeField] private float _comboDuration = 1.20f;

    [Tooltip("1 コンボ終了から次のコンボ開始までの待機時間（秒）")]
    [SerializeField] private float _attackCooldown = 2.00f;

    [Header("Hitbox")]
    [Tooltip("左拳の MeleeHitbox GameObject。デフォルトは Inactive にすること。")]
    [SerializeField] private GameObject _leftHitboxRoot;

    [Tooltip("右拳の MeleeHitbox GameObject。デフォルトは Inactive にすること。")]
    [SerializeField] private GameObject _rightHitboxRoot;

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
    [SerializeField] private string _animStaggerTrigger = "Damage";

    [Tooltip("死亡アニメーションのトリガー名（空欄でスキップ）")]
    [SerializeField] private string _animDieTrigger     = "Die";

    // ─────────────────────────────────────────────────────────────
    //  内部状態
    // ─────────────────────────────────────────────────────────────

    private enum AIState { Patrol, Alert, Combat, Attacking, Staggered, Dead }
    private AIState _aiState = AIState.Patrol;

    private Transform _player;

    private float _attackCooldownTimer = 0f;
    private float _staggerTimer        = 0f;
    private float _alertStopTimer      = 0f;   // 発見直後の硬直タイマー
    private Vector3 _currentPatrolTarget;

    private CancellationTokenSource _attackCts;
    private Rigidbody _rb;

    // ─────────────────────────────────────────────────────────────
    //  Unity ライフサイクル
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        // 巡回の初期目標を設定する（B がなければその場で待機）
        _currentPatrolTarget = _pointB != null ? _pointB.position : transform.position;

        // ヒットボックスは必ず非表示からスタートする
        DeactivateAllHitboxes();
    }

    private void OnDisable()
    {
        // 攻撃シーケンスをキャンセルしてヒットボックスを安全に落とす
        _attackCts?.Cancel();
        _attackCts?.Dispose();
        _attackCts = null;

        DeactivateAllHitboxes();
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
            // ── 死亡（EnemyStats が enabled = false にするまでの1フレームを無視する）
            case AIState.Dead:
                return;

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
                    // 発見直後の硬直タイマーをセットする（Alarmed アニメーションを見せる）
                    _alertStopTimer = _alertStopDuration;
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
                    _alertStopTimer = 0f;   // パトロールに戻るときはタイマーをリセット
                    break;
                }
                // 攻撃射程の 0.5m 手前で戦闘モードに切り替える
                if (dist <= _attackRange + 0.5f)
                {
                    _aiState = AIState.Combat;
                    _alertStopTimer = 0f;
                    break;
                }

                // ── 発見直後の硬直（Alarmed アニメーションを再生する）──
                if (_alertStopTimer > 0f)
                {
                    _alertStopTimer -= Time.deltaTime;
                    Face(_player.position.x - transform.position.x); // プレイヤーの方を向く
                    SetAnimParams(speed: 0f, alert: true);            // Alarmed ステートを再生
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

        // 攻撃開始時に残留速度を消してスライドを防ぐ
        if (_rb != null)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // 前回の CTS を破棄して新しいリンク済みトークンを作成する
        _attackCts?.Cancel();
        _attackCts?.Dispose();
        _attackCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCt);
        var ct = _attackCts.Token;

        try
        {
            // 攻撃アニメーションをトリガーする（Animator 側が Attack1→2→3 のコンボを処理する）
            TriggerAnim(_animAttackTrigger);
            SetAnimParams(speed: 0f, alert: true);

            if (_useAnimationEvents)
            {
                // ── Animation Event モード ────────────────────────────
                // ActivateHitbox() / DeactivateHitbox() は各 Attack クリップの
                // Animation Event から呼ばれる。ここではコンボ総尺 + 硬直だけ待つ。
                float waitSec = _comboDuration + _attackRecovery;
                if (waitSec > 0f)
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(waitSec),
                        DelayType.Realtime, PlayerLoopTiming.Update, ct);
            }
            else
            {
                // ── コードベース タイミングモード ────────────────────
                var hits = (_comboHits != null && _comboHits.Length > 0)
                    ? _comboHits
                    : new[] { new HitTiming { windup = 0.25f, activeTime = 0.20f } };

                for (int i = 0; i < hits.Length; i++)
                {
                    // 振りかぶり：ヒットボックスは無効のまま待つ
                    if (hits[i].windup > 0f)
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(hits[i].windup),
                            DelayType.Realtime, PlayerLoopTiming.Update, ct);

                    // ヒットボックス有効（OnEnable で前段のヒットフラグが自動リセットされる）
                    ActivateAllHitboxes();

                    if (hits[i].activeTime > 0f)
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(hits[i].activeTime),
                            DelayType.Realtime, PlayerLoopTiming.Update, ct);

                    // ヒットボックス無効（次の段の前に必ず落とす）
                    DeactivateAllHitboxes();
                }

                // コンボ全段終了後の硬直
                if (_attackRecovery > 0f)
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_attackRecovery),
                        DelayType.Realtime, PlayerLoopTiming.Update, ct);
            }
        }
        catch (OperationCanceledException) { /* Stagger / Die によるキャンセル */ }
        finally
        {
            // 中断されてもヒットボックスを必ず無効化してクールダウンをセットする
            DeactivateAllHitboxes();
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
        DeactivateAllHitboxes();

        _aiState      = AIState.Staggered;
        _staggerTimer = _staggerDuration;

        TriggerAnim(_animStaggerTrigger);
    }

    // ─────────────────────────────────────────────────────────────
    //  死亡通知（EnemyStats から呼ばれる）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// EnemyStats.Die() から <c>enabled = false</c> の直前に呼ばれる。<br/>
    /// AIを即座に停止し、死亡アニメーションを再生する。<br/>
    /// Animator コンポーネントは MonoBehaviour の enabled に関係なく動き続けるので、
    /// このメソッドを呼んだ後に enabled = false にしても Die アニメーションは最後まで再生される。
    /// </summary>
    public void NotifyDeath()
    {
        // 攻撃シーケンスをキャンセルしてヒットボックスを無効化する
        _attackCts?.Cancel();
        DeactivateAllHitboxes();

        _aiState = AIState.Dead;

        // Alert ブールを落として locomotion が干渉しないようにする
        SetAnimParams(speed: 0f, alert: false);

        // Die トリガーを発火する（Any State → Die 遷移が走る）
        TriggerAnim(_animDieTrigger);
    }

    // ─────────────────────────────────────────────────────────────
    //  Animation Event から呼ぶメソッド（MeleeAnimEventRelay 経由）
    //  使い方:
    //    Attack 1 clip (左パンチ) → ActivateLeftHitbox  / DeactivateHitbox
    //    Attack 2 clip (右パンチ) → ActivateRightHitbox / DeactivateHitbox
    //    Attack 3 clip (左パンチ) → ActivateLeftHitbox  / DeactivateHitbox
    // ─────────────────────────────────────────────────────────────

    /// <summary>Animation Event: 左拳ヒットボックスを有効にする。</summary>
    public void ActivateLeftHitbox()
    {
        if (_aiState != AIState.Attacking) return;
        _leftHitboxRoot?.SetActive(true);
    }

    /// <summary>Animation Event: 右拳ヒットボックスを有効にする。</summary>
    public void ActivateRightHitbox()
    {
        if (_aiState != AIState.Attacking) return;
        _rightHitboxRoot?.SetActive(true);
    }

    /// <summary>Animation Event: 全ヒットボックスを無効にする（拳が引くタイミング）。</summary>
    public void DeactivateHitbox() => DeactivateAllHitboxes();

    // ─────────────────────────────────────────────────────────────
    //  内部ヒットボックス補助
    // ─────────────────────────────────────────────────────────────

    private void ActivateAllHitboxes()
    {
        _leftHitboxRoot?.SetActive(true);
        _rightHitboxRoot?.SetActive(true);
    }

    private void DeactivateAllHitboxes()
    {
        _leftHitboxRoot?.SetActive(false);
        _rightHitboxRoot?.SetActive(false);
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
