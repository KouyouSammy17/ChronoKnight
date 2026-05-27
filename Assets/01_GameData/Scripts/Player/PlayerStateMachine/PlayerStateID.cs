// プレイヤーのステートマシンで使用する全アクションステートのID列挙体
/// <summary>
/// ステートマシンにおけるプレイヤーの全アクションステートの列挙体。
/// </summary>
public enum PlayerStateID
{
    /// <summary>プレイヤーが地上で静止・移動している</summary>
    Grounded,   // 地上にいる（静止・移動含む）
    /// <summary>プレイヤーが空中にいる（ジャンプ・落下）</summary>
    Airborne,   // 空中にいる（ジャンプ・落下含む）
    /// <summary>プレイヤーが壁をスライドしている</summary>
    WallSlide,  // 壁をスライドしている
    /// <summary>プレイヤーがダッシュを実行中</summary>
    Dash,       // ダッシュ中
    /// <summary>プレイヤーが近接攻撃（コンボ）を実行中</summary>
    Attack,     // 近接攻撃（コンボ）中
    /// <summary>プレイヤーがダッシュ攻撃を実行中</summary>
    DashAttack, // ダッシュ攻撃中
    /// <summary>プレイヤーがダメージによるヒットスタン中</summary>
    Hurt,       // ダメージによるヒットスタン中
    /// <summary>プレイヤーがノックダウン状態（空中→地面への連続処理）</summary>
    Knockdown,  // ノックダウン状態（空中→地面への連続処理）
    /// <summary>プレイヤーがターボモード起動アニメーションを再生中</summary>
    TurboStart, // ターボモード起動アニメーション中
    /// <summary>プレイヤーが死亡している</summary>
    Dead,       // 死亡状態
    /// <summary>プレイヤーがステージをクリアした</summary>
    Win         // ステージクリア（勝利）状態
}
