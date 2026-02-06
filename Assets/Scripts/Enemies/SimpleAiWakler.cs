using UnityEngine;

public class SimpleAiWalker : EnemyMovement
{
    public enum GravityDir { Down, Up, Left, Right }
    public enum BaseState { Idle, WalkTowardPlayer, WalkRandom }

    [Header("Root Movement")]
    [Tooltip("Transform réellement déplacé. Si vide: parent sinon self.")]
    public Transform bodyToMove;

    [Header("Collider (shape only)")]
    public CapsuleCollider2D capsule;

    [Header("Move")]
    public float moveSpeed = 4f;
    public float acceleration = 60f;
    public float deceleration = 80f;

    [Header("Jump")]
    public float jumpSpeed = 10f;
    public float jumpBufferTime = 0.10f;

    [Header("Gravity")]
    public GravityDir gravityDirection = GravityDir.Down;
    public float gravity = 30f;
    public float maxFallSpeed = 20f;

    [Header("Collision")]
    public LayerMask groundMask;
    public float skinWidth = 0.02f;
    public int maxResolveIterations = 4;
    public float depenetrationStep = 0.01f;

    [Header("Grounding")]
    public float groundCheckDistance = 0.06f;

    [Header("AI Base State")]
    public BaseState baseStateWhenPlayerSameZone = BaseState.WalkTowardPlayer;
    public BaseState baseStateWhenPlayerOtherZone = BaseState.WalkRandom;

    [Header("Walk Random")]
    public float randomChangeDirMin = 0.7f;
    public float randomChangeDirMax = 1.8f;

    // Motion
    private Vector2 _vel;

    // Ground
    private bool _isGrounded;
    private bool _wasGrounded;

    // AI
    private BaseState _currentBaseState;
    private int _walkSign = 1;
    private float _nextRandomSwitchTime;

    // Jump buffer
    private float _lastJumpRequested = -999f;
    private int _queuedJumpLateral = 0; // -1 left, +1 right, 0 none

    // Queries
    private ContactFilter2D _filter;
    private readonly RaycastHit2D[] _hits = new RaycastHit2D[12];

    // =========================
    // Anim (AJOUT UNIQUEMENT)
    // =========================
    private enum AnimState { Idle, Run, Jump }
    private AnimState _animState = AnimState.Idle;
    private AnimState _animStateBeforeDisable = AnimState.Idle;
    private bool _replayAnimOnEnable;

    private int _facing = 1; // 1 = droite, -1 = gauche

    private Vector2 Down
    {
        get
        {
            return gravityDirection switch
            {
                GravityDir.Down => Vector2.down,
                GravityDir.Up => Vector2.up,
                GravityDir.Left => Vector2.left,
                GravityDir.Right => Vector2.right,
                _ => Vector2.down
            };
        }
    }

    private Vector2 Up => -Down;

    // Axe latéral (perpendiculaire à Down)
    private Vector2 RightAxis
    {
        get
        {
            Vector2 d = Down.normalized;
            return new Vector2(d.y, -d.x); // rotation 90°
        }
    }

    private void Awake()
    {
        if (bodyToMove == null)
            bodyToMove = (transform.parent != null) ? transform.parent : transform;

        // Comme PlayerMovement : on bouge par Transform, donc rigidbody (si présent) désactivé
        var rb = bodyToMove.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        if (capsule == null)
            capsule = GetComponentInChildren<CapsuleCollider2D>();

        _filter = new ContactFilter2D();
        _filter.useTriggers = false;
        _filter.SetLayerMask(groundMask);
    }

    private void OnEnable()
    {
        if (!_replayAnimOnEnable) return;
        _replayAnimOnEnable = false;

        // On force un "changement" pour être sûr que ça redéclenche
        //playerAnimator?.SetIdle();
        _animState = (AnimState)(-1);

        // Puis on rejoue l'état mémorisé
        switch (_animStateBeforeDisable)
        {
            case AnimState.Run:  SetAnimRun();  break;
            case AnimState.Jump: SetAnimJump(); break;
            default:             SetAnimIdle(); break;
        }
    }

    private void OnDisable()
    {
        _animStateBeforeDisable = _animState;
        _replayAnimOnEnable = true;
    }

    private void FixedUpdate()
    {
        if (bodyToMove == null || capsule == null) return;

        // Ground check
        _wasGrounded = _isGrounded;
        _isGrounded = CheckGrounded();

        // Base state selon zone
        RefreshBaseState();

        // AI -> choix direction
        int desiredSign = 0;
        switch (_currentBaseState)
        {
            case BaseState.Idle:
                desiredSign = 0;
                break;

            case BaseState.WalkTowardPlayer:
                desiredSign = GetTowardPlayerSign();
                break;

            case BaseState.WalkRandom:
                desiredSign = GetRandomSign();
                break;
        }

        // Horizontal (lateral) accel/decel sur l'axe RightAxis
        float target = desiredSign * moveSpeed;
        float current = Vector2.Dot(_vel, RightAxis.normalized);
        float rate = (Mathf.Abs(target) > 0.01f) ? acceleration : deceleration;
        float newSpeed = Mathf.MoveTowards(current, target, rate * Time.fixedDeltaTime);
        _vel = SetComponent(_vel, RightAxis, newSpeed);

        // Jump buffer
        bool buffered = (Time.time - _lastJumpRequested) <= jumpBufferTime;
        if (buffered && _isGrounded && _queuedJumpLateral != 0)
        {
            // reset vertical
            _vel = RemoveComponent(_vel, Up);
            _vel = RemoveComponent(_vel, Down);

            // impulse jump
            _vel += Up * jumpSpeed;

            // petit biais latéral optionnel (comme ton ancien script)
            _vel += RightAxis.normalized * (_queuedJumpLateral * (jumpSpeed * 0.20f));

            _lastJumpRequested = -999f;
            _queuedJumpLateral = 0;
            _isGrounded = false;
        }

        // Gravity (clamp)
        _vel += Down * gravity * Time.fixedDeltaTime;
        float vDown = Vector2.Dot(_vel, Down.normalized);
        vDown = Mathf.Min(vDown, maxFallSpeed);
        _vel = SetComponent(_vel, Down, vDown);

        // If grounded, cancel Down component (no sinking)
        if (_isGrounded)
        {
            float d = Vector2.Dot(_vel, Down.normalized);
            if (d > 0f) _vel = RemoveComponent(_vel, Down);
        }

        // Move with collisions (comme ton player)
        Vector2 pos = bodyToMove.position;
        Vector2 delta = _vel * Time.fixedDeltaTime;

        pos = MoveAndCollide(pos, delta);
        bodyToMove.position = pos;
        Physics2D.SyncTransforms();

        // =========================
        // Anim (AJOUT UNIQUEMENT)
        // =========================
        UpdateAnimations(desiredSign);
        UpdateFlip(desiredSign);
    }

    private void UpdateAnimations(int desiredSign)
    {
        // Landing -> idle/run
        if (!_wasGrounded && _isGrounded)
        {
            if (desiredSign != 0) SetAnimRun();
            else SetAnimIdle();
            return;
        }

        // In air -> jump
        if (!_isGrounded)
        {
            SetAnimJump();
            return;
        }

        // Grounded -> idle/run
        if (desiredSign != 0) SetAnimRun();
        else SetAnimIdle();
    }

    private void SetAnimRun()
    {
        if (_animState == AnimState.Run) return;
        _animState = AnimState.Run;
        // playerAnimator est hérité (ne pas redéclarer)
        playerAnimator?.SetRun();
    }

    private void SetAnimIdle()
    {
        if (_animState == AnimState.Idle) return;
        _animState = AnimState.Idle;
        playerAnimator?.SetIdle();
    }

    private void SetAnimJump()
    {
        if (_animState == AnimState.Jump) return;
        _animState = AnimState.Jump;
        playerAnimator?.SetJump();
    }

    private void RefreshBaseState()
    {
        bool sameZone =
            ZoneManager.Instance != null &&
            zoneTrigger != null &&
            ZoneManager.Instance.playerCurrentZone != null &&
            zoneTrigger.currentZone != null &&
            ZoneManager.Instance.playerCurrentZone == zoneTrigger.currentZone;

        _currentBaseState = sameZone ? baseStateWhenPlayerSameZone : baseStateWhenPlayerOtherZone;
    }

    private int GetTowardPlayerSign()
    {
        if (PlayerMovement.instance == null || PlayerMovement.instance.container == null) return 0;

        Vector2 myPos = bodyToMove.position;
        Vector2 pPos = (Vector2)PlayerMovement.instance.container.position;

        Vector2 toPlayer = pPos - myPos;
        float lateral = Vector2.Dot(toPlayer, RightAxis.normalized);

        if (Mathf.Abs(lateral) < 0.05f) return 0;
        return (lateral > 0f) ? 1 : -1;
    }

    private int GetRandomSign()
    {
        if (Time.time >= _nextRandomSwitchTime)
        {
            _walkSign = (Random.value < 0.5f) ? -1 : 1;
            _nextRandomSwitchTime = Time.time + Random.Range(randomChangeDirMin, randomChangeDirMax);
        }
        return _walkSign;
    }

    // =========================
    // Actions
    // =========================
    public override void HandleActionOnTriggerEnter(string action)
    {
        switch (action)
        {
            case "JUMP_LEFT":
                JumpLeft();
                break;

            case "JUMP_RIGHT":
                JumpRight();
                break;

            case "WALK_LEFT":
                _walkSign = -1;
                break;

            case "WALK_RIGHT":
                _walkSign = 1;
                break;

            case "IDLE":
                _currentBaseState = BaseState.Idle;
                break;

            default:
                break;
        }
    }

    private void JumpLeft()
    {
        _queuedJumpLateral = -1;
        _lastJumpRequested = Time.time;
    }

    private void JumpRight()
    {
        _queuedJumpLateral = 1;
        _lastJumpRequested = Time.time;
    }

    // =========================
    // Collision helpers
    // =========================
    private bool CheckGrounded()
    {
        int hitCount = capsule.Cast(Down.normalized, _filter, _hits, groundCheckDistance);
        return hitCount > 0;
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

                // Ignore hits not facing movement direction
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
            Physics2D.SyncTransforms();

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

    private void UpdateFlip(int desiredSign)
    {
        if (desiredSign == 0 || visualContainer == null) return;

        if (desiredSign != _facing)
        {
            _facing = desiredSign;

            Vector3 s = visualContainer.localScale;
            s.x = Mathf.Abs(s.x) * _facing;
            visualContainer.localScale = s;
        }
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
}
