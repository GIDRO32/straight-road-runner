using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Movement : MonoBehaviour
{
    float horizontalInput;
    public float runSpeed = 7f;
    float jumpHeight = 10f;
    bool isFacingRight = false;
    bool isOnGround = false;
    bool superJumping = false;
    public Rigidbody2D rb;
    public Collider2D capsule;
    public Animator animator;
    float superRunSpeed = 15f;
    bool isSuperRunning = false;
    public TrailRenderer superRunTrail;
    public GameObject punchHitbox;
    public float punchSpeedReduction = 4f; // Amount to reduce speed during punch
    private bool isPunching = false;
    public GameObject uppercutHitbox; // Reference to the uppercut hitbox
    public float uppercutSpeedReduction = 2f; // Slowdown when uppercutting
    private bool isUppercutting = false; // Track uppercut state
    private PlayerData playerData;   // ← NEW

    private bool isDashing = false;
    private float dashSpeedMultiplier = 2f; // Speed multiplier for dashing
    private float dashTime = 0.2f; // Duration of the dash
    private float dashCooldown = 0.5f; // Cooldown time before another dash
    private float lastDashTime = -1f; // Track last dash time
    private float lastInputTime = 0f; // Time of last directional input
    private KeyCode lastKey; // Store last key pressed
    public BoxCollider2D playerCollider;
    private Transform currentPlatform;   // ← NEW
    private Rigidbody2D platformRb;   // ← NEW

    private bool isDucking = false;
    private bool canSuperJump = false;
    private float duckStartTime = 0f;
    private bool isChargingSuperJump = false;
    private float duckTime = 0f;
    private float superJumpChargeTime = 1f;
    private float superJumpHeight = 15f;
    private float stunTime = 2f;
    private bool isStunned = false;

    private Vector2 defaultColliderSize;
    private Vector2 defaultColliderOffset;

    void Start()
    {
        rb.GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        superRunTrail = GetComponent<TrailRenderer>(); // Ensure the Trail Renderer is attached
        superRunTrail.enabled = true; // Disable initially
        punchHitbox.SetActive(false);
        uppercutHitbox.SetActive(false);
        defaultColliderSize = playerCollider.size;
        defaultColliderOffset = playerCollider.offset;
        Debug.Log(canSuperJump);
        playerData = GetComponent<PlayerData>();  // ← ADD
        if (playerData != null)
            playerData.UpdateStaminaUI();
    }
    void Update()
    {
        if (isStunned) return;

        bool holdingDown = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        // --- Duck & Charge SuperJump ---
        if (holdingDown && isOnGround && !isDucking)
        {
            StartDucking();
        }
        else if (!holdingDown && isDucking)
        {
            StopDucking();
        }

        // Charge SuperJump while ducking
        if (isDucking && isOnGround)
        {
            duckTime += Time.deltaTime;
            if (duckTime >= superJumpChargeTime && !isChargingSuperJump)
            {
                isChargingSuperJump = true;
                canSuperJump = true;
                animator.SetBool("CanSuperJump", true); // Optional visual feedback
                Debug.Log("SuperJump charged!");
            }
        }

        // Perform SuperJump on C press while charged
        if (Input.GetKeyDown(KeyCode.C) && canSuperJump && isOnGround && isDucking && playerData.currentStamina >= 30f)
        {
            playerData.currentStamina -= Mathf.Max(0, playerData.currentStamina - 30f);
            playerData.UpdateStaminaUI();
            PerformSuperJump();
        }

        if (isDucking) return; // Block other inputs while ducking

        if (Input.GetKeyDown(KeyCode.C) && canSuperJump && isOnGround && isDucking)
        {

            rb.velocity = new Vector2(rb.velocity.x, superJumpHeight);
            animator.SetTrigger("IsSuperJumping");
            superJumping = true;
            Debug.Log("SuperJump activated!");
        }

        horizontalInput = Input.GetAxisRaw("Horizontal");
        FlipSprite();
        if (rb.velocity.y > 0)
        {
            capsule.enabled = false;
        }
        else
        {
            capsule.enabled = true;
        }
        // Horizontal movement via Arrow keys or A/D
        if ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) && !isDucking)
        {
            horizontalInput = -1f;
        }
        else if ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) && !isDucking)
        {
            horizontalInput = 1f;
        }
        else
        {
            horizontalInput = 0f;
        }

        // SuperRun logic
        isSuperRunning = Input.GetKey(KeyCode.LeftShift) && isOnGround;
        animator.SetBool("IsSuperRunning", isSuperRunning);
        superRunTrail.emitting = isSuperRunning;

        // Jumping logic
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && isOnGround)
        {
            float jumpSpeed = isSuperRunning ? superRunSpeed : runSpeed;
            rb.velocity = new Vector2(horizontalInput * jumpSpeed, jumpHeight);
            isOnGround = false;
            superJumping = isSuperRunning;
            animator.SetBool("IsJumping", !isOnGround);
        }

        // Punching logic (independent of movement)
        if (Input.GetKeyDown(KeyCode.Z) && !isPunching && !isUppercutting)
        {
            isPunching = true;
            animator.SetTrigger("IsPunching");

            // Reduce speed temporarily
            runSpeed -= punchSpeedReduction;
        }
        if (Input.GetKeyDown(KeyCode.X) && !isUppercutting && !isPunching)
        {
            isUppercutting = true;
            animator.SetTrigger("IsUpperCutting");

            // Reduce movement speed during uppercut
            runSpeed -= uppercutSpeedReduction;
        }
        float previousInput = horizontalInput;
        horizontalInput = Input.GetAxisRaw("Horizontal");
        // Horizontal movement via Arrow keys or A/D
        FlipSprite();
        if (!isOnGround && (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) ||
                Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
        {
            float currentTime = Time.time;
            KeyCode currentKey = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? KeyCode.A : KeyCode.D;

            if (lastKey == currentKey && currentTime - lastInputTime < 0.25f && playerData.currentStamina >= 5) // Double tap detected
            {
                playerData.currentStamina = Mathf.Max(0, playerData.currentStamina - 5f); // Reduce stamina by 10
                playerData.UpdateStaminaUI();
                StartCoroutine(SideDash(horizontalInput));
            }
            else
            {
                Debug.Log("Not enough stamina for Dash!");
            }

            lastKey = currentKey;
            lastInputTime = currentTime;
        }

        horizontalInput = Input.GetAxisRaw("Horizontal");
        FlipSprite();

        // Regular Jump (only if not ducking and not SuperJump)
        if (Input.GetButtonDown("Jump") && isOnGround && !isDucking && !canSuperJump)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpHeight);
            animator.SetBool("IsJumping", true);
        }

    }
    private IEnumerator SideDash(float direction)
    {
        if (Time.time - lastDashTime > dashCooldown && !isOnGround) // Only dash mid-air
        {
            isDashing = true;
            animator.SetBool("IsDashing", true);
            superRunTrail.emitting = true;

            float originalSpeed = runSpeed;
            runSpeed *= dashSpeedMultiplier; // Increase speed

            rb.velocity = new Vector2(runSpeed * direction, rb.velocity.y);
            lastDashTime = Time.time; // Reset cooldown timer

            yield return new WaitForSeconds(dashTime); // Dash duration

            isDashing = false;
            runSpeed = originalSpeed; // Reset speed after dash
            animator.SetBool("IsDashing", false);
            superRunTrail.emitting = false;
        }
    }

    public void EndPunch()
    {
        isPunching = false;
        runSpeed += punchSpeedReduction; // Restore original speed
        Debug.Log("Punch is finished!");

        // Disable hitboxes
        punchHitbox.SetActive(false);
    }
    public void ActivateHitbox()
    {
        punchHitbox.SetActive(true);
    }

    public void DeactivateHitbox()
    {
        // Disable hitboxes
        punchHitbox.SetActive(false);
    }
    public void EndUppercut()
    {
        isUppercutting = false;
        runSpeed += uppercutSpeedReduction; // Restore speed
        uppercutHitbox.SetActive(false);
        Debug.Log("Uppercut is finished!");
    }

    public void ActivateUppercutHitbox()
    {
        uppercutHitbox.SetActive(true);
    }

    public void DeactivateUppercutHitbox()
    {
        uppercutHitbox.SetActive(false);
    }

    void FixedUpdate()
    {
        float currentRunSpeed = runSpeed;
        isOnGround = Mathf.Abs(rb.velocity.y) < 0.01f;

        if (isPunching || isUppercutting)
        {
            currentRunSpeed -= punchSpeedReduction;
        }
        else
        {
            currentRunSpeed = isSuperRunning || superJumping ? superRunSpeed : runSpeed;
        }
        if (isDashing)
        {
            currentRunSpeed *= dashSpeedMultiplier;
        }
        if (isSuperRunning)
        {
            playerData.currentStamina = Mathf.Max(0, playerData.currentStamina - 0.1f); // Drain stamina
            playerData.UpdateStaminaUI();
            if (playerData.currentStamina <= 0f)
            {
                isSuperRunning = false; // Stop super running if out of stamina
            }
        }

        if (horizontalInput != 0)
        {
            float platformVelX = (platformRb != null) ? platformRb.velocity.x : 0f;  // ← ADD
            rb.velocity = new Vector2(horizontalInput * currentRunSpeed + platformVelX, rb.velocity.y);  // ← CHANGE
            animator.SetFloat("XSpeed", Math.Abs(rb.velocity.x));
        }
        else
        {
            float platformVelX = (platformRb != null) ? platformRb.velocity.x : 0f;  // ← ADD
            rb.velocity = new Vector2(platformVelX, rb.velocity.y);  // ← CHANGE
            animator.SetFloat("XSpeed", 0f);
        }

        animator.SetFloat("YHeight", rb.velocity.y);
    }

    void FlipSprite()
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
        duckStartTime = Time.time;
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
        Debug.Log("SuperJump cancelled!");
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
        StopDucking(); // Exit duck state after jump
        Debug.Log("SuperJump activated!");
    }

    private IEnumerator StunPlayer(float duration)
    {
        isStunned = true;
        animator.SetBool("IsStunned", true);
        yield return new WaitForSeconds(duration);
        isStunned = false;
        animator.SetBool("IsStunned", false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Platform"))
        {
            isOnGround = true;
            superJumping = false; // Reset super jump state
            animator.SetBool("IsJumping", false);
        }
        // if (collision.CompareTag("Enemy") && !isStunned && rb.velocity.y < 0)
        // {
        //     rb.velocity = new Vector2(rb.velocity.x, superJumpHeight * 0.4f);
        //     Destroy(collision.gameObject);
        // }
        if (collision.CompareTag("Ground"))
        {
            rb.velocity = new Vector2(rb.velocity.x, superJumpHeight * 1.5f); // Very high jump
            StartCoroutine(StunPlayer(stunTime));
        }
        isOnGround = true;
        animator.SetBool("IsJumping", false);
        animator.SetBool("IsSuperJumping", false);
    }
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Platform"))
        {
            transform.SetParent(col.transform);
            currentPlatform = col.transform;
            platformRb = col.gameObject.GetComponent<Rigidbody2D>();  // ← ADD
        }
        if (col.gameObject.CompareTag("Enemy") && !isStunned && isOnGround && !isDashing)
        {
            superJumping = false; // Reset super jump state
            StartCoroutine(StunPlayer(1f));
        }
    }
    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Platform"))
        {
            transform.SetParent(null);
            currentPlatform = null;
            platformRb = null;  // ← ADD

            // Optional: tiny snap to avoid micro-jitter
            Vector3 pos = transform.position;
            pos.x = Mathf.Round(pos.x * 100f) / 100f;
            transform.position = pos;
        }
    }

}
