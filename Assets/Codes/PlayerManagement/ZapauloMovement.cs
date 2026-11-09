using UnityEngine;

/// <summary>
/// Zapaulo's moveset. Put this on Zapaulo's prefab instead of Movement.
/// Keeps Run, Jump, Punch (Z) and Uppercut (X) from Movement; has no Super Run, Duck, Super Jump or Air Dash.
/// - Aggressive Scratching (hold Shift): all-around hitbox, drains stamina per second.
/// - Platform Grasp (hold C): pulls Zapaulo onto the nearest platform above him, drains stamina per second.
///
/// Animator parameters used on top of Movement's (XSpeed, YHeight, IsJumping, IsStunned, TriggerPunch, TriggerUppercut):
/// IsScratching (bool), IsGrasping (bool).
/// </summary>
public class ZapauloMovement : Movement
{
    [Header("Aggressive Scratching (hold)")]
    public KeyCode scratchKey = KeyCode.LeftShift;
    [Tooltip("Child hitbox around Zapaulo, tagged \"Hits\" with a trigger collider. Off unless scratching.")]
    public GameObject scratchHitbox;
    public float scratchStaminaPerSecond = 20f;
    [Tooltip("The hitbox is re-armed this often, so enemies staying inside are hit again")]
    public float scratchHitInterval = 0.25f;

    [Header("Platform Grasp (hold)")]
    public KeyCode graspKey = KeyCode.C;
    public float graspStaminaPerSecond = 70f;
    [Tooltip("How far away a platform can be grabbed")]
    public float graspRange = 12f;
    [Tooltip("How fast Zapaulo is pulled to the platform")]
    public float graspSpeed = 25f;
    [Tooltip("The platform's top must be at least this far above Zapaulo's feet")]
    public float graspMinHeight = 0.3f;
    [Tooltip("Keep the landing spot this far inside the platform's edges")]
    public float graspEdgeMargin = 0.5f;

    private bool isScratching = false;
    private float scratchTimer = 0f;

    private bool isGrasping = false;
    private Collider2D graspTarget;
    private float defaultGravityScale;

    public bool IsScratching => isScratching;
    public bool IsGrasping => isGrasping;

    private bool HasStamina => playerData == null || playerData.currentStamina > 0f;

    protected override void Start()
    {
        base.Start();
        defaultGravityScale = rb.gravityScale;
        if (scratchHitbox != null) scratchHitbox.SetActive(false);
    }

    protected override void Update()
    {
        if (isStunned) return;

        float horizontalInput = ReadHorizontalInput();
        FlipSprite(horizontalInput);

        HandleGrasp();
        if (isGrasping) return; // Nothing else while being pulled up

        HandleJump(horizontalInput);
        HandleAttacks();
        HandleScratching();
    }

    // No punching or uppercutting in the middle of a scratch or grasp
    protected override bool CanAttack => base.CanAttack && !isScratching && !isGrasping;

    protected override void FixedUpdate()
    {
        if (isGrasping && !isStunned)
        {
            PullTowardsPlatform();
            return;
        }

        base.FixedUpdate();
    }

    // ===== AGGRESSIVE SCRATCHING =====

    private void HandleScratching()
    {
        bool wantsToScratch = Input.GetKey(scratchKey) && !isPunching && !isUppercutting && HasStamina;

        if (wantsToScratch && !isScratching) StartScratching();
        else if (!wantsToScratch && isScratching) StopScratching();

        if (!isScratching) return;

        DrainStamina(scratchStaminaPerSecond * Time.deltaTime);
        if (!HasStamina)
        {
            StopScratching();
            return;
        }

        // Re-arm the hitbox so enemies that stay inside get hit again
        scratchTimer += Time.deltaTime;
        if (scratchTimer >= scratchHitInterval && scratchHitbox != null)
        {
            scratchTimer = 0f;
            scratchHitbox.SetActive(false);
            scratchHitbox.SetActive(true);
            soundEffects?.PlayWhoosh();
        }
    }

    private void StartScratching()
    {
        isScratching = true;
        scratchTimer = 0f;
        if (scratchHitbox != null) scratchHitbox.SetActive(true);
        animator.SetBool("IsScratching", true);
        soundEffects?.PlayWhoosh();
    }

    private void StopScratching()
    {
        if (!isScratching) return;

        isScratching = false;
        if (scratchHitbox != null) scratchHitbox.SetActive(false);
        animator.SetBool("IsScratching", false);
    }

    public override void OnOtherHitboxHit(Hitbox hitbox)
    {
        if (scratchHitbox != null && hitbox.gameObject == scratchHitbox)
            soundEffects?.PlayPunch();
    }

    // ===== PLATFORM GRASP =====

    private void HandleGrasp()
    {
        if (!isGrasping)
        {
            if (Input.GetKeyDown(graspKey) && HasStamina)
                TryStartGrasp();
            return;
        }

        // Let go, out of stamina, or the platform broke / scrolled away
        if (!Input.GetKey(graspKey) || !HasStamina || graspTarget == null || !graspTarget.enabled)
        {
            StopGrasp();
            return;
        }

        DrainStamina(graspStaminaPerSecond * Time.deltaTime);
    }

    private bool TryStartGrasp()
    {
        Collider2D target = FindPlatformAbove();
        if (target == null) return false;

        // Cancel whatever else was going on
        StopScratching();
        if (isPunching) EndPunch();
        if (isUppercutting) EndUppercut();

        isGrasping = true;
        graspTarget = target;
        superJumping = false;
        rb.gravityScale = 0f;

        animator.SetBool("IsGrasping", true);
        animator.SetBool("IsJumping", false);
        soundEffects?.PlayWhoosh();
        return true;
    }

    private void StopGrasp()
    {
        if (!isGrasping) return;

        isGrasping = false;
        graspTarget = null;
        rb.gravityScale = defaultGravityScale;
        animator.SetBool("IsGrasping", false);
    }

    /// <summary>Nearest solid platform whose top is above Zapaulo's feet, within graspRange.</summary>
    private Collider2D FindPlatformAbove()
    {
        float feetY = playerCollider.bounds.min.y;
        Collider2D best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, graspRange))
        {
            if (col.isTrigger || !col.CompareTag("Platform")) continue;
            if (col.bounds.max.y < feetY + graspMinHeight) continue;

            float distance = Vector2.Distance(rb.position, LandingPoint(col));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = col;
            }
        }

        return best;
    }

    /// <summary>Where Zapaulo's transform should be so his feet stand on top of the platform.</summary>
    private Vector2 LandingPoint(Collider2D platform)
    {
        Bounds b = platform.bounds;
        float margin = Mathf.Min(graspEdgeMargin, b.extents.x);
        float x = Mathf.Clamp(rb.position.x, b.min.x + margin, b.max.x - margin);
        float feetOffset = rb.position.y - playerCollider.bounds.min.y;
        return new Vector2(x, b.max.y + feetOffset + 0.05f);
    }

    private void PullTowardsPlatform()
    {
        if (graspTarget == null)
        {
            StopGrasp();
            return;
        }

        Vector2 target = LandingPoint(graspTarget);
        Vector2 toTarget = target - rb.position;
        float step = graspSpeed * Time.fixedDeltaTime;

        if (toTarget.magnitude <= step)
        {
            // Arrived: stand on the platform and move with it
            rb.position = target;
            Rigidbody2D platformRb = graspTarget.attachedRigidbody;
            rb.velocity = platformRb != null ? platformRb.velocity : Vector2.zero;
            currentPlatformRb = platformRb;
            isOnGround = true;
            animator.SetBool("IsJumping", false);
            StopGrasp();
        }
        else
        {
            rb.velocity = toTarget.normalized * graspSpeed;
        }

        animator.SetFloat("YHeight", rb.velocity.y);
    }

    // ===== SHARED =====

    protected override void OnStunned()
    {
        StopScratching();
        StopGrasp();
    }

    private void DrainStamina(float amount)
    {
        if (playerData == null) return;

        playerData.currentStamina = Mathf.Max(0f, playerData.currentStamina - amount);
        playerData.UpdateStaminaUI();
    }
}
