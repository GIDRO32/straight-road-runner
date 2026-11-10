using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Black Ink Crab boss.
/// - Slides in from the right and stays on the right edge of the screen, walking all the time,
///   so it looks like it's chasing the player. Follows the camera (and its zoom).
/// - Its two claws attack on their own (see CrabClaw): aim, lock, lunge, snap. Only the snap hurts.
///   The crab spaces the attacks out so there's always a moment to dodge; below half health it gets faster.
/// - Punches only hurt it on the body/head (hard to reach); signs punched with full stamina fly at it.
/// Rig: Rig (bob) → claws, legs (pivots at the hips), body, head (jaw pivot, eye).
/// </summary>
[DefaultExecutionOrder(100)] // After CameraFollow, so it sticks to the screen edge without lag
public class BlackInkCrab : BossBase
{
    [Header("Rig")]
    [Tooltip("Everything visible; bobs while walking")]
    public Transform rig;
    public CrabClaw[] claws;
    [Tooltip("Leg pivots (at the hips). Alternate legs swing in opposite directions.")]
    public Transform[] legs;
    public Transform jaw;
    public Transform eye;

    [Header("Screen Placement")]
    [Tooltip("Crab scale when the camera's orthographic size equals Reference Ortho Size")]
    public float baseScale = 0.4f;
    public bool scaleWithZoom = true;
    public float referenceOrthoSize = 5f;
    [Tooltip("Distance from the crab's origin to its right edge / bottom (local units)")]
    public float extentRight = 6.95f;
    public float extentBottom = 10.3f;
    [Tooltip("Width of the whole crab, used to start it off-screen (local units)")]
    public float fullWidth = 23f;
    [Tooltip("How much of the crab hangs past the right / bottom screen edge (local units)")]
    public float rightOverhang = 1.2f;
    public float bottomOverhang = 0.6f;

    [Header("Walking")]
    [Tooltip("Steps per second")]
    public float walkSpeed = 1.8f;
    public float legSwing = 8f;
    public float bobHeight = 0.18f;
    public float jawOpenAngle = 10f;
    [Tooltip("How far the pupil looks toward the player (local units)")]
    public float eyeLook = 0.12f;

    [Header("Entrance / Exit")]
    public float introDuration = 2.5f;
    public float exitDuration = 2f;

    [Header("Attacks")]
    public float firstAttackDelay = 1f;
    [Tooltip("Seconds between the start of one claw attack and the next (random in range)")]
    public Vector2 attackGap = new Vector2(1.3f, 2.1f);
    public float aimTime = 0.85f;
    public float lockTime = 0.45f;
    public float lungeTime = 0.16f;

    [Header("Enraged (health at or below this part)")]
    [Range(0f, 1f)] public float enragedHealth = 0.5f;
    public Vector2 enragedAttackGap = new Vector2(0.8f, 1.4f);
    public float enragedAimTime = 0.65f;
    public float enragedLockTime = 0.36f;
    public float enragedWalkSpeed = 2.4f;

    private bool entering = true;
    private bool fighting;
    private float slide = 1f;     // 1 = fully off-screen to the right, 0 = in place
    private float sink;           // Exit: local units down
    private float walkTime;
    private float nextAttackTime;
    private Vector3 rigRest;
    private Vector3 eyeRest;
    private float[] legRest;
    private float jawRest;
    private float jawAngle;

    public override bool IsVulnerable => base.IsVulnerable && !entering;
    public bool IsEnraged => HealthNormalized <= enragedHealth;

    protected override void Awake()
    {
        base.Awake();
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (rig == null) rig = transform;
        rigRest = rig.localPosition;
        if (eye != null) eyeRest = eye.localPosition;
        if (jaw != null) jawRest = jaw.localEulerAngles.z;

        legRest = new float[legs != null ? legs.Length : 0];
        for (int i = 0; i < legRest.Length; i++)
            if (legs[i] != null) legRest[i] = legs[i].localEulerAngles.z;

        if (claws != null)
            foreach (var claw in claws)
                if (claw != null) claw.Init(this);

        PlaceOnScreen(); // Start off-screen right away
    }

    protected override void OnSpawned()
    {
        StartCoroutine(Entrance());
    }

    private IEnumerator Entrance()
    {
        entering = true;
        float t = 0f;
        while (t < introDuration)
        {
            t += Time.deltaTime;
            slide = 1f - Mathf.SmoothStep(0f, 1f, t / introDuration);
            yield return null;
        }
        slide = 0f;
        entering = false;
        fighting = true;
        nextAttackTime = Time.time + firstAttackDelay;
    }

    // ===== UPDATE =====

    void Update()
    {
        bool enraged = IsEnraged;
        Walk(enraged ? enragedWalkSpeed : walkSpeed);

        if (!fighting || isDefeated || player == null) return;

        ApplyPhase(enraged);

        if (Time.time >= nextAttackTime)
        {
            CrabClaw claw = PickReadyClaw();
            if (claw != null)
            {
                claw.Attack(player);
                Vector2 gap = enraged ? enragedAttackGap : attackGap;
                nextAttackTime = Time.time + Random.Range(gap.x, gap.y);
            }
            else nextAttackTime = Time.time + 0.2f;
        }
    }

    void LateUpdate()
    {
        PlaceOnScreen();
    }

    private CrabClaw PickReadyClaw()
    {
        if (claws == null) return null;
        var ready = new List<CrabClaw>();
        foreach (var c in claws)
            if (c != null && c.IsReady) ready.Add(c);
        return ready.Count > 0 ? ready[Random.Range(0, ready.Count)] : null;
    }

    private void ApplyPhase(bool enraged)
    {
        if (claws == null) return;
        foreach (var c in claws)
        {
            if (c == null) continue;
            c.aimTime = enraged ? enragedAimTime : aimTime;
            c.lockTime = enraged ? enragedLockTime : lockTime;
            c.lungeTime = lungeTime;
        }
    }

    /// <summary>A claw snapped shut: hurts the player if they're inside its snap zone.</summary>
    public void OnClawSnap(CrabClaw claw, Vector3 center, float radius)
    {
        PlayAttackSound();
        if (isDefeated) return;

        foreach (Collider2D col in Physics2D.OverlapCircleAll(center, radius))
        {
            if (col.CompareTag("Hits")) continue; // The player's punch hitboxes don't count
            Movement movement = col.GetComponentInParent<Movement>();
            if (movement != null)
            {
                HitPlayer(movement);
                return;
            }
        }
    }

    // ===== LOOK =====

    private void Walk(float speed)
    {
        walkTime += Time.deltaTime * speed;
        float phase = walkTime * Mathf.PI * 2f;

        if (rig != null)
            rig.localPosition = rigRest + new Vector3(0f, Mathf.Sin(phase * 2f) * bobHeight, 0f);

        for (int i = 0; i < legRest.Length; i++)
        {
            if (legs[i] == null) continue;
            float swing = Mathf.Sin(phase + (i % 2 == 0 ? 0f : Mathf.PI) + i * 0.4f) * legSwing;
            legs[i].localRotation = Quaternion.Euler(0f, 0f, legRest[i] + swing);
        }

        // Mouth opens while a claw is aiming
        bool aiming = false;
        if (claws != null)
            foreach (var c in claws)
                if (c != null && c.IsAiming) aiming = true;
        float jawTarget = aiming ? jawOpenAngle : Mathf.Sin(phase * 0.5f) * 1.5f;
        jawAngle = Mathf.MoveTowards(jawAngle, jawTarget, 60f * Time.deltaTime);
        if (jaw != null) jaw.localRotation = Quaternion.Euler(0f, 0f, jawRest + jawAngle);

        // Pupil looks at the player
        if (eye != null && player != null && eye.parent != null)
        {
            Vector3 d = eye.parent.InverseTransformPoint(player.position) - eyeRest;
            d.z = 0f;
            eye.localPosition = eyeRest + Vector3.ClampMagnitude(d, eyeLook);
        }
    }

    // Right screen edge, legs at the bottom, scaled with the camera's zoom
    private void PlaceOnScreen()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float size = cam.orthographicSize;
        float scale = baseScale * (scaleWithZoom ? size / Mathf.Max(0.01f, referenceOrthoSize) : 1f);
        transform.localScale = new Vector3(scale, scale, 1f);

        Vector3 c = cam.transform.position;
        float right = c.x + size * cam.aspect;
        float bottom = c.y - size;

        float x = right - (extentRight - rightOverhang) * scale + slide * fullWidth * scale;
        float y = bottom + (extentBottom - bottomOverhang - sink) * scale;
        transform.position = new Vector3(x, y, 0f);
    }

    // ===== DEFEAT =====

    protected override void OnDefeated()
    {
        fighting = false;
        if (claws != null)
            foreach (var c in claws)
                if (c != null) c.Stop();

        base.OnDefeated();
        StartCoroutine(Exit());
    }

    // Backs off to the right and sinks while fading out (EnemyBase fades and destroys it)
    private IEnumerator Exit()
    {
        float t = 0f;
        while (t < exitDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / exitDuration);
            slide = k * 0.5f;
            sink = k * 4f;
            yield return null;
        }
    }
}
