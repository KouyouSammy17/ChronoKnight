// プレイヤーが攻撃（コンボ）を実行しているときの状態を管理するステート
public class PlayerState_Attack : PlayerStateBase
{
    public override PlayerStateID ID => PlayerStateID.Attack;

    public override void Tick(PlayerStateMachineBrain brain)
    {
        // keep buffering move while input is locked (combat uses this)
        brain.Motor.InputMove(brain.Input.Move); // 入力ロック中も移動入力をバッファ（コンボ後に使用）

        // allow buffering further attacks during combo
        if (brain.Input.ConsumeAttackPressed())
            brain.Combat?.RequestAttack(); // コンボ中の次の攻撃入力をバッファ

        // Freeze in air while attacking
        bool shouldHang = !brain.Motor.IsGrounded; // Attack state only runs while combo is active 空中ならハングを有効化
        brain.Motor.SetAirComboHang(shouldHang); // 空中コンボ中はY速度をフリーズ

        brain.Motor.MotorUpdate(false, true, false); // ジャンプ禁止・モメンタム獲得許可・壁スライド禁止
    }
    public override void FixedTick(PlayerStateMachineBrain brain)
    {
        brain.Motor.MotorFixedUpdate(allowHorizontalMovement: false); // 攻撃中は水平移動を禁止
    }
    public override void Exit(PlayerStateMachineBrain brain)
    {
        brain.Motor.SetAirComboHang(false); // 攻撃終了時に空中コンボハングを解除
    }

}
