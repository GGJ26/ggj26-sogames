using System.Collections;
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

    [Header("Grounding")]
    [Tooltip("Durée pendant laquelle on ignore le sol après un jump (évite le glitch 1-2 frames).")]
    public float groundIgnoreAfterJump = 0.08f;
    private float _groundLockUntil = -1f;

    [Header("Gravity Modes")]
    [Tooltip("Gravité de base (valeur positive). Le signe est géré automatiquement pour l'inversion.")]
    public float baseGravityScale = 1.5f;

    [Tooltip("Multiplier de gravité quand on tombe (normal). 1 = normal, >1 chute plus rapide.")]
    public float fallMultiplierNormal = 1.5f;

    [Tooltip("Multiplier de gravité quand on tombe (flottant). <1 = chute plus lente.")]
    public float fallMultiplierFloat = 0.2f;

    private float _moveX; // -1 (left), 0 (idle), +1 (right)

    // States liés aux masques
    private bool _slowFallEnabled;
    private bool _invertGravityEnabled;

    // Cache pour reset
    private float _defaultBaseGravityScale;

    [Header("Visual / Anim")]
    public Transform container;                // Parent que tu retournes (flip X/Y)
    public PlayerAnimator playerAnimator;

    // Grounded cache (pour éviter plusieurs Overlap par frame)
    private bool _isGrounded;
    private bool _wasGrounded;

    public ZoneTracker2D zoneTracker2D;

    private void Awake()
    {
        instance = this;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        _defaultBaseGravityScale = baseGravityScale;

        // Init grounded states
        _isGrounded = IsGroundedInternal();
        _wasGrounded = _isGrounded;
    }

    #region Adding Listeners
    private void OnEnable()
    {
        StartCoroutine(SetMaskChanged());
    }

    private IEnumerator SetMaskChanged()
    {
        yield return null;
        MaskManager.instance.AddMaskListener(OnMaskChanged);
        OnMaskChanged();
    }

    private void OnDisable()
    {
        if (MaskManager.instance != null)
            MaskManager.instance.RemoveMaskListener(OnMaskChanged);
    }
    #endregion

    private void OnMaskChanged()
    {
        ResetMasks();

        switch (MaskManager.instance.selectedMask.id)
        {
            case "mask1":
                HandleLowGravityJump();      // flottant (descente ralentie)
                break;

            case "mask2":
                HandleInvertedGravity();     // gravité inversée
                break;

            case "mask3":
                HandlePhaseThrough();
                break;
        }
    }

    // 1) Descente ralentie (même hauteur de saut, chute plus lente)
    private void HandleLowGravityJump()
    {
        _slowFallEnabled = true;
    }

    // 2) Gravité inversée
    private void HandleInvertedGravity()
    {
        _invertGravityEnabled = true;

        // Retournement vertical du transform (visuel)
        if (container != null)
        {
            var s = container.localScale;
            s.y = -Mathf.Abs(s.y);
            container.localScale = s;
        }
    }

    private void HandlePhaseThrough()
    {
        // TODO
    }

    private void ResetMasks()
    {
        _slowFallEnabled = false;
        _invertGravityEnabled = false;

        baseGravityScale = _defaultBaseGravityScale;

        // Reset flip vertical
        if (container != null)
        {
            var s = container.localScale;
            s.y = Mathf.Abs(s.y);
            container.localScale = s;
        }

        // Reset gravité effective (sera recalculée en FixedUpdate)
        if (rb != null)
            rb.gravityScale = baseGravityScale;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // --- Grounded cache ---
        _wasGrounded = _isGrounded;
        _isGrounded = IsGroundedInternal();

        // Transitions d'anim basées sur grounded (plus fiable que timers)
        if (!_wasGrounded && _isGrounded)
        {
            // atterrissage
            if (_moveX != 0f) playerAnimator?.SetRun();
            else playerAnimator?.SetIdle();
        }
        else if (_wasGrounded && !_isGrounded)
        {
            // départ en l'air (marche aussi si tu tombes d'une plateforme)
            playerAnimator?.SetJump();
        }

        // --- Mouvement horizontal ---
        var v = rb.linearVelocity;
        v.x = _moveX * moveSpeed;
        rb.linearVelocity = v;

        // --- Gravité effective ---
        float signedBase = Mathf.Abs(baseGravityScale) * (_invertGravityEnabled ? -1f : 1f);

        bool fallingWithGravity =
            signedBase > 0f ? (rb.linearVelocity.y < 0f)  // gravité normale => tombe si velY < 0
                            : (rb.linearVelocity.y > 0f); // gravité inversée => tombe (vers le haut) si velY > 0

        float fallMult = _slowFallEnabled ? fallMultiplierFloat : fallMultiplierNormal;

        // Appliquer : montée inchangée, chute modifiée
        rb.gravityScale = fallingWithGravity ? (signedBase * fallMult) : signedBase;
    }

    public void MoveLeft()
    {
        _moveX = -1f;

        // flip horizontal
        if (container != null)
        {
            var s = container.localScale;
            s.x = -Mathf.Abs(s.x);
            container.localScale = s;
        }

        if (_isGrounded)
            playerAnimator?.SetRun();
    }

    public void MoveRight()
    {
        _moveX = 1f;

        // flip horizontal
        if (container != null)
        {
            var s = container.localScale;
            s.x = Mathf.Abs(s.x);
            container.localScale = s;
        }

        if (_isGrounded)
            playerAnimator?.SetRun();
    }

    public void StopMove()
    {
        _moveX = 0f;

        if (_isGrounded)
            playerAnimator?.SetIdle();
    }

    public void TryJump()
    {
        if (rb == null) return;
        if (!_isGrounded) return;

        // Reset de la vitesse verticale pour un saut constant
        var v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        // Saut "contre" la gravité
        Vector2 jumpDir = (_invertGravityEnabled ? Vector2.down : Vector2.up);
        rb.AddForce(jumpDir * jumpForce, ForceMode2D.Impulse);

        // IMPORTANT: ignore le sol pendant quelques ms pour éviter le glitch de départ
        _groundLockUntil = Time.time + groundIgnoreAfterJump;
        _isGrounded = false;

        playerAnimator?.SetJump();
    }

    // Public si tu veux l'utiliser ailleurs
    public bool IsGrounded() => _isGrounded;

    private bool IsGroundedInternal()
    {
        // Lock anti-glitch juste après jump
        if (Time.time < _groundLockUntil)
            return false;

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
