// ターボモードのステート（TimeScaleや速度倍率の変更はTurboModeManagerが担当）
public class PlayerMode_Turbo : IPlayerModeState
{
    public PlayerModeID ID => PlayerModeID.Turbo; // このモードのID

    // 重要：
    // ここでTimeScaleや移動倍率を変更しないこと。
    // TurboModeManagerが既に処理している。
    public void Enter(PlayerStateMachineBrain brain) { } // ターボモード開始時の処理（TimeScale変更はTurboModeManagerへ委譲）
    public void Exit(PlayerStateMachineBrain brain) { } // ターボモード終了時の処理（現状なし）
    public void Tick(PlayerStateMachineBrain brain) { } // 毎フレーム処理（現状なし）
}
