using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlaceholderMan : MonoBehaviour
{
    [Header("Movement Settings")]
    public float runSpeed = 7f;
    public float jumpHeight = 10f;
    public float superRunSpeed = 15f;
    
    [Header("Components")]
    public Rigidbody2D rb;
    public Collider2D landingCapsule;
    public Animator animator;
    public TrailRenderer superRunTrail;
    public BoxCollider2D playerCollider;
    
    [Header("Combat")]
    public GameObject punchHitbox;
    public float punchSpeedReduction = 4f;
    public GameObject uppercutHitbox;
    public float uppercutSpeedReduction = 2f;
    
    // Private state variables
    private float horizontalInput;
    private bool isFacingRight = false;
    private bool isGrounded = false;
    private bool isSuperRunning = false;
    
    // Combat state
    private bool isPunching = false;
    private bool isUppercutting = false;
    private bool isStunned = false;
    
    // Platform tracking
    private Transform currentPlatform;
    private Rigidbody2D platformRb;
    private Vector3 lastPlatformPosition;
    
    // Advanced movement state
    private float duckTime = 0f;
    private float superJumpChargeTime = 1f;
    private float superJumpHeight = 15f;
    private float stunTime = 2f;
    private float dashTime = 0.2f;
    private float dashCooldown = 0.5f;
    private float lastDashTime = -1f;
    private float lastInputTime = 0f;
    private float dashSpeedMultiplier = 2f;
    
    private KeyCode lastKey;
    private PlayerData playerData;
    private Vector2 defaultColliderSize;
    private Vector2 defaultColliderOffset;
    
    // Ground check
    private const float GROUND_VELOCITY_THRESHOLD = 0.01f;

    void Start()
    {
        // Auto-assign if not set
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (superRunTrail == null) superRunTrail = GetComponent<TrailRenderer>();
        
        // Initialize
        if (superRunTrail != null) superRunTrail.enabled = true;
        if (punchHitbox != null) punchHitbox.SetActive(false);
        if (uppercutHitbox != null) uppercutHitbox.SetActive(false);
        
        defaultColliderSize = playerCollider.size;
        defaultColliderOffset = playerCollider.offset;
        
        playerData = GetComponent<PlayerData>();
        if (playerData != null)
            playerData.UpdateStaminaUI();
    }

    void Update()
    {
        if (isStunned) return; // Block all input when stunned
        
        HandleInput();
        UpdateAnimations();
    }

    void FixedUpdate()
    {
        CheckGroundStatus();
        ApplyMovement();
    }

    // ===== INPUT HANDLING =====
    void HandleInput()
    {
        // Horizontal input
        horizontalInput = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            horizontalInput = -1f;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            horizontalInput = 1f;
        
        // Super run
        isSuperRunning = Input.GetKey(KeyCode.LeftShift) && isGrounded;
        if (superRunTrail != null)
            superRunTrail.emitting = isSuperRunning;
        
        // Jump
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || 
             Input.GetKeyDown(KeyCode.UpArrow)) && isGrounded)
        {
            PerformJump();
        }
        
        // Punch
        if (Input.GetKeyDown(KeyCode.Z) && !isPunching && !isUppercutting)
        {
            StartPunch();
        }
        
        // Uppercut
        if (Input.GetKeyDown(KeyCode.X) && !isUppercutting && !isPunching)
        {
            StartUppercut();
        }
        
        // Flip sprite based on input direction
        FlipSprite();
    }

    // ===== MOVEMENT =====
    void ApplyMovement()
    {
        float currentSpeed = isSuperRunning ? superRunSpeed : runSpeed;
        
        // Reduce speed during combat moves
        if (isPunching)
            currentSpeed -= punchSpeedReduction;
        if (isUppercutting)
            currentSpeed -= uppercutSpeedReduction;
        
        float targetVelocityX = horizontalInput * currentSpeed;
        
        // Add platform velocity for seamless movement
        float platformVelX = 0f;
        if (platformRb != null && isGrounded)
        {
            platformVelX = platformRb.velocity.x;
        }
        
        rb.velocity = new Vector2(targetVelocityX + platformVelX, rb.velocity.y);
        
        // Drain stamina during super run
        if (isSuperRunning && playerData != null)
        {
            playerData.currentStamina = Mathf.Max(0, playerData.currentStamina - 0.1f);
            playerData.UpdateStaminaUI();
            
            if (playerData.currentStamina <= 0f)
                isSuperRunning = false;
        }
    }

    void PerformJump()
    {
        float jumpSpeed = isSuperRunning ? superRunSpeed : runSpeed;
        rb.velocity = new Vector2(horizontalInput * jumpSpeed, jumpHeight);
        isGrounded = false;
    }

    void FlipSprite()
    {
        if ((isFacingRight && horizontalInput < 0f) || (!isFacingRight && horizontalInput > 0f))
        {
            isFacingRight = !isFacingRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1f;
            transform.localScale = scale;
        }
    }

    // ===== GROUND DETECTION =====
    void CheckGroundStatus()
    {
        // Ground check: vertical velocity near zero
        isGrounded = Mathf.Abs(rb.velocity.y) < GROUND_VELOCITY_THRESHOLD;
        
        // Enable/disable landing capsule based on movement
        if (landingCapsule != null)
        {
            landingCapsule.enabled = rb.velocity.y <= 0; // Only active when falling/landed
        }
    }

    // ===== ANIMATIONS =====
    void UpdateAnimations()
    {
        if (animator == null) return;
        
        animator.SetFloat("XSpeed", Mathf.Abs(rb.velocity.x));
        animator.SetFloat("YHeight", rb.velocity.y);
        animator.SetBool("IsJumping", !isGrounded);
        animator.SetBool("IsSuperRunning", isSuperRunning);
        animator.SetBool("IsStunned", isStunned);
    }

    // ===== COMBAT =====
    void StartPunch()
    {
        isPunching = true;
        animator.SetTrigger("TriggerPunch");
    }

    void StartUppercut()
    {
        isUppercutting = true;
        animator.SetTrigger("TriggerUppercut");
    }

    // Called from animation event
    public void ActivateHitbox()
    {
        if (punchHitbox != null) punchHitbox.SetActive(true);
    }

    public void DeactivateHitbox()
    {
        if (punchHitbox != null) punchHitbox.SetActive(false);
    }

    public void EndPunch()
    {
        isPunching = false;
    }

    public void ActivateUppercutHitbox()
    {
        if (uppercutHitbox != null) uppercutHitbox.SetActive(true);
    }

    public void DeactivateUppercutHitbox()
    {
        if (uppercutHitbox != null) uppercutHitbox.SetActive(false);
    }

    public void EndUppercut()
    {
        isUppercutting = false;
    }

    // ===== STUN SYSTEM =====
    IEnumerator StunPlayer(float duration)
    {
        isStunned = true;
        yield return new WaitForSeconds(duration);
        isStunned = false;
    }

    // ===== PLATFORM PHYSICS =====
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Platform"))
        {
            currentPlatform = col.transform;
            platformRb = col.gameObject.GetComponent<Rigidbody2D>();
            
            if (currentPlatform != null)
                lastPlatformPosition = currentPlatform.position;
            
            isGrounded = true;
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Platform"))
        {
            currentPlatform = null;
            platformRb = null;
        }
    }

    // Trigger detection for landing capsule
    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Platform"))
        {
            isGrounded = true;
        }
        
        // Hit ground - launch player up and stun
        if (col.CompareTag("Ground"))
        {
            rb.velocity = new Vector2(rb.velocity.x, superJumpHeight * 1.5f);
            StartCoroutine(StunPlayer(stunTime));
        }
    }
}