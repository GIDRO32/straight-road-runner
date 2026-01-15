using System.Collections;
using UnityEngine;

public class Movement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float runSpeed = 7f;
    float jumpHeight = 10f;
    float superRunSpeed = 15f;
    
    [Header("Components")]
    public Rigidbody2D rb;
    public Collider2D capsule;
    public Animator animator;
    public TrailRenderer superRunTrail;
    public BoxCollider2D playerCollider;
    
    [Header("Combat")]
    public GameObject punchHitbox;
    public float punchSpeedReduction = 4f;
    public GameObject uppercutHitbox;
    public float uppercutSpeedReduction = 2f;
    
    // State variables
    private bool isFacingRight = false;
    private bool isOnGround = false;
    private bool superJumping = false;
    private bool isSuperRunning = false;
    private bool isPunching = false;
    private bool isUppercutting = false;
    private bool isDashing = false;
    private bool isDucking = false;
    private bool canSuperJump = false;
    private bool isChargingSuperJump = false;
    private bool isStunned = false;
    
    // Platform tracking - NEW APPROACH
    private Rigidbody2D currentPlatformRb;
    private Vector2 platformVelocity;
    
    // Timers
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
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        superRunTrail = GetComponent<TrailRenderer>();
        superRunTrail.enabled = true;
        punchHitbox.SetActive(false);
        uppercutHitbox.SetActive(false);
        defaultColliderSize = playerCollider.size;
        defaultColliderOffset = playerCollider.offset;
        
        playerData = GetComponent<PlayerData>();
        if (playerData != null)
            playerData.UpdateStaminaUI();
    }
    
    void Update()
    {
        if (isStunned) return;

        bool holdingDown = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        // Duck & Charge SuperJump
        if (holdingDown && isOnGround && !isDucking)
            StartDucking();
        else if (!holdingDown && isDucking)
            StopDucking();

        // Charge SuperJump while ducking
        if (isDucking && isOnGround)
        {
            duckTime += Time.deltaTime;
            if (duckTime >= superJumpChargeTime && !isChargingSuperJump)
            {
                isChargingSuperJump = true;
                canSuperJump = true;
                animator.SetBool("CanSuperJump", true);
            }
        }

        // Perform SuperJump
        if (Input.GetKeyDown(KeyCode.C) && canSuperJump && isOnGround && isDucking && playerData.currentStamina >= 30f)
        {
            playerData.currentStamina = Mathf.Max(0, playerData.currentStamina - 30f);
            playerData.UpdateStaminaUI();
            PerformSuperJump();
        }

        if (isDucking) return;

        // Handle capsule collider for jump-through platforms
        capsule.enabled = rb.velocity.y <= 0;

        // Horizontal movement
        float horizontalInput = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            horizontalInput = -1f;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            horizontalInput = 1f;

        FlipSprite(horizontalInput);

        // SuperRun logic
        isSuperRunning = Input.GetKey(KeyCode.LeftShift) && isOnGround;
        animator.SetBool("IsSuperRunning", isSuperRunning);
        superRunTrail.emitting = isSuperRunning;

        // Jumping
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && isOnGround)
        {
            float jumpSpeed = isSuperRunning ? superRunSpeed : runSpeed;
            rb.velocity = new Vector2(horizontalInput * jumpSpeed, jumpHeight);
            isOnGround = false;
            superJumping = isSuperRunning;
            animator.SetBool("IsJumping", true);
        }

        // Punching
        if (Input.GetKeyDown(KeyCode.Z) && !isPunching && !isUppercutting)
        {
            isPunching = true;
            animator.SetTrigger("TriggerPunch");
            runSpeed -= punchSpeedReduction;
        }

        // Uppercut
        if (Input.GetKeyDown(KeyCode.X) && !isUppercutting && !isPunching)
        {
            isUppercutting = true;
            animator.SetTrigger("TriggerUppercut");
            runSpeed -= uppercutSpeedReduction;
        }

        // Air dash detection
        if (!isOnGround && (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) ||
                Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
        {
            float currentTime = Time.time;
            KeyCode currentKey = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? KeyCode.A : KeyCode.D;

            if (lastKey == currentKey && currentTime - lastInputTime < 0.25f && playerData.currentStamina >= 5)
            {
                playerData.currentStamina = Mathf.Max(0, playerData.currentStamina - 5f);
                playerData.UpdateStaminaUI();
                StartCoroutine(SideDash(horizontalInput));
            }

            lastKey = currentKey;
            lastInputTime = currentTime;
        }
    }

    void FixedUpdate()
    {
        if (isStunned) return;

        // ✅ Get platform velocity BEFORE calculating movement
        UpdatePlatformVelocity();

        float currentRunSpeed = runSpeed;
        isOnGround = Mathf.Abs(rb.velocity.y) < 0.01f;

        if (isPunching || isUppercutting)
            currentRunSpeed -= punchSpeedReduction;
        else
            currentRunSpeed = isSuperRunning || superJumping ? superRunSpeed : runSpeed;

        if (isDashing)
            currentRunSpeed *= dashSpeedMultiplier;

        // Stamina drain for super run
        if (isSuperRunning)
        {
            playerData.currentStamina = Mathf.Max(0, playerData.currentStamina - 0.1f);
            playerData.UpdateStaminaUI();
            if (playerData.currentStamina <= 0f)
            {
                isSuperRunning = false;
                animator.SetBool("IsSuperRunning", false);
                superRunTrail.emitting = false;
            }
        }

        // ✅ Calculate movement with platform velocity
        float horizontalInput = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            horizontalInput = -1f;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            horizontalInput = 1f;

        if (horizontalInput != 0)
        {
            // Player's desired velocity + platform velocity
            float desiredVelocityX = horizontalInput * currentRunSpeed + platformVelocity.x;
            rb.velocity = new Vector2(desiredVelocityX, rb.velocity.y);
            animator.SetFloat("XSpeed", Mathf.Abs(horizontalInput * currentRunSpeed));
        }
        else
        {
            // Standing still - just inherit platform velocity
            rb.velocity = new Vector2(platformVelocity.x, rb.velocity.y);
            animator.SetFloat("XSpeed", 0f);
        }

        animator.SetFloat("YHeight", rb.velocity.y);
    }

    // ✅ NEW METHOD: Update platform velocity
    private void UpdatePlatformVelocity()
    {
        if (currentPlatformRb != null)
        {
            platformVelocity = currentPlatformRb.velocity;
        }
        else
        {
            platformVelocity = Vector2.zero;
        }
    }

    private void FlipSprite(float horizontalInput)
    {
        if (isFacingRight && horizontalInput < 0f || !isFacingRight && horizontalInput > 0f)
        {
            isFacingRight = !isFacingRight;
            Vector3 ls = transform.localScale;
            ls.x *= -1f;
            transform.localScale = ls;
        }
    }

    private void StartDucking()
    {
        isDucking = true;
        duckTime = 0f;
        isChargingSuperJump = false;
        animator.SetBool("IsDucking", true);
        playerCollider.size = new Vector2(playerCollider.size.x, 2f);
        playerCollider.offset = new Vector2(playerCollider.offset.x, -1.3f);
    }

    private void StopDucking()
    {
        isDucking = false;
        canSuperJump = false;
        isChargingSuperJump = false;
        duckTime = 0f;
        animator.SetBool("IsDucking", false);
        animator.SetBool("CanSuperJump", false);
        playerCollider.size = defaultColliderSize;
        playerCollider.offset = defaultColliderOffset;
    }

    private void PerformSuperJump()
    {
        rb.velocity = new Vector2(rb.velocity.x, superJumpHeight);
        animator.SetTrigger("IsSuperJumping");
        superJumping = true;
        canSuperJump = false;
        isChargingSuperJump = false;
        StopDucking();
    }

    private IEnumerator SideDash(float direction)
    {
        if (Time.time - lastDashTime > dashCooldown && !isOnGround)
        {
            isDashing = true;
            animator.SetBool("IsDashing", true);
            superRunTrail.emitting = true;

            float originalSpeed = runSpeed;
            runSpeed *= dashSpeedMultiplier;

            rb.velocity = new Vector2(runSpeed * direction, rb.velocity.y);
            lastDashTime = Time.time;

            yield return new WaitForSeconds(dashTime);

            isDashing = false;
            runSpeed = originalSpeed;
            animator.SetBool("IsDashing", false);
            superRunTrail.emitting = false;
        }
    }

    private IEnumerator StunPlayer(float duration)
    {
        playerData.currentStamina = Mathf.Max(0, playerData.currentStamina - 10f);
        isStunned = true;
        animator.SetBool("IsStunned", true);
        yield return new WaitForSeconds(duration);
        isStunned = false;
        animator.SetBool("IsStunned", false);
    }

    public void EndPunch()
    {
        isPunching = false;
        runSpeed += punchSpeedReduction;
        punchHitbox.SetActive(false);
    }

    public void ActivateHitbox()
    {
        punchHitbox.SetActive(true);
    }

    public void DeactivateHitbox()
    {
        punchHitbox.SetActive(false);
    }

    public void EndUppercut()
    {
        isUppercutting = false;
        runSpeed += uppercutSpeedReduction;
        uppercutHitbox.SetActive(false);
    }

    public void ActivateUppercutHitbox()
    {
        uppercutHitbox.SetActive(true);
    }

    public void DeactivateUppercutHitbox()
    {
        uppercutHitbox.SetActive(false);
    }

    // ✅ SIMPLIFIED COLLISION - No parenting!
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Platform"))
        {
            // Just track the platform's Rigidbody2D
            currentPlatformRb = col.gameObject.GetComponent<Rigidbody2D>();
        }

        if (col.gameObject.CompareTag("Enemy") && !isStunned && isOnGround && !isDashing)
        {
            superJumping = false;
            StartCoroutine(StunPlayer(1f));
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Platform"))
        {
            // Stop tracking this platform
            if (currentPlatformRb == col.gameObject.GetComponent<Rigidbody2D>())
            {
                currentPlatformRb = null;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Platform") && rb.velocity.y <= 0)
        {
            isOnGround = true;
            superJumping = false;
            animator.SetBool("IsJumping", false);
        }

        if (collision.CompareTag("Ground"))
        {
            rb.velocity = new Vector2(rb.velocity.x, superJumpHeight * 1.5f);
            StartCoroutine(StunPlayer(stunTime));
        }

        isOnGround = true;
        animator.SetBool("IsJumping", false);
        animator.SetBool("IsSuperJumping", false);
    }
}