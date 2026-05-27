// 2点間を高速で往復する動く足場の挙動を制御するスクリプト
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class SuperFastPlatform : MonoBehaviour
{
    [Header("Movement Points")]
    [SerializeField] private Transform pointA;  // 往復の折り返し点A
    [SerializeField] private Transform pointB;  // 往復の折り返し点B

    [Header("Timing")]
    [SerializeField] private float travelTime = 0.7f;   // A→B（またはB→A）にかかる時間（秒）
    [SerializeField] private float pauseAtEnds = 0f;    // 折り返し点で一時停止する時間

    [Header("Speed Curve")]
    [SerializeField] private AnimationCurve easing;     // 移動速度のイージングカーブ

    private Rigidbody rb;           // 物理演算コンポーネント
    private Vector3 posA, posB;     // ワールド座標でのA・B位置
    private float t;                // 0〜1の正規化された移動進捗
    private int direction = +1;     // 移動方向（+1:A→B, -1:B→A）
    private float pauseTimer;       // 一時停止の残り時間

    public Vector3 PlatformVelocity { get; private set; }  // 現在フレームの速度ベクトル
    public Vector3 PlatformDelta { get; private set; }     // 1フィジクスステップあたりの移動量

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;                                                  // キネマティックで物理シミュレーションは受けない
        rb.interpolation = RigidbodyInterpolation.Interpolate;                  // 補間で滑らかに表示

        // 高速なキネマティックボディが接触を見逃さないようにする
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 高速時の接触抜けを防ぐ
    }

    private void Start()
    {
        if (!pointA || !pointB) { enabled = false; return; }    // ウェイポイントが未設定なら無効化
        posA = pointA.position;
        posB = pointB.position;
        transform.position = posA;  // 開始位置をAに設定
    }

    private void FixedUpdate()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;  // 一時停止タイマーを減算
            PlatformVelocity = Vector3.zero;
            PlatformDelta = Vector3.zero;
            return;
        }

        Vector3 prevPos = rb.position;  // 前のフレームの位置を保存

        float dtNorm = Time.fixedDeltaTime / Mathf.Max(travelTime, 0.001f); // 1ステップあたりの正規化移動量
        t = Mathf.Clamp01(t + dtNorm * direction);  // 移動進捗を方向に応じて更新

        float easedT = (easing != null && easing.keys.Length > 0) ? easing.Evaluate(t) : t;    // イージングカーブを適用

        Vector3 target = Vector3.Lerp(posA, posB, easedT); // イージング後のtでA・B間を線形補間

        PlatformDelta = target - prevPos;                       // 今回の移動量を計算
        PlatformVelocity = PlatformDelta / Time.fixedDeltaTime; // フレームレートに依存しない速度を算出

        rb.MovePosition(target);    // キネマティックリジッドボディを目標位置へ移動

        if (t <= 0f || t >= 1f)
        {
            direction *= -1;                                // 折り返し点に到達したら方向を反転
            if (pauseAtEnds > 0f) pauseTimer = pauseAtEnds; // 一時停止が設定されていればタイマーをセット
        }
    }
}
