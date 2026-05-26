// プレイヤーのモードステート（通常・ターボ等）の基底インターフェース
/// <summary>
/// Interface for player mode states (e.g., Normal, Turbo).
/// Separates mode-level state management from action states.
/// </summary>
public interface IPlayerModeState
{
    /// <summary>Unique identifier for this mode</summary>
    PlayerModeID ID { get; } // このモードを識別するID

    /// <summary>Called when entering this mode</summary>
    void Enter(PlayerStateMachineBrain brain); // モード開始時に呼ばれる

    /// <summary>Called when exiting this mode</summary>
    void Exit(PlayerStateMachineBrain brain); // モード終了時に呼ばれる

    /// <summary>Called every frame while in this mode</summary>
    void Tick(PlayerStateMachineBrain brain); // 毎フレーム呼ばれる更新処理
}
