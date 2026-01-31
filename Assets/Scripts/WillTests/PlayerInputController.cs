using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour
{
    private PlayerControls _controls;
    private Vector2 _move;

    private void Awake()
    {
        _controls = new PlayerControls();

        _controls.Player.Move.performed += ctx => {
            _move = ctx.ReadValue<Vector2>();
            Debug.Log(_move);
        };
        _controls.Player.Move.canceled  += _ => _move = Vector2.zero;

        _controls.Player.Jump.performed += _ => TryJump();

        _controls.Player.Mask1.performed += _ => MaskManager.instance.SetMask("mask1");
        _controls.Player.Mask2.performed += _ => MaskManager.instance.SetMask("mask2");
        _controls.Player.Mask3.performed += _ => MaskManager.instance.SetMask("mask3");
    }

    private void MoveLeft()
    {
        
    }

    private void MoveRight()
    {
        
    }

    private void MoveUp()
    {
        
    }

    private void MoveDown()
    {
        
    }

    private void TryJump()
    {
        Debug.Log("Try Jump");
    }

    private void OnEnable()  => _controls.Enable();
    private void OnDisable() => _controls.Disable();
}
