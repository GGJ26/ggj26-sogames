using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement instance;

    [Header("Refs")]
    public Rigidbody2D rb;

    [Header("Move")]
    public float moveSpeed = 6f;

    [Header("Jump")]
    public float jumpForce = 8f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundMask;

    private float _moveX; // -1 (left), 0 (idle), +1 (right)

    private void Awake()
    {
        instance = this;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // conserve la vitesse verticale, applique seulement l'horizontale
        var v = rb.linearVelocity;
        v.x = _moveX * moveSpeed;
        rb.linearVelocity = v;
    }

    public void MoveLeft()
    {
        _moveX = -1f;
    }

    public void MoveRight()
    {
        _moveX = 1f;
    }

    public void StopMove()
    {
        _moveX = 0f;
    }

    public void TryJump()
    {
        if (rb == null) return;
        if (!IsGrounded()) return;

        // reset Y pour un saut constant
        var v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    private bool IsGrounded()
    {
        if (groundCheck == null) return false;

        return Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundMask
        );
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
#endif
}
