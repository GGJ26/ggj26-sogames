using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement instance;

    [Header("Refs")]
    public Rigidbody2D rb;

    [Header("Move")]
    public float moveSpeed = 6f;

    [Header("Test")]
    public float testSpeed = 60f;

    [Header("Jump")]
    public float jumpForceLegacy = 8f;
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
    public float fallMultiplierNormal = 1.6f;

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

    private Vector2 _moveInput;

    public Vector2 MoveInput
    {
        get => _moveInput;
        set
        {
            _moveInput = value;
        }
    }

    [Header("Gravity")]
    public float fallGravityMult;
	public float maxFallSpeed;

	[HideInInspector] public float gravityStrength;
	[HideInInspector] public float gravityScale;
    [Space(5)]

    [Header("Run")]
	public float runMaxSpeed; //Target speed we want the player to reach.
	public float runAcceleration; //The speed at which our player accelerates to max speed, can be set to runMaxSpeed for instant acceleration down to 0 for none at all
	[HideInInspector] public float runAccelAmount; //The actual force (multiplied with speedDiff) applied to the player.
	public float runDecceleration; //The speed at which our player decelerates from their current speed, can be set to runMaxSpeed for instant deceleration down to 0 for none at all
	[HideInInspector] public float runDeccelAmount; //Actual force (multiplied with speedDiff) applied to the player .
	[Space(5)]
	[Range(0f, 1)] public float accelInAir; //Multipliers applied to acceleration rate when airborne.
	[Range(0f, 1)] public float deccelInAir;
	[Space(5)]
	public bool doConserveMomentum = true;

    [Header("Jump2")]
    public float jumpHeight; //Height of the player's jump
	public float jumpTimeToApex; //Time between applying the jump force and reaching the desired jump height. These values also control the player's gravity and jump force.
	[HideInInspector] public float jumpForce; //The actual force applied (upwards) to the player when they jump.
	public float jumpCutGravityMult; //Multiplier to increase gravity if the player releases thje jump button while still jumping
	[Range(0f, 1)] public float jumpHangGravityMult; //Reduces gravity while close to the apex (desired max height) of the jump
	public float jumpHangTimeThreshold; //Speeds (close to 0) where the player will experience extra "jump hang". The player's velocity.y is closest to 0 at the jump's apex (think of the gradient of a parabola or quadratic function)
	[Space(0.5f)]
	public float jumpHangAccelerationMult = 5f;
	public float jumpHangMaxSpeedMult;
    [Range(0.01f, 0.5f)] public float coyoteTime; // Time we have to trigger a jump when falling from a platform
    [Range(0.01f, 0.5f)] public float jumpInputBufferTime; // To adjust jump player input accuraty 
    [Range(0.01f, 0.5f)] public float testrange; // To adjust jump player input accuraty 

    private float _lastOnGroundTime;
    private float _lastPressedJumpTime;

    private bool _isJumping;
	private bool _isJumpFalling;

    public bool test = true;

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
        /* StartCoroutine(SetMaskChanged()); */
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

        /* // --- Mouvement horizontal ---
        var v = rb.linearVelocity;
        v.x = _moveX * moveSpeed;
        rb.linearVelocity = v; */

        Run(1);

        /* // --- Gravité effective ---
        float signedBase = Mathf.Abs(baseGravityScale) * (_invertGravityEnabled ? -1f : 1f);

        bool fallingWithGravity =
            signedBase > 0f ? (rb.linearVelocity.y < 0f)  // gravité normale => tombe si velY < 0
                            : (rb.linearVelocity.y > 0f); // gravité inversée => tombe (vers le haut) si velY > 0

        float fallMult = _slowFallEnabled ? fallMultiplierFloat : fallMultiplierNormal;

        // Appliquer : montée inchangée, chute modifiée
        rb.gravityScale = fallingWithGravity ? (signedBase * fallMult) : signedBase; */
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

        _lastPressedJumpTime = jumpInputBufferTime;
        Debug.Log("trigger jump " + testrange);
        playerAnimator?.SetJump();

        /* // Reset de la vitesse verticale pour un saut constant
        var v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        // Saut "contre" la gravité
        Vector2 jumpDir = (_invertGravityEnabled ? Vector2.down : Vector2.up);
        rb.AddForce(jumpDir * jumpForceLegacy, ForceMode2D.Impulse);

        // IMPORTANT: ignore le sol pendant quelques ms pour éviter le glitch de départ
        _groundLockUntil = Time.time + groundIgnoreAfterJump;
        _isGrounded = false;

        playerAnimator?.SetJump(); */
    }

    // Public si tu veux l'utiliser ailleurs
    public bool IsGrounded() => _isGrounded;

    private bool IsGroundedInternal()
    {
        // Lock anti-glitch juste après jump
        if (Time.time < _groundLockUntil)
            return false;

        if (groundCheck == null) return false;

        bool isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundMask
        );

        if(_lastOnGroundTime < -0.1f)
        {
            _lastOnGroundTime = coyoteTime;
        }

        return isGrounded;
    }

    private void Jump()
	{
		//Ensures we can't call Jump multiple times from one press
		_lastPressedJumpTime = 0;
		_lastOnGroundTime = 0;
        
		float force = jumpForce;
		if (rb.linearVelocity.y < 0)
			force -= rb.linearVelocity.y;
        Debug.Log("jump : " + (_invertGravityEnabled ? Vector2.down * force : Vector2.up * force));
		rb.AddForce(_invertGravityEnabled ? Vector2.down * 1000 : Vector2.up * force, ForceMode2D.Impulse);
	}

    private bool CanJump()
    {
        Debug.Log("<color=yellow> CanJump <color> last ");
		return _lastOnGroundTime > 0 && !_isJumping;
    }

    private void Run(float lerpAmount)
	{
		float targetSpeed = _moveInput.x * runMaxSpeed;
		targetSpeed = Mathf.Lerp(rb.linearVelocity.x, targetSpeed, lerpAmount);

		// Calculate AccelRate
		float accelRate;

		if (_lastOnGroundTime > 0)
			accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? runAccelAmount : runDeccelAmount;
		else
			accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? runAccelAmount * accelInAir : runDeccelAmount * deccelInAir;

		// Bonus Jump Apex Acceleration
		//Increase are acceleration and maxSpeed when at the apex of their jump, makes the jump feel a bit more bouncy, responsive and natural
		if ((_isJumping || _isJumpFalling) && Mathf.Abs(rb.linearVelocity.y) < jumpHangTimeThreshold)
		{
			accelRate *= jumpHangAccelerationMult;
			targetSpeed *= jumpHangMaxSpeedMult;
		}

		// Conserve Momentum
		if(
            doConserveMomentum && Mathf.Abs(rb.linearVelocity.x) > Mathf.Abs(targetSpeed) 
            && Mathf.Sign(rb.linearVelocity.x) == Mathf.Sign(targetSpeed) && Mathf.Abs(targetSpeed) > 0.01f 
            && _lastOnGroundTime < 0)
		{
			accelRate = 0; 
		}

		float speedDif = targetSpeed - rb.linearVelocity.x;

		float movement = speedDif * accelRate;

		rb.AddForce(movement * Vector2.right, ForceMode2D.Force);
	}

    public void SetGravityScale(float scale)
    {
        if(_invertGravityEnabled) scale *= -1;

        rb.gravityScale = scale;
    }

    void Start()
    {
        SetGravityScale(1);
    }

    void Update()
    {
        _lastOnGroundTime -= Time.deltaTime;

        Debug.Log(Time.deltaTime);
        Debug.Log(_lastPressedJumpTime);

        if (_isJumping && rb.linearVelocity.y < 0)
		{
            Debug.Log("<color=red>Jump</color>");
			_isJumping = false;

			_isJumpFalling = true;
		}

        if (CanJump() && _lastPressedJumpTime > 0)
        {
            Debug.Log("<color=orange>Jump</color>");
            _isJumping = true;
            /* _isJumpCut = false; */
            _isJumpFalling = false;
            Jump();
        }

        // Gravity
        else if ((_isJumping || _isJumpFalling) && Mathf.Abs(rb.linearVelocity.y) < jumpHangTimeThreshold)
        {
            SetGravityScale(gravityScale * jumpHangGravityMult);
        }
        else if (rb.linearVelocity.y < 0)
        {
            //Higher gravity if falling
            SetGravityScale(gravityScale * fallGravityMult);
            //Caps maximum fall speed, TODO check what's the current gravity scale
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, _invertGravityEnabled ? maxFallSpeed : -maxFallSpeed));
        }
        else
        {
            //Default gravity if standing on a platform or moving upwards
			SetGravityScale(gravityScale);
        }

        _lastPressedJumpTime -= Time.deltaTime;
    }

    // Init at the script loading or inspector changing
    void OnValidate()
    {
		gravityStrength = -(2 * jumpHeight) / (jumpTimeToApex * jumpTimeToApex);

		gravityScale = gravityStrength / Physics2D.gravity.y;

		//Calculate are run acceleration & deceleration forces using formula: amount = ((1 / Time.fixedDeltaTime) * acceleration) / runMaxSpeed
		runAccelAmount = 50 * runAcceleration / runMaxSpeed;
		runDeccelAmount = 50 * runDecceleration / runMaxSpeed;

		//Calculate jumpForce using the formula (initialJumpVelocity = gravity * timeToJumpApex)
		jumpForce = Mathf.Abs(gravityStrength) * jumpTimeToApex;

		#region Variable Ranges
		runAcceleration = Mathf.Clamp(runAcceleration, 0.01f, runMaxSpeed);
		runDecceleration = Mathf.Clamp(runDecceleration, 0.01f, runMaxSpeed);
		#endregion
	}
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
#endif
}
