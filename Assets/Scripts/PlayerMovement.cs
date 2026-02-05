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

    [Header("Slope Movement")]
    [Tooltip("Au-delà de cet angle, on considère la surface comme un mur (bloque la marche).")]
    [Range(0f, 89f)] public float maxWalkableSlopeAngle = 60f;

    [Header("Jump")]
    public float jumpSpeed = 12f;
    public float coyoteTime = 0.10f;
    public float jumpBufferTime = 0.10f;
    [Range(0.1f, 0.9f)] public float jumpCutMultiplier = 0.5f;

    [Header("Gravity")]
    public float gravity = 30f;
    public float fallMultiplierNormal = 1.6f;
    public float fallMultiplierFloat = 0.35f;
    public float maxFallSpeed = 20f;

    [Header("Collision")]
    public LayerMask groundMask;
    public float skinWidth = 0.02f;
    public int maxResolveIterations = 4;
    public float depenetrationStep = 0.01f;

    [Header("Grounding")]
    public float groundCheckDistance = 0.06f;

    [Header("Ground Snap / Stick")]
    public float groundSnapDistance = 0.18f;
    public float groundSnapMaxDownSpeed = 4.0f;
    public float groundStickDistance = 0.30f;

    [Header("Anti Hop")]
    [Tooltip("Durée où on 'colle' au sol juste après avoir quitté une pente.")]
    public float stickGraceTime = 0.08f;
    [Tooltip("Si on tombe plus vite que ça, on n'aimante pas (valeur positive).")]
    public float stickDownSpeedLimit = 6f;

    [Header("Visual / Anim")]
    public Transform container;
    public PlayerAnimator playerAnimator;

    public TriggerPoint triggerPoint;
    public ZoneTrigger zoneTrigger;

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

    // Anti-hop times
    private float _lastTimeGroundedReal = -999f;
    private float _lastTimeJumped = -999f;

    // Ground info
    private Vector2 _groundNormal = Vector2.up;
    private Vector2 _groundTangent = Vector2.right;
    private bool _hasGroundInfo;

    // Queries
    private ContactFilter2D _filter;
    private readonly RaycastHit2D[] _hits = new RaycastHit2D[12];

    // Source de vérité gravité
    private Vector2 Down => _invertGravityEnabled ? Vector2.up : Vector2.down;
    private Vector2 Up => -Down;

    [Header("Gravity Flip Pivot")]
    public Transform flipPivot; // mettre le body avec collider centré, attention pas d'offset sur Y sur collider

    private void Awake()
    {
        instance = this;

        if (bodyToMove == null)
            bodyToMove = (transform.parent != null) ? transform.parent : transform;

        // On bouge par Transform, donc pas de Rigidbody simulé ici
        var rb = bodyToMove.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        if (capsule == null)
            capsule = GetComponentInChildren<CapsuleCollider2D>();

        _filter = new ContactFilter2D();
        _filter.useTriggers = false;
        _filter.SetLayerMask(groundMask);
    }

    #region Masks
    private void OnEnable() => StartCoroutine(DelayMaskInit());

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

        FlipVisualAroundPivotY(_invertGravityEnabled);

        if (MaskManager.instance == null || MaskManager.instance.selectedMask == null)
            return;

        switch (MaskManager.instance.selectedMask.id)
        {
            case "mask1":
                _slowFallEnabled = true;
                break;

            case "mask2":
                _invertGravityEnabled = true;
                FlipVisualAroundPivotY(_invertGravityEnabled);

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
        _isGrounded = CheckGrounded(); // ✅ FIX 1

        if (_isGrounded)
        {
            _lastGroundedTime = Time.time;
            _lastTimeGroundedReal = Time.time;
            UpdateGroundInfo();
        }
        else
        {
            _hasGroundInfo = false;
        }

        // Landing anim
        if (!_wasGrounded && _isGrounded)
        {
            _vel = RemoveComponent(_vel, Down);
            if (_moveX != 0f) playerAnimator?.SetRun();
            else playerAnimator?.SetIdle();
        }
        else if (_wasGrounded && !_isGrounded)
        {
            playerAnimator?.SetJump();
        }

        // ---- Slope / Horizontal control ----
        if (_isGrounded && _hasGroundInfo)
        {
            float targetSpeed = _moveX * moveSpeed;
            float currentSpeed = Vector2.Dot(_vel, _groundTangent);
            float rate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;

            float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);
            _vel = SetComponent(_vel, _groundTangent, newSpeed);
        }
        else
        {
            float targetX = _moveX * moveSpeed;
            float rate = (Mathf.Abs(targetX) > 0.01f) ? acceleration : deceleration;
            rate *= airControlMultiplier;

            _vel.x = Mathf.MoveTowards(_vel.x, targetX, rate * Time.fixedDeltaTime);
        }

        // ---- Jump buffer + coyote ----
        bool canJump = (Time.time - _lastGroundedTime) <= coyoteTime;
        bool buffered = (Time.time - _lastJumpPressed) <= jumpBufferTime;

        if (canJump && buffered)
        {
            _vel = RemoveComponent(_vel, Up);
            _vel = RemoveComponent(_vel, Down);
            _vel += Up * jumpSpeed;

            _lastJumpPressed = -999f;
            _isGrounded = false;
            _hasGroundInfo = false;

            _lastTimeJumped = Time.time;
            playerAnimator?.SetJump();
        }

        // ---- Gravity ----
        float fallMult = _slowFallEnabled ? fallMultiplierFloat : fallMultiplierNormal;
        bool falling = Vector2.Dot(_vel, Down) > 0f;
        float g = gravity * (falling ? fallMult : 1f);

        _vel += Down * g * Time.fixedDeltaTime;

        // Clamp fall speed along Down
        float vDown = Vector2.Dot(_vel, Down);
        vDown = Mathf.Min(vDown, maxFallSpeed);
        _vel = SetComponent(_vel, Down, vDown);

        // Jump cut (variable height)
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

        // ---- Ground snap (before moving) ----
        if (!_isGrounded)
        {
            float downSpeed = Vector2.Dot(_vel, Down);
            if (downSpeed > 0f && downSpeed < groundSnapMaxDownSpeed)
            {
                if (TryGetCastDistance(Down, groundSnapDistance, out float distToSurface))
                {
                    float snap = Mathf.Max(0f, distToSurface - skinWidth);
                    if (snap > 0f)
                    {
                        bodyToMove.position = (Vector2)bodyToMove.position + Down * snap;
                        Physics2D.SyncTransforms(); // ✅ important

                        _isGrounded = true;
                        _lastTimeGroundedReal = Time.time;
                        UpdateGroundInfo();
                        _vel = RemoveComponent(_vel, Down);
                    }
                }
            }
        }

        // ---- Move with collisions ----
        Vector2 pos = bodyToMove.position;
        Vector2 delta = _vel * Time.fixedDeltaTime;

        pos = MoveAndCollide(pos, delta);
        bodyToMove.position = pos;
        Physics2D.SyncTransforms(); // ✅ important

        // ---- Ground stick (when grounded) ----
        if (_isGrounded)
        {
            StickToGroundIfClose();
        }
        else
        {
            bool recentlyGrounded = (Time.time - _lastTimeGroundedReal) <= stickGraceTime;
            bool recentlyJumped = (Time.time - _lastTimeJumped) <= 0.12f;

            if (recentlyGrounded && !recentlyJumped)
            {
                float downSpeed = Vector2.Dot(_vel, Down);
                if (downSpeed < stickDownSpeedLimit)
                {
                    if (TryGetCastDistance(Down, groundStickDistance, out float distToSurface))
                    {
                        float stick = Mathf.Max(0f, distToSurface - skinWidth);
                        if (stick > 0f)
                        {
                            bodyToMove.position = (Vector2)bodyToMove.position + Down * stick;
                            Physics2D.SyncTransforms(); // ✅ important

                            _isGrounded = true;
                            _lastTimeGroundedReal = Time.time;
                            UpdateGroundInfo();
                            _vel = RemoveComponent(_vel, Down);
                            StickToGroundIfClose();
                        }
                    }
                }
            }
        }
    }

    private void StickToGroundIfClose()
    {
        if (TryGetCastDistance(Down, groundStickDistance, out float distToSurface))
        {
            float stick = Mathf.Max(0f, distToSurface - skinWidth);
            if (stick > 0f)
            {
                bodyToMove.position = (Vector2)bodyToMove.position + Down * stick;
                Physics2D.SyncTransforms(); // ✅ important
            }
        }
    }

    private Vector2 MoveAndCollide(Vector2 startPos, Vector2 delta)
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

                // ✅ FIX 2 : ignore hits that are not facing the movement direction
                // (évite les hits parasites à distance ~0 le long des murs/polygones)
                if (Vector2.Dot(h.normal, dir) > -0.001f)
                    continue;

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
                pos += best.normal * depenetrationStep;
            else
                pos += dir * moveDist;

            bodyToMove.position = pos;
            Physics2D.SyncTransforms(); // ✅ important (casts suivants doivent être corrects)

            // Remove velocity into surface normal
            Vector2 n = best.normal;
            float into = Vector2.Dot(_vel, n);
            if (into < 0f) _vel -= n * into;

            // Slide remaining along tangent
            Vector2 tangent = new Vector2(-n.y, n.x);
            remaining = Vector2.Dot(remaining, tangent) * tangent;

            if (remaining.sqrMagnitude < 0.000001f)
                break;
        }

        return pos;
    }

    // ✅ FIX 1 : grounded = uniquement surfaces "support" (normale vers Up + pente <= max)
    private bool CheckGrounded()
    {
        int hitCount = capsule.Cast(Down, _filter, _hits, groundCheckDistance);
        if (hitCount <= 0) return false;

        for (int i = 0; i < hitCount; i++)
        {
            var h = _hits[i];
            if (h.collider == null) continue;

            if (Vector2.Dot(h.normal, Up) < 0.2f) // ignore murs/plafonds
                continue;

            float angle = Vector2.Angle(h.normal, Up);
            if (angle > maxWalkableSlopeAngle) // ignore pentes trop raides
                continue;

            return true;
        }

        return false;
    }

    private void UpdateGroundInfo()
    {
        _hasGroundInfo = false;

        int hitCount = capsule.Cast(Down, _filter, _hits, groundCheckDistance + 0.25f);
        if (hitCount <= 0) return;

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

        if (best.collider == null) return;

        Vector2 n = best.normal;

        if (Vector2.Dot(n, Up) < 0.2f) return;

        float angle = Vector2.Angle(n, Up);
        if (angle > maxWalkableSlopeAngle) return;

        _groundNormal = n;
        _groundTangent = new Vector2(-_groundNormal.y, _groundNormal.x).normalized;
        if (_groundTangent.x < 0f) _groundTangent = -_groundTangent;

        _hasGroundInfo = true;
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

    private void FlipVisualAroundPivotY(bool inverted)
    {
        if (container == null || bodyToMove == null || flipPivot == null)
            return;

        // 1) position monde du pivot AVANT
        Vector3 pivotWorldBefore = flipPivot.position;

        // 2) flip visuel
        var s = container.localScale;
        s.y = inverted ? -Mathf.Abs(s.y) : Mathf.Abs(s.y);
        container.localScale = s;

        // 3) resync
        Physics2D.SyncTransforms();

        // 4) position monde du pivot APRÈS
        Vector3 pivotWorldAfter = flipPivot.position;

        // 5) delta à appliquer au body (ou root déplacé)
        Vector3 delta = pivotWorldBefore - pivotWorldAfter;

        bodyToMove.position += delta;
        Physics2D.SyncTransforms();
    }

    public bool IsGrounded() => _isGrounded;
}
