// プレイヤーのステートマシンにおける全ステートの基底インターフェース
/// <summary>
/// Interface for all player state machine states.
/// Defines the lifecycle and update methods for state behavior.
/// </summary>
public interface IPlayerState
{
    /// <summary>Unique identifier for this state</summary>
    PlayerStateID ID { get; } // このステートを識別するID

    /// <summary>Called when transitioning into this state</summary>
    void Enter(PlayerStateMachineBrain brain); // ステート開始時に呼ばれる

    /// <summary>Called when transitioning out of this state</summary>
    void Exit(PlayerStateMachineBrain brain); // ステート終了時に呼ばれる

    /// <summary>Called every frame while in this state</summary>
    void Tick(PlayerStateMachineBrain brain); // 毎フレーム呼ばれる更新処理

    /// <summary>Called every physics frame while in this state</summary>
    void FixedTick(PlayerStateMachineBrain brain); // 毎物理フレーム呼ばれる更新処理
}
