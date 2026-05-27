// プレイヤーのモードステート（通常・ターボ等）の基底インターフェース
/// <summary>
/// プレイヤーのモードステート（例：通常・ターボ）のインターフェース。
/// モードレベルのステート管理をアクションステートから分離する。
/// </summary>
public interface IPlayerModeState
{
    /// <summary>このモードを識別する固有ID</summary>
    PlayerModeID ID { get; } // このモードを識別するID

    /// <summary>このモードへ入るときに呼ばれる</summary>
    void Enter(PlayerStateMachineBrain brain); // モード開始時に呼ばれる

    /// <summary>このモードから出るときに呼ばれる</summary>
    void Exit(PlayerStateMachineBrain brain); // モード終了時に呼ばれる

    /// <summary>このモードの間、毎フレーム呼ばれる</summary>
    void Tick(PlayerStateMachineBrain brain); // 毎フレーム呼ばれる更新処理
}
