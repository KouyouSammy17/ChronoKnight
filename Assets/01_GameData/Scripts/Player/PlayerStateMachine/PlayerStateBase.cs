public abstract class PlayerStateBase : IPlayerState
{
    public abstract PlayerStateID ID { get; }

    public virtual void Enter(PlayerStateMachineBrain brain) { }
    public virtual void Exit(PlayerStateMachineBrain brain) { }
    public virtual void Tick(PlayerStateMachineBrain brain) { }
    public virtual void FixedTick(PlayerStateMachineBrain brain) { }
}
