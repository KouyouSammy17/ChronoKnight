// IPlayerStateを実装するステートの抽象基底クラス（デフォルト実装を提供）
public abstract class PlayerStateBase : IPlayerState
{
    public abstract PlayerStateID ID { get; } // 各サブクラスで固有のIDを定義する

    public virtual void Enter(PlayerStateMachineBrain brain) { } // ステート開始時の処理（必要に応じてオーバーライド）
    public virtual void Exit(PlayerStateMachineBrain brain) { } // ステート終了時の処理（必要に応じてオーバーライド）
    public virtual void Tick(PlayerStateMachineBrain brain) { } // 毎フレーム更新（必要に応じてオーバーライド）
    public virtual void FixedTick(PlayerStateMachineBrain brain) { } // 毎物理フレーム更新（必要に応じてオーバーライド）
}
