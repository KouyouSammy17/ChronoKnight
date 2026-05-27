// 移動する足場のプレイヤーへの速度引き継ぎを処理するスクリプト
using UnityEngine;

/// <summary>
/// 移動する足場からプレイヤーへの速度引き継ぎを処理する。
/// プレイヤーが移動足場の上に立っているときを検出し、その速度を適用する。
/// ターボモード中のみ動作するようにゲートを設定できる。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlatformCarryInjector : MonoBehaviour
{
    /// <summary>プレイヤーのモーターコンポーネントへの参照</summary>
    [Header("Refs")]
    [SerializeField] private PlayerMotor _motor; // プレイヤーのモーターコンポーネント

    /// <summary>上面接触を検出するドット積の閾値（上に乗っていることを判定）</summary>
    [Header("Top contact filter")]
    [SerializeField] private float _topContactDot = 0.75f; // 上面接触を判定するドット積の閾値

    /// <summary>有効にすると、ターボモード中のみ足場の速度引き継ぎが適用される</summary>
    [Header("Turbo gate")]
    [SerializeField] private bool _onlyInTurbo = true; // ターボモード中のみ足場の速度を引き継ぐか

    /// <summary>現在プレイヤーを乗せている足場への参照</summary>
    private SuperFastPlatform _platform; // 現在プレイヤーを乗せている足場
    /// <summary>今の物理フレームで足場の上面接触が検出されたか</summary>
    private bool _topContactThisStep; // 今の物理フレームで上面接触を検出したか

    /// <summary>ターボモードが現在アクティブかどうかを確認する</summary>
    private bool TurboActive =>
        TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;

    /// <summary>未アサインの場合、モーターの参照を初期化する</summary>
    private void Awake()
    {
        if (_motor == null) _motor = GetComponent<PlayerMotor>();
    }

    /// <summary>
    /// 上方向からの移動足場との衝突を検出する。
    /// 足場の参照を更新し、上面接触の状態を記録する。
    /// </summary>
    private void OnCollisionStay(Collision c)
    {
        _topContactThisStep = false; // フレームごとにリセット

        if (_onlyInTurbo && !TurboActive)
        {
            DetachIfThisPlatform(c); // ターボが無効なら足場から切り離す
            return;
        }

        if (c.collider.TryGetComponent(out SuperFastPlatform plat))
        {
            foreach (var cp in c.contacts)
            {
                if (Vector3.Dot(cp.normal, Vector3.up) > _topContactDot) // 上面接触か確認
                {
                    _platform = plat; // 乗っている足場を記録
                    _topContactThisStep = true; // このフレームで上面接触を検出
                    return;
                }
            }
        }
    }

    /// <summary>衝突が終了したとき、足場から切り離す</summary>
    private void OnCollisionExit(Collision c)
    {
        DetachIfThisPlatform(c);
    }

    /// <summary>
    /// 毎物理フレーム、足場の速度をプレイヤーに適用する。
    /// 足場の移動量を水平面のみの速度に変換する（2.5D用）。
    /// </summary>
    private void FixedUpdate()
    {
        if (_motor == null) return;

        // ターボ終了 => 速度引き継ぎを停止
        if (_onlyInTurbo && !TurboActive)
        {
            _platform = null;
            _motor.ClearPlatformCarryVelocity(); // ターボ終了時に足場速度をクリア
            return;
        }

        if (_platform == null || !_topContactThisStep)
        {
            _platform = null;
            _motor.ClearPlatformCarryVelocity(); // 足場に乗っていない場合は速度をクリア
            return;
        }

        // 足場の移動量を速度に変換する（累積なし！）
        // PlatformDeltaは足場のFixedUpdateで計算される。
        Vector3 carryVel = _platform.PlatformDelta / Time.fixedDeltaTime; // 足場の移動量を速度に変換

        // 2.5D: X/Zのみ引き継ぐ
        carryVel.y = 0f; // 垂直速度は引き継がない（2.5D用）

        _motor.SetPlatformCarryVelocity(carryVel); // プレイヤーに足場の速度を適用
    }

    /// <summary>
    /// 指定した衝突が現在の足場との衝突であれば、足場から切り離す。
    /// </summary>
    private void DetachIfThisPlatform(Collision c)
    {
        if (_platform != null && c.collider.GetComponent<SuperFastPlatform>() == _platform)
        {
            _platform = null;
            if (_motor != null) _motor.ClearPlatformCarryVelocity();
        }
    }
}
