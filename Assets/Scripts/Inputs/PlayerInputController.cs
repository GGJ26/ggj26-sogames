using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour
{
    private PlayerControls _controls;
    private Vector2 _move;

    private enum Dir { None, Left, Right }
    private Dir _currentDir = Dir.None;

    private void Awake()
    {
        _controls = new PlayerControls();

        _controls.Player.Move.performed += ctx =>
        {
            _move = ctx.ReadValue<Vector2>();
            PlayerMovement.instance.MoveInput = _move;

            /* ApplyHorizontalMove(_move.x); */
        };

        _controls.Player.Move.canceled += _ =>
        {
            _move = Vector2.zero;
            SetDir(Dir.None);
        };

        _controls.Player.Jump.performed += _ => TryJump();

        _controls.Player.Mask1.performed += _ => MaskManager.instance.SetMask("mask1");
        _controls.Player.Mask2.performed += _ => MaskManager.instance.SetMask("mask2");
        _controls.Player.Mask3.performed += _ => MaskManager.instance.SetMask("mask3");
    }

    private void ApplyHorizontalMove(float x)
    {
        // zone morte pour éviter le bruit clavier / stick
        if (Mathf.Abs(x) < 0.01f)
        {
            SetDir(Dir.None);
            return;
        }

        SetDir(x < 0f ? Dir.Left : Dir.Right);
    }

    private void SetDir(Dir newDir)
    {
        if (_currentDir == newDir) return;
        _currentDir = newDir;

        switch (_currentDir)
        {
            case Dir.Left:
                MoveLeft();
                break;
            case Dir.Right:
                MoveRight();
                break;
            default:
                StopMove();
                break;
        }
    }

    private void MoveLeft()
    {
        PlayerMovement.instance.MoveLeft();
    }

    private void MoveRight()
    {
        PlayerMovement.instance.MoveRight();
    }

    private void StopMove()
    {
        PlayerMovement.instance.StopMove();
    }

    private void TryJump()
    {
        PlayerMovement.instance.TryJump();
    }

    private void OnEnable()  => _controls.Enable();
    private void OnDisable() => _controls.Disable();
}
