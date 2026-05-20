/// <summary>
/// Interface for all player state machine states.
/// Defines the lifecycle and update methods for state behavior.
/// </summary>
public interface IPlayerState
{
    /// <summary>Unique identifier for this state</summary>
    PlayerStateID ID { get; }

    /// <summary>Called when transitioning into this state</summary>
    void Enter(PlayerStateMachineBrain brain);

    /// <summary>Called when transitioning out of this state</summary>
    void Exit(PlayerStateMachineBrain brain);

    /// <summary>Called every frame while in this state</summary>
    void Tick(PlayerStateMachineBrain brain);

    /// <summary>Called every physics frame while in this state</summary>
    void FixedTick(PlayerStateMachineBrain brain);
}
