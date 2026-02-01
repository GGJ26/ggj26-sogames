using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    public Animator animator;

    public void SetJump()
    {
        animator.SetTrigger("jump");
    }

    public void SetRun()
    {
        animator.SetTrigger("run");
    }

    public void SetIdle()
    {
        animator.SetTrigger("idle");
    }

    public void SetGravitySwitch()
    {
        animator.SetTrigger("gravitySwitch");
    }
}
