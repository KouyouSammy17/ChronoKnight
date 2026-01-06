public class PlayerMode_Turbo : IPlayerModeState
{
    public PlayerModeID ID => PlayerModeID.Turbo;

    // IMPORTANT:
    // Do NOT change timeScale or movement multipliers here.
    // TurboModeManager already does that.
    public void Enter(PlayerStateMachineBrain brain) { }
    public void Exit(PlayerStateMachineBrain brain) { }
    public void Tick(PlayerStateMachineBrain brain) { }
}
