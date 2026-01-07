public interface IPlayerState
{
    PlayerStateID ID { get; }
    void Enter(PlayerStateMachineBrain brain);
    void Exit(PlayerStateMachineBrain brain);
    void Tick(PlayerStateMachineBrain brain);
    void FixedTick(PlayerStateMachineBrain brain);
}
