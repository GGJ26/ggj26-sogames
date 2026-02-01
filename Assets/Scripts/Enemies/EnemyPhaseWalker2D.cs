using UnityEngine;

public class EnemyZoneChaser2D : MonoBehaviour
{
    public enum TeleportReference
    {
        ClosestToPlayer,
        ClosestToEnemy
    }

    [Header("Refs")]
    public Transform container;

    [Tooltip("Rigidbody2D à piloter (peut être sur un enfant/parent).")]
    public Rigidbody2D rb;

    [Tooltip("Assign if you want, otherwise auto via PlayerMovement.instance.container")]
    public Transform playerTarget;

    // [Tooltip("Zone tracker on the player")]
    // public ZoneTracker2D playerTracker;

    // [Tooltip("Zone tracker on this enemy (can be on another GO too, but usually same).")]
    // public ZoneTracker2D enemyTracker;

    [Header("Chase")]
    public float moveSpeed = 4f;

    [Header("Teleport")]
    public bool allowTeleport = true;
    public TeleportReference teleportReference = TeleportReference.ClosestToPlayer;
    public float teleportCooldown = 0.5f;
    public bool zeroVelocityOnTeleport = true;
    private float _nextTeleportTime = 0f;

    [Header("Jump & Obstacles")]
    public Transform groundCheck;
    public Transform wallCheck;
    public LayerMask groundMask;
    public LayerMask obstacleMask;

    public float groundCheckRadius = 0.1f;
    public float wallDetectDistance = 0.5f;

    public float jumpVelocity = 9f;
    public float jumpForwardBoost = 2.5f;
    public float jumpCooldown = 0.5f;
    public float minBlockedSpeed = 0.5f;

    [Header("Commit (avoid instant turn after jump)")]
    public float commitAfterJumpTime = 0.3f;

    [Header("Debug")]
    public bool debugOverlay = true;
    public bool allowAirControl = false; // si tu veux bouger même sans grounded

    private bool _grounded;
    private float _desiredDirX;
    private float _nextJumpTime;
    private float _commitUntil;
    private float _commitDirX;

    public ZoneTracker2D zoneTracker2D;

    private void Awake()
    {
        if (container == null) container = transform;

        // Si rb n’est pas assigné, on tente de le trouver autour
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = GetComponentInChildren<Rigidbody2D>();
            if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    private void Start()
    {
        if (playerTarget == null && PlayerMovement.instance != null)
            playerTarget = PlayerMovement.instance.container;

        // if (enemyTracker == null)
        // {
        //     enemyTracker = GetComponent<ZoneTracker2D>();
        //     if (enemyTracker == null) enemyTracker = GetComponentInChildren<ZoneTracker2D>();
        //     if (enemyTracker == null) enemyTracker = GetComponentInParent<ZoneTracker2D>();
        // }

        // if (playerTracker == null && PlayerMovement.instance != null)
        // {
        //     playerTracker = PlayerMovement.instance.GetComp<ZoneTracker2D>();
        //     if (playerTracker == null) playerTracker = PlayerMovement.instance.GetComponentInChildren<ZoneTracker2D>();
        // }
    }

    private void Update()
    {
        _grounded = IsGrounded();
    }

    private void FixedUpdate()
    {
        // if (rb == null || playerTarget == null || enemyTracker == null || playerTracker == null)
        //     return;

        Zone2D enemyZone =  zoneTracker2D.CurrentZone;
        Zone2D playerZone = PlayerMovement.instance.zoneTracker2D.CurrentZone;

        bool sameZone = (enemyZone != null && playerZone != null && enemyZone == playerZone);

        // Teleport si pas même zone
        if (!sameZone)
        {
            if (allowTeleport && playerZone != null && Time.time >= _nextTeleportTime)
            {
                TeleportToPlayerZone(playerZone);
                _nextTeleportTime = Time.time + teleportCooldown;
                return;
            }
        }

        // Chase
        UpdateDesiredDirection();
        TryJump();
        ApplyMove();
        UpdateFacing(GetFacingDir());
    }

    // ----------------------------
    // Chase
    // ----------------------------

    private void UpdateDesiredDirection()
    {
        float dx = playerTarget.position.x - rb.position.x;

        if (Mathf.Abs(dx) > 0.05f)
            _desiredDirX = Mathf.Sign(dx);
        else
            _desiredDirX = 0f;
    }

    private void ApplyMove()
    {
        float dir = (Time.time < _commitUntil) ? _commitDirX : _desiredDirX;

        if (!_grounded && !allowAirControl)
            return; // keep air momentum

        if (Mathf.Abs(_desiredDirX) < 0.01f)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);
    }

    private float GetFacingDir()
    {
        if (Time.time < _commitUntil) return _commitDirX;
        if (Mathf.Abs(_desiredDirX) < 0.01f) return Mathf.Sign(playerTarget.position.x - rb.position.x);
        return _desiredDirX;
    }

    // ----------------------------
    // Jump
    // ----------------------------

    private void TryJump()
    {
        if (!_grounded) return;
        if (Time.time < _nextJumpTime) return;

        float dir = (Time.time < _commitUntil) ? _commitDirX : _desiredDirX;
        if (Mathf.Abs(dir) < 0.1f) return;

        bool wall = IsWallAhead(dir);
        bool blocked = Mathf.Abs(rb.linearVelocity.x) < minBlockedSpeed;

        if (wall || blocked)
        {
            rb.linearVelocity = new Vector2((dir * moveSpeed) + (dir * jumpForwardBoost), jumpVelocity);

            _nextJumpTime = Time.time + jumpCooldown;
            _commitDirX = dir;
            _commitUntil = Time.time + commitAfterJumpTime;
        }
    }

    private bool IsGrounded()
    {
        if (groundCheck == null) return false;
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask) != null;
    }

    private bool IsWallAhead(float dirX)
    {
        if (wallCheck == null) return false;
        RaycastHit2D hit = Physics2D.Raycast(
            wallCheck.position,
            Vector2.right * Mathf.Sign(dirX),
            wallDetectDistance,
            obstacleMask
        );
        return hit.collider != null && !hit.collider.isTrigger;
    }

    // ----------------------------
    // Teleport
    // ----------------------------

    private void TeleportToPlayerZone(Zone2D playerZone)
    {
        if (playerZone == null) return;

        Vector2 reference =
            (teleportReference == TeleportReference.ClosestToEnemy)
                ? rb.position
                : (Vector2)playerTarget.position;

        Transform sp = playerZone.GetClosestSpawnPoint(reference);
        if (sp == null) return;

        rb.position = sp.position;

        if (zeroVelocityOnTeleport)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void UpdateFacing(float dirX)
    {
        if (container == null) return;
        if (Mathf.Abs(dirX) < 0.001f) return;

        Vector3 s = container.localScale;
        s.x = Mathf.Abs(s.x) * Mathf.Sign(dirX);
        container.localScale = s;
    }

    // ----------------------------
    // Debug overlay
    // ----------------------------

    // private void OnGUI()
    // {
    //     if (!debugOverlay || !Application.isPlaying) return;

    //     GUI.color = Color.white;
    //     GUILayout.BeginArea(new Rect(12, 12, 560, 240), GUI.skin.box);
    //     GUILayout.Label("<b>EnemyZoneChaser2D</b>", new GUIStyle(GUI.skin.label) { richText = true });

    //     GUILayout.Label($"rb assigned: {(rb != null)} | playerTarget assigned: {(playerTarget != null)} | trackers: enemy={(enemyTracker != null)} player={(playerTracker != null)}");

    //     Zone2D ez = (enemyTracker != null) ? enemyTracker.CurrentZone : null;
    //     Zone2D pz = (playerTracker != null) ? playerTracker.CurrentZone : null;
    //     bool same = (ez != null && pz != null && ez == pz);

    //     GUILayout.Label($"EnemyZone: {(ez ? ez.name : "NULL")} | PlayerZone: {(pz ? pz.name : "NULL")} | Same: {same}");
    //     if (rb != null && playerTarget != null)
    //         GUILayout.Label($"EnemyX: {rb.position.x:F2} | PlayerX: {playerTarget.position.x:F2} | desiredDirX: {_desiredDirX:F1}");

    //     GUILayout.Label($"Grounded: {_grounded} | Vel: {(rb ? $"({rb.linearVelocity.x:F2}, {rb.linearVelocity.y:F2})" : "(n/a)")}");
    //     GUILayout.Label($"Teleport CD until: {_nextTeleportTime:0.00} (now {Time.time:0.00})");
    //     GUILayout.Label($"Jump CD until: {_nextJumpTime:0.00} | Commit: {(Time.time < _commitUntil)} dir={_commitDirX:F1}");
    //     GUILayout.EndArea();
    // }
}
