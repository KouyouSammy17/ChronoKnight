using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputRouter : MonoBehaviour
{
    public Vector2 Move { get; private set; }
    public bool JumpHeld { get; private set; }

    bool _jumpPressed;
    bool _jumpReleased;
    bool _dashPressed;
    bool _attackPressed;
    bool _turboPressed;

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Move = ctx.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            _jumpPressed = true;
            JumpHeld = true;
        }
        else if (ctx.canceled)
        {
            _jumpReleased = true;
            JumpHeld = false;
        }
    }

    public void OnDash(InputAction.CallbackContext ctx)
    {
        if (ctx.started) _dashPressed = true;
    }

    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (ctx.started) _attackPressed = true;
    }

    public void OnTurbo(InputAction.CallbackContext ctx)
    {
        if (ctx.started) _turboPressed = true;
    }
    // one-frame edge consumption
    public bool ConsumeJumpPressed() { var v = _jumpPressed; _jumpPressed = false; return v; }
    public bool ConsumeJumpReleased() { var v = _jumpReleased; _jumpReleased = false; return v; }
    public bool ConsumeDashPressed() { var v = _dashPressed; _dashPressed = false; return v; }
    public bool ConsumeAttackPressed() { var v = _attackPressed; _attackPressed = false; return v; }
    public bool ConsumeTurboPressed()
    {
        var v = _turboPressed;
        _turboPressed = false;
        return v;
    }

    public void ClearAll()
    {
        Move = Vector2.zero;
       ClearPressedBuffers();
    }
    public void ClearPressedBuffers()
    {
        _attackPressed = false;
        _dashPressed = false;
        _jumpPressed = false;
        _turboPressed = false;
        JumpHeld = false;
    }
}
