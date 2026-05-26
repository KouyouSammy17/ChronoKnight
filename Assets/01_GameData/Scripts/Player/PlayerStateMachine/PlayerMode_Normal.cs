// 通常モードのステート（特別な処理なし。ターボモードと対をなす）
public class PlayerMode_Normal : IPlayerModeState
{
    public PlayerModeID ID => PlayerModeID.Normal; // このモードのID
    public void Enter(PlayerStateMachineBrain brain) { } // 通常モード開始時の処理（現状なし）
    public void Exit(PlayerStateMachineBrain brain) { } // 通常モード終了時の処理（現状なし）
    public void Tick(PlayerStateMachineBrain brain) { } // 毎フレーム処理（現状なし）
}
