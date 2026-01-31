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
    private Vector3 _groundCheckLocalDefault;

    public Transform container;

    private void Awake()
    {
        instance = this;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        // valeurs par défaut
        _defaultBaseGravityScale = baseGravityScale;
        if (groundCheck != null) _groundCheckLocalDefault = groundCheck.localPosition;
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
        // On ne change PAS la gravité de montée : baseGravityScale reste pareil.
        // On modifie uniquement le "fall multiplier" dans FixedUpdate.
    }

    // 2) Gravité inversée
    private void HandleInvertedGravity()
    {
        _invertGravityEnabled = true;

        // Retournement vertical du transform
        if (groundCheck != null)
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

        // Retournement vertical du transform
        var s = container.localScale;
        s.y = Mathf.Abs(s.y);
        container.localScale = s;

        // Reset gravité effective (sera recalculée en FixedUpdate)
        if (rb != null)
            rb.gravityScale = baseGravityScale;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // Mouvement horizontal
        var v = rb.linearVelocity;
        v.x = _moveX * moveSpeed;
        rb.linearVelocity = v;

        // ---- Gravité effective ----
        // Base gravité (signe géré par inversion)
        float signedBase = Mathf.Abs(baseGravityScale) * (_invertGravityEnabled ? -1f : 1f);

        // Déterminer si on "tombe" dans le sens de la gravité
        // - gravité normale (signedBase > 0) : tombe si velY < 0
        // - gravité inversée (signedBase < 0) : tombe (vers le haut) si velY > 0
        bool fallingWithGravity = signedBase > 0f ? (rb.linearVelocity.y < 0f)
                                                  : (rb.linearVelocity.y > 0f);

        float fallMult = _slowFallEnabled ? fallMultiplierFloat : fallMultiplierNormal;

        // Appliquer : même montée, chute modifiée
        rb.gravityScale = fallingWithGravity ? (signedBase * fallMult) : signedBase;
    }

    public void MoveLeft() {
        _moveX = -1f;
        var s = container.localScale;
        s.x = -Mathf.Abs(s.x);
        container.localScale = s;
    }
    public void MoveRight() {
        _moveX = 1f;
        var s = container.localScale;
        s.x = Mathf.Abs(s.x);
        container.localScale = s;
    }
    public void StopMove()  => _moveX = 0f;

    public void TryJump()
    {
        if (rb == null) return;
        if (!IsGrounded()) return;

        // Reset de la vitesse verticale pour un saut constant
        var v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        // Saut "contre" la gravité :
        // gravité normale => jump vers le haut
        // gravité inversée => jump vers le bas
        Vector2 jumpDir = (_invertGravityEnabled ? Vector2.down : Vector2.up);

        rb.AddForce(jumpDir * jumpForce, ForceMode2D.Impulse);
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
