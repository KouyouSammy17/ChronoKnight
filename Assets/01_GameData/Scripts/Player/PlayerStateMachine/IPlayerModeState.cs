/// <summary>
/// Interface for player mode states (e.g., Normal, Turbo).
/// Separates mode-level state management from action states.
/// </summary>
public interface IPlayerModeState
{
    /// <summary>Unique identifier for this mode</summary>
    PlayerModeID ID { get; }

    /// <summary>Called when entering this mode</summary>
    void Enter(PlayerStateMachineBrain brain);

    /// <summary>Called when exiting this mode</summary>
    void Exit(PlayerStateMachineBrain brain);

    /// <summary>Called every frame while in this mode</summary>
    void Tick(PlayerStateMachineBrain brain);
}
