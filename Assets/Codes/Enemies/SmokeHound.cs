using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Smoke Hound (Volcanic Desert). Lives on on-screen platforms:
/// - Appears on a random on-screen platform in a puff of smoke.
/// - Stands for a few seconds, then jumps in an arc to another on-screen platform. Mid-jump, if the player
///   is close, it dashes at them (one dash per jump). Hit or miss, it then vanishes in smoke and reappears
///   on a random on-screen platform. No dash = it simply lands.
/// - Carried off the left/right of the screen while standing (its platform scrolls away) = it teleports to an
///   on-screen platform. Above/below the screen, and during jumps and dashes, it doesn't.
/// - Only the dash hurts: touching a standing or jumping hound is harmless.
/// - First hit: vanishes in smoke and reappears elsewhere (silent). Second hit: dies (hit sound, damage animation, fade).
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
    [Tooltip("Cooldown between jumps: stands on a platform for a random time in this range (seconds)")]
    public float standTimeMin = 2f;
    public float standTimeMax = 3f;
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

    [Header("Teleport")]
    [Tooltip("Seconds hidden in smoke before reappearing")]
    public float teleportDelay = 0.45f;
    [Tooltip("Platforms closer than this to the screen edge don't count as on-screen")]
    public float screenMargin = 0.5f;

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
    private float landedAt;

    private Platform jumpTarget;
    private float jumpTargetOffsetX;
    private Vector2 jumpStart;
    private float jumpTime;
    private float jumpLength;

    private Vector2 dashDirection;
    private float dashTimer;

    [Header("Getting Hit")]
    [Tooltip("A punch touching the hound for several frames only counts once in this time")]
    public float punchCooldown = 0.3f;
    private float lastPunchTime = -10f;
    private ContactFilter2D punchFilter;
    private readonly Collider2D[] overlapResults = new Collider2D[8];

    private float feetOffset; // transform.y - feet bottom
    private float bodyTop;    // body top - transform.y

    // Touching a hound is harmless: only its mid-air dash hurts
    public override bool DealsContactDamage => false;

    // The first hit only scatters it into smoke: silent. The killing hit plays the hit sound.
    protected override bool ShouldPlayHitSound(bool fatal) => fatal;

    protected override void Awake()
    {
        base.Awake();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        punchFilter = new ContactFilter2D().NoFilter(); // Includes triggers (the player's punch hitboxes)
    }

    protected override void OnSpawned()
    {
        // StageControl spawned us somewhere off to the side: appear on an on-screen platform instead
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

        CheckPunches();
    }

    private void UpdateStanding()
    {
        // Platform gone/broken, or carried off the left/right of the screen: teleport to an on-screen platform
        // (above or below the screen is fine)
        if (!IsUsable(standingOn) || LeavingScreen())
        {
            Teleport();
            return;
        }

        // Feet no longer on the platform: same.
        // (Waits a moment after landing: the Jump clip spins the hound and Idle resets it on the next frame.)
        if (Time.time - landedAt > 0.15f && !FeetOnPlatform(standingOn))
        {
            Teleport();
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
        // Target broke or left the screen, or we did: teleport instead
        // The screen doesn't matter mid-jump; only a broken / vanished target does
        if (!IsUsable(jumpTarget))
        {
            Teleport();
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

        // One dash per jump
        if (t >= dashWindow.x && t <= dashWindow.y && PlayerInRange(dashRange))
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
            Teleport(); // Missed: vanish and reappear on another platform
    }

    // ===== MOVES =====

    private void Land(Platform platform, float offsetX)
    {
        state = State.Standing;
        standingOn = platform;
        standOffsetX = offsetX;
        standTimer = Random.Range(standTimeMin, standTimeMax);
        landedAt = Time.time;
        rb.MovePosition(LandingPoint(platform, offsetX));
        SetAnim(AnimIdle);
    }

    private void StartJump()
    {
        Platform target = PickJumpTarget();
        if (target == null)
        {
            // No other on-screen platform to jump to: keep standing a bit longer
            standTimer = 0.5f;
            return;
        }

        state = State.Jumping;
        jumpTarget = target;
        jumpTargetOffsetX = RandomOffsetOn(target);
        jumpStart = rb.position;
        jumpTime = 0f;

        float distance = Vector2.Distance(jumpStart, LandingPoint(target, jumpTargetOffsetX));
        jumpLength = jumpDuration * Mathf.Clamp(distance / 8f, 0.6f, 1.5f);

        standingOn = null;
        SetAnim(AnimJump);
    }

    private void StartDash()
    {
        if (player == null) return;

        state = State.Dashing;
        dashTimer = dashDuration;
        dashDirection = ((Vector2)player.position - rb.position).normalized;
        Face(dashDirection.x);
        SetAnim(AnimDash);
        PlayAttackSound();
    }

    // ===== HURTING THE PLAYER =====

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Hits"))
        {
            RegisterPunch(collision);
            return;
        }
        if (TouchesBody(collision)) TryHitPlayer(collision); // Ignore the feet sensor
    }

    // Backup for fast, spinning jumps where the trigger "enter" can be missed: check the body every physics step
    private void CheckPunches()
    {
        if (state == State.Hidden || state == State.Dead || bodyCollider == null || !bodyCollider.enabled) return;

        int count = bodyCollider.OverlapCollider(punchFilter, overlapResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = overlapResults[i];
            if (col != null && col.enabled && col.CompareTag("Hits"))
            {
                RegisterPunch(col);
                return;
            }
        }
    }

    // One punch = one hit (it touches the hound for several frames)
    private void RegisterPunch(Collider2D punch)
    {
        if (isDefeated || Time.time - lastPunchTime < punchCooldown) return;
        lastPunchTime = Time.time;
        TakeHit(1, punch);
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

    // Only the dash hurts
    private void TryHitPlayer(Collider2D collision)
    {
        if (state != State.Dashing || !collision.CompareTag("Player")) return;

        Movement movement = collision.GetComponentInParent<Movement>();
        if (movement == null) return;

        if (movement.TakeHit(ContactStaminaDamage, ContactStunDuration))
            Teleport(); // Got them: vanish and reappear on another platform
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
            Teleport();
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

    private void Teleport()
    {
        if (state == State.Hidden || state == State.Dead) return;
        StartCoroutine(Vanish());
    }

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
        jumpTarget = null;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (feetOffset <= 0f) MeasureBody();

        Platform platform = null;
        while (platform == null)
        {
            platform = RandomPlatform();
            if (platform == null) yield return new WaitForSeconds(0.25f); // No platform on screen yet
        }

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

    // Usable platforms that are fully on screen (with room for the hound on top).
    // horizontalOnly: only the left/right edges matter (jumps may go above / below the screen).
    private List<Platform> OnScreenPlatforms(bool horizontalOnly = false)
    {
        var result = new List<Platform>();
        foreach (Platform p in FindObjectsOfType<Platform>())
            if (IsUsable(p) && IsOnScreen(p, horizontalOnly)) result.Add(p);
        return result;
    }

    private Platform RandomPlatform()
    {
        var all = OnScreenPlatforms();
        return all.Count > 0 ? all[Random.Range(0, all.Count)] : null;
    }

    // A different platform within jump range and between the left/right screen edges; otherwise the nearest one
    private Platform PickJumpTarget()
    {
        var all = OnScreenPlatforms(horizontalOnly: true);
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

    private void MeasureBody()
    {
        Collider2D feet = feetSensor != null ? feetSensor : bodyCollider;
        bool feetWasEnabled = feet != null && feet.enabled;
        bool bodyWasEnabled = bodyCollider != null && bodyCollider.enabled;
        if (feet != null) feet.enabled = true;
        if (bodyCollider != null) bodyCollider.enabled = true;
        Physics2D.SyncTransforms();

        feetOffset = feet != null ? transform.position.y - feet.bounds.min.y : 0.5f;
        bodyTop = bodyCollider != null ? bodyCollider.bounds.max.y - transform.position.y : 1.5f;

        if (feet != null) feet.enabled = feetWasEnabled;
        if (bodyCollider != null) bodyCollider.enabled = bodyWasEnabled;
        if (feetOffset <= 0f) feetOffset = 0.5f;
        if (bodyTop <= 0f) bodyTop = 1.5f;
    }

    // ===== SCREEN =====

    private static bool ScreenRect(out Rect rect)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) { rect = default; return false; }
        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        rect = new Rect(c.x - w, c.y - h, w * 2f, h * 2f);
        return true;
    }

    // The whole platform is in view, with room for the hound standing on it
    private bool IsOnScreen(Platform platform, bool horizontalOnly = false)
    {
        if (!ScreenRect(out Rect view)) return true;
        Bounds b = platform.GetComponent<Collider2D>().bounds;
        bool horizontal = b.min.x >= view.xMin + screenMargin && b.max.x <= view.xMax - screenMargin;
        if (horizontalOnly) return horizontal;
        float houndTop = b.max.y + feetOffset + bodyTop;
        return horizontal && b.max.y >= view.yMin + screenMargin && houndTop <= view.yMax;
    }

    // Standing: any part of the body crossing the left or right screen edge (above / below is fine)
    private bool LeavingScreen()
    {
        if (!ScreenRect(out Rect view) || bodyCollider == null || !bodyCollider.enabled) return false;
        Bounds b = bodyCollider.bounds;
        return b.min.x < view.xMin || b.max.x > view.xMax;
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
