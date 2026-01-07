public interface IPlayerModeState
{
    PlayerModeID ID { get; }
    void Enter(PlayerStateMachineBrain brain);
    void Exit(PlayerStateMachineBrain brain);
    void Tick(PlayerStateMachineBrain brain);
}
