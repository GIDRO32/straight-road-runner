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
    public BoxCollider2D playerCollider;
    // ✅ Updated SuperRun structure
    public GameObject superRunObject; // Parent empty object
    public TrailRenderer superRunTrail;
    public ParticleSystem superRunParticles;

    [Header("Particle Effects")]
    public ParticleSystem landingParticles;
    public ParticleSystem punchParticles;

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
    private float superJumpChargeTime = 0.66f;
    private float superJumpHeight = 15f;
    private float stunTime = 2f;
    private float dashTime = 0.2f;
    private float dashCooldown = 0.5f;
    private float lastDashTime = -1f;
    private float lastInputTime = 0f;
    private float dashSpeedMultiplier = 2f;
    [Header("Speed Limiter")]
    public bool enableSpeedLimiter = true;
    private float baseRunSpeed; // Store original run speed

    private KeyCode lastKey;
    private PlayerData playerData;
    private Vector2 defaultColliderSize;
    private Vector2 defaultColliderOffset;
    [Header("Sound Effects")]
    public SoundEffects soundEffects;

    private bool punchHitSomething = false;
    private bool uppercutHitSomething = false;
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // ✅ Auto-find if not assigned
        if (superRunObject == null)
            superRunObject = transform.Find("SuperRun")?.gameObject;

        if (superRunTrail == null && superRunObject != null)
            superRunTrail = superRunObject.GetComponentInChildren<TrailRenderer>();

        if (superRunParticles == null && superRunObject != null)
            superRunParticles = superRunObject.GetComponentInChildren<ParticleSystem>();

        if (superRunTrail != null) superRunTrail.enabled = true;
        if (superRunParticles != null) superRunParticles.Stop(); // Start stopped

        punchHitbox.SetActive(false);
        uppercutHitbox.SetActive(false);
        defaultColliderSize = playerCollider.size;
        defaultColliderOffset = playerCollider.offset;

        baseRunSpeed = runSpeed;

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
                soundEffects?.PlaySuperJumpCharge();
                punchParticles.transform.position = new Vector3(transform.position.x, transform.position.y, 0);
                punchParticles.Play();
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
            soundEffects?.PlaySuperJump();
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
        // SuperRun logic
        isSuperRunning = Input.GetKey(KeyCode.LeftShift) && isOnGround;
        animator.SetBool("IsSuperRunning", isSuperRunning);

        // ✅ Control both trail and particles
        if (superRunTrail != null)
            superRunTrail.emitting = isSuperRunning;

        if (superRunParticles != null)
        {
            if (isSuperRunning && !superRunParticles.isPlaying)
                superRunParticles.Play();
            else if (!isSuperRunning && superRunParticles.isPlaying)
                superRunParticles.Stop();
        }

        // Jumping
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && isOnGround)
        {
            float jumpSpeed = isSuperRunning ? superRunSpeed : runSpeed;
            rb.velocity = new Vector2(horizontalInput * jumpSpeed, jumpHeight);
            isOnGround = false;
            superJumping = isSuperRunning;
            animator.SetBool("IsJumping", true);
        }

        // Modify StartPunch
        if (Input.GetKeyDown(KeyCode.Z) && !isPunching && !isUppercutting)
        {
            isPunching = true;
            punchHitSomething = false; // Reset hit flag
            animator.SetTrigger("TriggerPunch");
            runSpeed -= punchSpeedReduction;

            // Play whoosh sound immediately
            soundEffects?.PlayWhoosh();
        }

        // Modify StartUppercut
        if (Input.GetKeyDown(KeyCode.X) && !isUppercutting && !isPunching)
        {
            isUppercutting = true;
            uppercutHitSomething = false; // Reset hit flag
            animator.SetTrigger("TriggerUppercut");
            runSpeed -= uppercutSpeedReduction;

            // Play whoosh sound immediately
            soundEffects?.PlayWhoosh();
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

        if (horizontalInput < 0)
        {
            // Player's desired velocity + platform velocity
            float desiredVelocityX = horizontalInput * currentRunSpeed;
            rb.velocity = new Vector2(desiredVelocityX, rb.velocity.y);
            animator.SetFloat("XSpeed", Mathf.Abs(horizontalInput * currentRunSpeed));
        }
        else if (horizontalInput > 0)
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
        animator.SetBool("IsSuperJumping", true);
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
        isStunned = true;
        animator.SetBool("IsStunned", true);

        // ✅ Force reset all combat states
        if (isPunching)
        {
            isPunching = false;
            punchHitbox.SetActive(false);
        }
        if (isUppercutting)
        {
            isUppercutting = false;
            uppercutHitbox.SetActive(false);
        }

        // ✅ Reset speed to base
        runSpeed = baseRunSpeed;

        yield return new WaitForSeconds(duration);

        isStunned = false;
        animator.SetBool("IsStunned", false);
    }

    public void EndPunch()
    {
        isPunching = false;

        // ✅ Safety: Only add back if we actually subtracted
        if (runSpeed < baseRunSpeed)
        {
            runSpeed += punchSpeedReduction;
            // Clamp to base speed
            runSpeed = Mathf.Min(runSpeed, baseRunSpeed);
        }

        punchHitbox.SetActive(false);
        punchHitSomething = false;
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

        // ✅ Safety: Only add back if we actually subtracted
        if (runSpeed < baseRunSpeed)
        {
            runSpeed += uppercutSpeedReduction;
            // Clamp to base speed
            runSpeed = Mathf.Min(runSpeed, baseRunSpeed);
        }

        uppercutHitbox.SetActive(false);
        uppercutHitSomething = false;
    }
    public void OnPunchHit()
    {
        if (!punchHitSomething)
        {
            punchHitSomething = true;
            soundEffects?.PlayPunch();
        }
    }

    public void OnUppercutHit()
    {
        if (!uppercutHitSomething)
        {
            uppercutHitSomething = true;
            soundEffects?.PlayPunch();
        }
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
            soundEffects?.PlayDamageTaken();
            StartCoroutine(StunPlayer(1f));
            if (isPunching)
            {
                EndPunch();
            }
            if (isUppercutting)
            {
                EndUppercut();
            }
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
        if (collision.CompareTag("Platform"))
        {
            bool wasInAir = !isOnGround; // Check if we were airborne

            isOnGround = true;
            superJumping = false;
            animator.SetBool("IsJumping", false);

            // ✅ Play landing particles
            if (wasInAir && landingParticles != null)
            {
                landingParticles.transform.position = new Vector3(
                    transform.position.x,
                    collision.bounds.max.y, // Top of platform
                    transform.position.z
                );
                landingParticles.Play();
            }

            Platform platform = collision.GetComponent<Platform>();
            if (platform != null && wasInAir)
            {
                platform.OnPlayerLanded(transform.position);
            }
        }

        if (collision.CompareTag("Ground"))
        {
            soundEffects?.PlayGroundHit();
            rb.velocity = new Vector2(rb.velocity.x, superJumpHeight * 1.5f);
            StartCoroutine(StunPlayer(stunTime));
            if (isPunching)
            {
                EndPunch();
            }
            if (isUppercutting)
            {
                EndUppercut();
            }
        }

        isOnGround = true;
        animator.SetBool("IsJumping", false);
        animator.SetBool("IsSuperJumping", false);
    }
}