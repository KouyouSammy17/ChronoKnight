// プレイヤーのステートマシンにおける全ステートの基底インターフェース
/// <summary>
/// プレイヤーのステートマシンにおける全ステートのインターフェース。
/// ステートの振る舞いのライフサイクルと更新メソッドを定義する。
/// </summary>
public interface IPlayerState
{
    /// <summary>このステートを識別する固有ID</summary>
    PlayerStateID ID { get; } // このステートを識別するID

    /// <summary>このステートへ遷移するときに呼ばれる</summary>
    void Enter(PlayerStateMachineBrain brain); // ステート開始時に呼ばれる

    /// <summary>このステートから遷移するときに呼ばれる</summary>
    void Exit(PlayerStateMachineBrain brain); // ステート終了時に呼ばれる

    /// <summary>このステートの間、毎フレーム呼ばれる</summary>
    void Tick(PlayerStateMachineBrain brain); // 毎フレーム呼ばれる更新処理

    /// <summary>このステートの間、毎物理フレーム呼ばれる</summary>
    void FixedTick(PlayerStateMachineBrain brain); // 毎物理フレーム呼ばれる更新処理
}
