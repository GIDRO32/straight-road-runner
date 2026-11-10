using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Smoke Hound (Volcanic Desert). Lives on platforms:
/// - Appears on a random platform (off-screen ones too) in a puff of smoke.
/// - Stands for a moment, then jumps in an arc to another platform. Mid-jump, if the player is close,
///   it dashes at them; on contact the player takes damage. After the dash it arcs to a new platform.
/// - First hit: vanishes in smoke and reappears on a random platform. Second hit: dies (damage animation + fade).
///
/// Setup: kinematic Rigidbody2D; Body = trigger collider on this object (takes punches, hurts the player while dashing);
/// Feet = trigger collider on a child (recognises the platform it stands on).
/// Animator: int parameter "State" (0 Idle, 1 Jump, 2 Dash, 3 Damage).
/// Hits to kill come from the EnemyDefinition's Health (2).
/// </summary>
public class SmokeHound : EnemyBase
{
    private enum State { Hidden, Standing, Jumping, Dashing, Dead }

    private static readonly int StateParam = Animator.StringToHash("State");
    private const int AnimIdle = 0, AnimJump = 1, AnimDash = 2, AnimDamage = 3;

    [Header("Colliders")]
    [Tooltip("Trigger on this object: takes punches, hurts the player while dashing")]
    public Collider2D bodyCollider;
    [Tooltip("Trigger on a child at the paws: recognises the platform it stands on")]
    public Collider2D feetSensor;

    [Header("Standing")]
    public float standTimeMin = 0.6f;
    public float standTimeMax = 1.4f;
    [Tooltip("Keep this far inside a platform's edges")]
    public float edgeMargin = 0.6f;

    [Header("Jumping")]
    public float minJumpDistance = 3f;
    public float maxJumpDistance = 14f;
    [Tooltip("Arc height above the higher of start / landing")]
    public float jumpHeight = 3f;
    [Tooltip("Seconds for a jump of ~8 units (scales a little with distance)")]
    public float jumpDuration = 0.9f;

    [Header("Mid-air Dash")]
    [Tooltip("Dashes when the player is within this distance during a jump")]
    public float dashRange = 9f;
    [Tooltip("Part of the jump (0-1) in which the dash can start")]
    public Vector2 dashWindow = new Vector2(0.25f, 0.7f);
    public float dashSpeed = 20f;
    public float dashDuration = 0.35f;

    [Header("Teleport (first hit)")]
    [Tooltip("Seconds hidden in smoke before reappearing")]
    public float teleportDelay = 0.45f;

    [Header("Smoke")]
    [Tooltip("Optional particle prefab. Empty = built-in smoke puff.")]
    public GameObject smokeEffectPrefab;
    public float smokeSize = 1.5f;

    [Header("Death")]
    public float deathGravity = 2f;

    private State state = State.Hidden;
    private Platform standingOn;
    private float standOffsetX;
    private float standTimer;

    private Platform jumpTarget;
    private float jumpTargetOffsetX;
    private Vector2 jumpStart;
    private float jumpTime;
    private float jumpLength;
    private bool dashedThisJump;

    private Vector2 dashDirection;
    private float dashTimer;
    private bool dashHitPlayer;

    private float feetOffset; // transform.y - feet bottom

    public override void Init(EnemyDefinition def)
    {
        base.Init(def);
    }

    protected override void Awake()
    {
        base.Awake();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
    }

    protected override void OnSpawned()
    {
        // StageControl spawned us somewhere off to the side: appear on a random platform instead
        SetVisible(false);
        StartCoroutine(AppearOnRandomPlatform(0f));
    }

    // ===== UPDATE =====

    void FixedUpdate()
    {
        switch (state)
        {
            case State.Standing: UpdateStanding(); break;
            case State.Jumping: UpdateJumping(); break;
            case State.Dashing: UpdateDashing(); break;
        }
    }

    private void UpdateStanding()
    {
        // Platform gone, broken or no longer under our feet: leave
        if (!IsUsable(standingOn) || !FeetOnPlatform(standingOn))
        {
            StartJump();
            return;
        }

        rb.MovePosition(LandingPoint(standingOn, standOffsetX));
        FacePlayer();

        standTimer -= Time.fixedDeltaTime;
        if (standTimer <= 0f)
            StartJump();
    }

    private void UpdateJumping()
    {
        if (!IsUsable(jumpTarget))
        {
            StartJump(); // Target broke or left: pick another from here
            return;
        }

        jumpTime += Time.fixedDeltaTime / jumpLength;
        float t = Mathf.Clamp01(jumpTime);

        Vector2 end = LandingPoint(jumpTarget, jumpTargetOffsetX);
        float peak = Mathf.Max(jumpStart.y, end.y) + jumpHeight;
        Vector2 flat = Vector2.Lerp(jumpStart, end, t);
        float lift = 4f * (peak - Mathf.Lerp(jumpStart.y, end.y, 0.5f)) * t * (1f - t);
        Vector2 position = new Vector2(flat.x, flat.y + lift);

        Face(end.x - rb.position.x);
        rb.MovePosition(position);

        if (!dashedThisJump && t >= dashWindow.x && t <= dashWindow.y && PlayerInRange(dashRange))
        {
            StartDash();
            return;
        }

        if (t >= 1f)
            Land(jumpTarget, jumpTargetOffsetX);
    }

    private void UpdateDashing()
    {
        rb.MovePosition(rb.position + dashDirection * dashSpeed * Time.fixedDeltaTime);

        dashTimer -= Time.fixedDeltaTime;
        if (dashTimer <= 0f)
            StartJump(); // Missed (or done): arc to a new platform
    }

    // ===== MOVES =====

    private void Land(Platform platform, float offsetX)
    {
        state = State.Standing;
        standingOn = platform;
        standOffsetX = offsetX;
        standTimer = Random.Range(standTimeMin, standTimeMax);
        rb.MovePosition(LandingPoint(platform, offsetX));
        SetAnim(AnimIdle);
    }

    private void StartJump()
    {
        Platform target = PickJumpTarget();
        if (target == null)
        {
            // Nowhere to jump: vanish and come back somewhere else
            StartCoroutine(Vanish());
            return;
        }

        state = State.Jumping;
        jumpTarget = target;
        jumpTargetOffsetX = RandomOffsetOn(target);
        jumpStart = rb.position;
        jumpTime = 0f;
        dashedThisJump = false;

        float distance = Vector2.Distance(jumpStart, LandingPoint(target, jumpTargetOffsetX));
        jumpLength = jumpDuration * Mathf.Clamp(distance / 8f, 0.6f, 1.5f);

        standingOn = null;
        SetAnim(AnimJump);
    }

    private void StartDash()
    {
        if (player == null) return;

        state = State.Dashing;
        dashedThisJump = true;
        dashHitPlayer = false;
        dashTimer = dashDuration;
        dashDirection = ((Vector2)player.position - rb.position).normalized;
        Face(dashDirection.x);
        SetAnim(AnimDash);
        PlayAttackSound();
    }

    // ===== HURTING THE PLAYER =====

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (!TouchesBody(collision)) return; // Ignore the feet sensor
        base.OnTriggerEnter2D(collision); // Punches ("Hits")
        TryHitPlayer(collision);
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        if (TouchesBody(collision)) TryHitPlayer(collision);
    }

    // Trigger messages arrive for every collider on this rigidbody (body AND feet): only the body counts
    private bool TouchesBody(Collider2D other)
    {
        return bodyCollider == null || feetSensor == null || bodyCollider.IsTouching(other);
    }

    private void TryHitPlayer(Collider2D collision)
    {
        if (state != State.Dashing || dashHitPlayer || !collision.CompareTag("Player")) return;

        Movement movement = collision.GetComponentInParent<Movement>();
        if (movement == null) return;

        if (movement.TakeHit(ContactStaminaDamage, ContactStunDuration))
        {
            dashHitPlayer = true;
            StartJump(); // Got them: arc away to another platform
        }
    }

    // ===== GETTING HIT =====

    public override void TakeHit(int damage, Collider2D source = null)
    {
        if (state == State.Hidden || state == State.Dead) return; // In smoke: can't be hit
        base.TakeHit(damage, source);
    }

    protected override void OnHit(Collider2D source)
    {
        // Not dead yet: vanish in smoke and reappear on a random platform
        if (currentHealth > 0)
            StartCoroutine(Vanish());
    }

    public override void Defeat()
    {
        if (isDefeated) return;

        StopAllCoroutines();
        state = State.Dead;
        SetVisible(true);
        SetAnim(AnimDamage);

        // Fall away (EnemyBase adds the knockback and fades out)
        if (feetSensor != null) feetSensor.enabled = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = deathGravity;
        rb.velocity = Vector2.zero;

        base.Defeat();
    }

    // ===== SMOKE / TELEPORT =====

    private IEnumerator Vanish()
    {
        Smoke(rb.position);
        SetVisible(false);
        yield return AppearOnRandomPlatform(teleportDelay);
    }

    private IEnumerator AppearOnRandomPlatform(float delay)
    {
        state = State.Hidden;
        standingOn = null;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        Platform platform = null;
        while (platform == null)
        {
            platform = RandomPlatform(null);
            if (platform == null) yield return new WaitForSeconds(0.5f); // No platforms yet
        }

        if (feetOffset <= 0f) MeasureFeet();

        float offset = RandomOffsetOn(platform);
        Vector2 point = LandingPoint(platform, offset);
        rb.position = point;
        transform.position = point;

        Smoke(point);
        SetVisible(true);
        Land(platform, offset);
    }

    private void Smoke(Vector2 position)
    {
        int order = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 20;
        SmokePuff.Spawn(position, smokeSize, order, smokeEffectPrefab);
    }

    private void SetVisible(bool visible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
        if (bodyCollider != null) bodyCollider.enabled = visible;
        if (feetSensor != null) feetSensor.enabled = visible;
        if (!visible) rb.velocity = Vector2.zero;
    }

    // ===== PLATFORMS =====

    private static bool IsUsable(Platform platform)
    {
        if (platform == null || platform.IsBroken || !platform.isActiveAndEnabled) return false;
        Collider2D col = platform.GetComponent<Collider2D>();
        return col != null && col.enabled;
    }

    private static List<Platform> UsablePlatforms()
    {
        var result = new List<Platform>();
        foreach (Platform p in FindObjectsOfType<Platform>())
            if (IsUsable(p)) result.Add(p);
        return result;
    }

    private static Platform RandomPlatform(Platform except)
    {
        var all = UsablePlatforms();
        all.Remove(except);
        return all.Count > 0 ? all[Random.Range(0, all.Count)] : null;
    }

    // A different platform within jump range; otherwise the nearest other one
    private Platform PickJumpTarget()
    {
        var all = UsablePlatforms();
        all.Remove(standingOn);

        var inRange = all.FindAll(p =>
        {
            float d = Vector2.Distance(rb.position, p.transform.position);
            return d >= minJumpDistance && d <= maxJumpDistance;
        });
        if (inRange.Count > 0) return inRange[Random.Range(0, inRange.Count)];

        Platform nearest = null;
        float best = float.MaxValue;
        foreach (var p in all)
        {
            float d = Vector2.Distance(rb.position, p.transform.position);
            if (d < best) { best = d; nearest = p; }
        }
        return nearest;
    }

    private float RandomOffsetOn(Platform platform)
    {
        Bounds b = platform.GetComponent<Collider2D>().bounds;
        float half = Mathf.Max(0f, b.extents.x - edgeMargin);
        return (b.center.x - platform.transform.position.x) + Random.Range(-half, half);
    }

    /// <summary>Where our transform goes so the paws stand on top of the platform.</summary>
    private Vector2 LandingPoint(Platform platform, float offsetX)
    {
        Bounds b = platform.GetComponent<Collider2D>().bounds;
        float x = Mathf.Clamp(platform.transform.position.x + offsetX, b.min.x + edgeMargin, b.max.x - edgeMargin);
        return new Vector2(x, b.max.y + feetOffset);
    }

    // Feet sensor overlapping the platform's collider = standing on it
    private bool FeetOnPlatform(Platform platform)
    {
        if (feetSensor == null || !feetSensor.enabled) return true;
        Collider2D platformCollider = platform.GetComponent<Collider2D>();
        return feetSensor.Distance(platformCollider).distance <= 0.2f;
    }

    private void MeasureFeet()
    {
        Collider2D feet = feetSensor != null ? feetSensor : bodyCollider;
        bool wasEnabled = feet != null && feet.enabled;
        if (feet != null) feet.enabled = true;
        Physics2D.SyncTransforms();
        feetOffset = feet != null ? transform.position.y - feet.bounds.min.y : 0.5f;
        if (feet != null) feet.enabled = wasEnabled;
        if (feetOffset <= 0f) feetOffset = 0.5f;
    }

    // ===== HELPERS =====

    private bool PlayerInRange(float range)
    {
        return player != null && Vector2.Distance(rb.position, player.position) <= range;
    }

    private void FacePlayer()
    {
        if (player != null) Face(player.position.x - rb.position.x);
    }

    // The art faces left
    private void Face(float directionX)
    {
        if (spriteRenderer != null && Mathf.Abs(directionX) > 0.01f)
            spriteRenderer.flipX = directionX > 0f;
    }

    private void SetAnim(int value)
    {
        if (animator != null) animator.SetInteger(StateParam, value);
    }
}
