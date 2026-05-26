// プレイヤーのステートマシンで使用する全アクションステートのID列挙体
/// <summary>
/// Enumeration of all player action states in the state machine.
/// </summary>
public enum PlayerStateID
{
    /// <summary>Player is standing or moving on ground</summary>
    Grounded,   // 地上にいる（静止・移動含む）
    /// <summary>Player is in the air (jumping or falling)</summary>
    Airborne,   // 空中にいる（ジャンプ・落下含む）
    /// <summary>Player is sliding on a wall</summary>
    WallSlide,  // 壁をスライドしている
    /// <summary>Player is performing a dash move</summary>
    Dash,       // ダッシュ中
    /// <summary>Player is performing a melee attack</summary>
    Attack,     // 近接攻撃（コンボ）中
    /// <summary>Player is performing a dash attack</summary>
    DashAttack, // ダッシュ攻撃中
    /// <summary>Player is in hitstun from taking damage</summary>
    Hurt,       // ダメージによるヒットスタン中
    /// <summary>Player is in knockdown state (air-to-ground sequence)</summary>
    Knockdown,  // ノックダウン状態（空中→地面への連続処理）
    /// <summary>Player is playing turbo mode activation sequence</summary>
    TurboStart, // ターボモード起動アニメーション中
    /// <summary>Player is dead</summary>
    Dead,       // 死亡状態
    /// <summary>Player has won the level</summary>
    Win         // ステージクリア（勝利）状態
}
