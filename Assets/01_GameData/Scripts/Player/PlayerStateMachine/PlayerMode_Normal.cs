public class PlayerMode_Normal : IPlayerModeState
{
    public PlayerModeID ID => PlayerModeID.Normal;
    public void Enter(PlayerStateMachineBrain brain) { }
    public void Exit(PlayerStateMachineBrain brain) { }
    public void Tick(PlayerStateMachineBrain brain) { }
}
