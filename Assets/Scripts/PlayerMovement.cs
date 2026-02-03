using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement instance;

    [Header("Root Movement")]
    [Tooltip("Transform réellement déplacé. Si vide: parent sinon self.")]
    public Transform bodyToMove;

    [Header("Collider (shape only)")]
    public CapsuleCollider2D capsule;

    [Header("Move")]
    public float moveSpeed = 7f;
    public float acceleration = 80f;
    public float deceleration = 100f;
    public float airControlMultiplier = 0.75f;

    [Header("Jump")]
    public float jumpSpeed = 12f;
    public float coyoteTime = 0.10f;
    public float jumpBufferTime = 0.10f;
    [Range(0.1f, 0.9f)] public float jumpCutMultiplier = 0.5f;

    [Header("Gravity")]
    [Tooltip("Valeur positive. La direction est gérée par Down/Up.")]
    public float gravity = 30f;
    public float fallMultiplierNormal = 1.6f;
    public float fallMultiplierFloat = 0.35f;
    [Tooltip("Vitesse max de chute (valeur positive, dans le sens de Down).")]
    public float maxFallSpeed = 20f;

    [Header("Collision")]
    public LayerMask groundMask;
    [Tooltip("Peau de collision (garde un micro espace, évite collages).")]
    public float skinWidth = 0.01f;
    public int maxResolveIterations = 2;
    public float depenetrationStep = 0.02f;

    [Header("Grounding")]
    [Tooltip("Distance de check sol (petit).")]
    public float groundCheckDistance = 0.06f;

    [Header("Ground Snap / Stick")]
    [Tooltip("Snap : colle au sol si on est proche pendant une descente lente.")]
    public float groundSnapDistance = 0.12f;
    public float groundSnapMaxDownSpeed = 2.5f;

    [Tooltip("Stick : quand on est grounded, on maintient le contact (évite hover sur petites irrégularités).")]
    public float groundStickDistance = 0.20f;

    [Header("Visual / Anim")]
    public Transform container;
    public PlayerAnimator playerAnimator;

    public TriggerPoint triggerPoint;

    // Input
    private float _moveX;
    private bool _jumpHeld;

    // Masks
    private bool _slowFallEnabled;
    private bool _invertGravityEnabled;

    // Motion
    private Vector2 _vel;

    // Ground state
    private bool _isGrounded;
    private bool _wasGrounded;
    private float _lastGroundedTime = -999f;
    private float _lastJumpPressed = -999f;

    // Queries
    private ContactFilter2D _filter;
    private readonly RaycastHit2D[] _hits = new RaycastHit2D[12];

    // ✅ Source de vérité gravité
    private Vector2 Down => _invertGravityEnabled ? Vector2.up : Vector2.down;
    private Vector2 Up => -Down;

    private void Awake()
    {
        instance = this;

        if (bodyToMove == null)
            bodyToMove = (transform.parent != null) ? transform.parent : transform;

        // Neutralise tout Rigidbody2D sur l'objet déplacé (si tu en as un)
        var rb = bodyToMove.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        if (capsule == null)
            capsule = GetComponentInChildren<CapsuleCollider2D>();

        _filter = new ContactFilter2D();
        _filter.useTriggers = false;
        _filter.SetLayerMask(groundMask);
    }

    #region Masks
    private void OnEnable()
    {
        StartCoroutine(DelayMaskInit());
    }

    private IEnumerator DelayMaskInit()
    {
        yield return null;
        if (MaskManager.instance != null)
            MaskManager.instance.AddMaskListener(OnMaskChanged);

        OnMaskChanged();
    }

    private void OnDisable()
    {
        if (MaskManager.instance != null)
            MaskManager.instance.RemoveMaskListener(OnMaskChanged);
    }

    private void OnMaskChanged()
    {
        _slowFallEnabled = false;
        _invertGravityEnabled = false;

        // reset visual flip
        if (container != null)
        {
            var s = container.localScale;
            s.y = Mathf.Abs(s.y);
            container.localScale = s;
        }

        if (MaskManager.instance == null || MaskManager.instance.selectedMask == null)
            return;

        switch (MaskManager.instance.selectedMask.id)
        {
            case "mask1":
                _slowFallEnabled = true;
                break;

            case "mask2":
                _invertGravityEnabled = true;
                if (container != null)
                {
                    var s = container.localScale;
                    s.y = -Mathf.Abs(s.y);
                    container.localScale = s;
                }
                break;

            case "mask3":
                // TODO phase through
                break;
        }
    }
    #endregion

    private void FixedUpdate()
    {
        if (bodyToMove == null || capsule == null) return;

        _wasGrounded = _isGrounded;
        _isGrounded = CheckGrounded();
        if (_isGrounded) _lastGroundedTime = Time.time;

        // Landing anim
        if (!_wasGrounded && _isGrounded)
        {
            _vel = RemoveComponent(_vel, Down); // stop falling into ground
            if (_moveX != 0f) playerAnimator?.SetRun();
            else playerAnimator?.SetIdle();
        }
        else if (_wasGrounded && !_isGrounded)
        {
            playerAnimator?.SetJump();
        }

        // Horizontal velocity
        float targetX = _moveX * moveSpeed;
        float accel = Mathf.Abs(targetX) > 0.01f ? acceleration : deceleration;
        if (!_isGrounded) accel *= airControlMultiplier;
        _vel.x = Mathf.MoveTowards(_vel.x, targetX, accel * Time.fixedDeltaTime);

        // Jump buffer + coyote
        bool canJump = (Time.time - _lastGroundedTime) <= coyoteTime;
        bool buffered = (Time.time - _lastJumpPressed) <= jumpBufferTime;

        if (canJump && buffered)
        {
            _vel = RemoveComponent(_vel, Up);
            _vel = RemoveComponent(_vel, Down);
            _vel += Up * jumpSpeed;

            _lastJumpPressed = -999f;
            _isGrounded = false;
            playerAnimator?.SetJump();
        }

        // Gravity toward Down
        float fallMult = _slowFallEnabled ? fallMultiplierFloat : fallMultiplierNormal;
        bool falling = Vector2.Dot(_vel, Down) > 0f;
        float g = gravity * (falling ? fallMult : 1f);
        _vel += Down * g * Time.fixedDeltaTime;

        // Clamp fall speed along Down
        float vDown = Vector2.Dot(_vel, Down);
        vDown = Mathf.Min(vDown, maxFallSpeed);
        _vel = SetComponent(_vel, Down, vDown);

        // Jump cut (variable height) - cut Up component only
        if (!_jumpHeld)
        {
            float vUp = Vector2.Dot(_vel, Up);
            if (vUp > 0f)
                _vel = SetComponent(_vel, Up, vUp * jumpCutMultiplier);
        }

        // If grounded, cancel any Down component (no sinking)
        if (_isGrounded)
        {
            float d = Vector2.Dot(_vel, Down);
            if (d > 0f) _vel = RemoveComponent(_vel, Down);
        }

        // Ground snap when close and descending slowly
        if (!_isGrounded)
        {
            float downSpeed = Vector2.Dot(_vel, Down); // >0 = descending
            if (downSpeed > 0f && downSpeed < groundSnapMaxDownSpeed)
            {
                if (TryGetCastDistance(Down, groundSnapDistance, out float distToSurface))
                {
                    float snap = Mathf.Max(0f, distToSurface - skinWidth);
                    if (snap > 0f)
                    {
                        bodyToMove.position = (Vector2)bodyToMove.position + Down * snap;
                        _isGrounded = true;
                        _vel = RemoveComponent(_vel, Down);
                    }
                }
            }
        }

        // Move with collisions (X then Y)
        Vector2 pos = bodyToMove.position;
        Vector2 delta = _vel * Time.fixedDeltaTime;

        pos = MoveAndCollide(pos, new Vector2(delta.x, 0f), isVerticalMove: false);
        pos = MoveAndCollide(pos, new Vector2(0f, delta.y), isVerticalMove: true);

        bodyToMove.position = pos;

        // Ground stick: once grounded, keep tight contact with ground
        // Helps remove “hover gap” on uneven tiles.
        if (_isGrounded)
        {
            if (TryGetCastDistance(Down, groundStickDistance, out float distToSurface))
            {
                float stick = Mathf.Max(0f, distToSurface - skinWidth);
                if (stick > 0f)
                    bodyToMove.position = (Vector2)bodyToMove.position + Down * stick;
            }
        }
    }

    private Vector2 MoveAndCollide(Vector2 startPos, Vector2 delta, bool isVerticalMove)
    {
        if (delta == Vector2.zero) return startPos;

        Vector2 pos = startPos;
        Vector2 remaining = delta;

        for (int iter = 0; iter < maxResolveIterations; iter++)
        {
            float dist = remaining.magnitude;
            if (dist <= 0f) break;

            Vector2 dir = remaining / dist;

            int hitCount = capsule.Cast(dir, _filter, _hits, dist + skinWidth);
            if (hitCount == 0)
            {
                pos += remaining;
                break;
            }

            RaycastHit2D best = default;
            float bestDist = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                var h = _hits[i];
                if (h.collider == null) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h;
                }
            }

            if (best.collider == null)
            {
                pos += remaining;
                break;
            }

            float moveDist = Mathf.Max(0f, bestDist - skinWidth);

            if (moveDist < 0.0001f)
            {
                pos += best.normal * depenetrationStep;
            }
            else
            {
                pos += dir * moveDist;
            }

            // Apply position immediately so subsequent casts use updated collider pose
            bodyToMove.position = pos;

            Vector2 n = best.normal;

            if (isVerticalMove)
            {
                // If hitting floor/ceiling, kill vertical components
                if (Vector2.Dot(n, Up) > 0.5f)
                {
                    _vel = RemoveComponent(_vel, Up);
                    _vel = RemoveComponent(_vel, Down);
                    remaining = Vector2.zero;
                    break;
                }
            }
            else
            {
                // Wall hit: kill x
                if (Mathf.Abs(n.x) > 0.2f)
                    _vel.x = 0f;
            }

            // Minimal slide along tangent to avoid “up injection”
            Vector2 tangent = new Vector2(-n.y, n.x);
            remaining = Vector2.Dot(remaining, tangent) * tangent;

            if (remaining.sqrMagnitude < 0.000001f)
                break;
        }

        return pos;
    }

    private bool CheckGrounded()
    {
        return capsule.Cast(Down, _filter, _hits, groundCheckDistance) > 0;
    }

    private bool TryGetCastDistance(Vector2 dir, float maxDistance, out float bestDistance)
    {
        int hitCount = capsule.Cast(dir, _filter, _hits, maxDistance);
        if (hitCount > 0)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                var h = _hits[i];
                if (h.collider == null) continue;
                if (h.distance < best) best = h.distance;
            }

            if (best < float.PositiveInfinity)
            {
                bestDistance = best;
                return true;
            }
        }

        bestDistance = 0f;
        return false;
    }

    // --- Vector helpers: remove/set component along axis ---
    private static Vector2 RemoveComponent(Vector2 v, Vector2 axis)
    {
        axis = axis.normalized;
        return v - axis * Vector2.Dot(v, axis);
    }

    private static Vector2 SetComponent(Vector2 v, Vector2 axis, float value)
    {
        axis = axis.normalized;
        v = RemoveComponent(v, axis);
        return v + axis * value;
    }

    // -------- Input API --------

    public void MoveLeft()
    {
        _moveX = -1f;

        if (container != null)
        {
            var s = container.localScale;
            s.x = -Mathf.Abs(s.x);
            container.localScale = s;
        }

        if (_isGrounded) playerAnimator?.SetRun();
    }

    public void MoveRight()
    {
        _moveX = 1f;

        if (container != null)
        {
            var s = container.localScale;
            s.x = Mathf.Abs(s.x);
            container.localScale = s;
        }

        if (_isGrounded) playerAnimator?.SetRun();
    }

    public void StopMove()
    {
        _moveX = 0f;
        if (_isGrounded) playerAnimator?.SetIdle();
    }

    public void TryJump()
    {
        _jumpHeld = true;
        _lastJumpPressed = Time.time;
    }

    public void ReleaseJump()
    {
        _jumpHeld = false;
    }

    public bool IsGrounded() => _isGrounded;
}
