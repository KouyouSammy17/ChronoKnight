// プレイヤーの入力をInputSystemから受け取り、各システムに配布するルータースクリプト
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputRouter : MonoBehaviour
{
    public Vector2 Move { get; private set; } // 移動入力の現在値
    public bool JumpHeld { get; private set; } // ジャンプボタンを押し続けているか

    bool _jumpPressed; // このフレームでジャンプが押された（ワンショット）
    bool _jumpReleased; // このフレームでジャンプが離された（ワンショット）
    bool _dashPressed; // このフレームでダッシュが押された（ワンショット）
    bool _attackPressed; // このフレームで攻撃が押された（ワンショット）
    bool _turboPressed; // このフレームでターボが押された（ワンショット）

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Move = ctx.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            _jumpPressed = true; // ジャンプ押下フラグを立てる
            JumpHeld = true; // ホールド状態を有効化
        }
        else if (ctx.canceled)
        {
            _jumpReleased = true; // ジャンプ離しフラグを立てる
            JumpHeld = false; // ホールド状態を解除
        }
    }

    public void OnDash(InputAction.CallbackContext ctx)
    {
        if (ctx.started) _dashPressed = true; // ダッシュ押下フラグを立てる
    }

    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (ctx.started) _attackPressed = true; // 攻撃押下フラグを立てる
    }

    public void OnTurbo(InputAction.CallbackContext ctx)
    {
        if (ctx.started) _turboPressed = true; // ターボ押下フラグを立てる
    }
    // one-frame edge consumption（各入力は1フレームで消費される）
    public bool ConsumeJumpPressed() { var v = _jumpPressed; _jumpPressed = false; return v; } // ジャンプ押下を消費して返す
    public bool ConsumeJumpReleased() { var v = _jumpReleased; _jumpReleased = false; return v; } // ジャンプ離しを消費して返す
    public bool ConsumeDashPressed() { var v = _dashPressed; _dashPressed = false; return v; } // ダッシュ押下を消費して返す
    public bool ConsumeAttackPressed() { var v = _attackPressed; _attackPressed = false; return v; } // 攻撃押下を消費して返す
    public bool ConsumeTurboPressed()
    {
        var v = _turboPressed;
        _turboPressed = false; // ターボ押下を消費してリセット
        return v;
    }

    public void ClearAll()
    {
        Move = Vector2.zero; // 移動入力をクリア
       ClearPressedBuffers(); // 全てのバッファされた入力をクリア
    }
    public void ClearPressedBuffers()
    {
        _attackPressed = false;
        _dashPressed = false;
        _jumpPressed = false;
        _turboPressed = false;
        JumpHeld = false; // ホールド状態もリセット
    }
}
