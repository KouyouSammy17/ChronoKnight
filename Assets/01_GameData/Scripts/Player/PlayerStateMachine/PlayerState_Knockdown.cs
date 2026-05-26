// プレイヤーがノックダウン（吹き飛び→着地）状態のときを管理するステート
public class PlayerState_Knockdown : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Knockdown;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // no input buffering here: you are "down"
        // ノックダウン中は全入力を無視（完全な無力状態）
        // no input buffering here (you�fre �gdown�h)
        brain.Motor.MotorUpdate(false, false, false); // ジャンプ・モメンタム・壁スライド全て禁止
    }

    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        // allow knockback while airborne, but stop sliding once grounded
        bool allow = !brain.Motor.IsGrounded; // 空中ならノックバック用の水平移動を許可
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: allow);
    }
}
