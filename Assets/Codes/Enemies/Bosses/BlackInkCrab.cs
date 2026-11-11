using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Black Ink Crab boss.
/// - Slides in from the right and stands on the floor at the right edge of the screen, walking all the time,
///   so it looks like it's chasing the player. It doesn't follow the camera.
/// - Attacks in volleys: 3-6 random spots get red warning circles, then the two claws strike them one by one
///   (see CrabClaw: approach, lock = circle turns solid, lunge, snap). Only the snap hurts.
///   After a volley it rests 3-5 seconds. Below half health the strikes get faster.
/// - Punches only hurt it on the body/head (hard to reach); signs punched with full stamina fly at it.
/// Rig: Rig (bob) → claws, legs (pivots at the hips), body, head (jaw pivot, eye).
/// </summary>
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

    [Header("Placement")]
    public float baseScale = 0.4f;
    [Tooltip("The right screen edge is measured at this camera size, so zooming doesn't move the crab")]
    public float referenceOrthoSize = 5f;
    [Tooltip("Floor height (top of the floor) when the scene has no object tagged Ground")]
    public float floorY = -3.6f;
    [Tooltip("Distance from the crab's origin to its right edge / bottom (local units)")]
    public float extentRight = 6.95f;
    public float extentBottom = 10.3f;
    [Tooltip("Width of the whole crab, used to start it off-screen (local units)")]
    public float fullWidth = 23f;
    [Tooltip("How much of the crab hangs past the right screen edge / sinks into the floor (local units)")]
    public float rightOverhang = 1.2f;
    public float bottomOverhang = 0.3f;
    [Tooltip("Front of the crab (local x): strike spots stay left of it")]
    public float frontEdge = -9.5f;

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

    [Header("Volleys")]
    public float firstVolleyDelay = 1f;
    [Tooltip("Spots per volley (min, max)")]
    public Vector2Int spotCount = new Vector2Int(3, 6);
    [Tooltip("Rest after a volley (seconds, random in range)")]
    public Vector2 restTime = new Vector2(3f, 5f);
    [Tooltip("All circles are shown this long before the first claw moves")]
    public float warningLead = 0.8f;
    [Tooltip("The second claw starts this much later, so the claws take turns")]
    public float clawStagger = 0.55f;
    [Tooltip("Radius of the warning circles AND of the snap damage (world units). 0 = the claws' Snap Zone radius.")]
    public float circleRadius = 0f;
    [Tooltip("Spots are at least this far apart (world units)")]
    public float minSpotSpacing = 2.2f;
    [Tooltip("Spots stay this far inside the screen (world units)")]
    public float screenMargin = 1f;
    [Tooltip("Chance that one spot of a volley lands where the player is")]
    [Range(0f, 1f)] public float playerSpotChance = 0.35f;
    public float approachTime = 0.55f;
    public float lockTime = 0.35f;

    [Header("Enraged (health at or below this part)")]
    [Range(0f, 1f)] public float enragedHealth = 0.5f;
    public float enragedApproachTime = 0.42f;
    public float enragedLockTime = 0.27f;
    public float enragedWalkSpeed = 2.4f;

    private bool entering = true;
    private float slide = 1f;     // 1 = fully off-screen to the right, 0 = in place
    private float sink;           // Exit: local units down
    private float anchorX, anchorY;
    private float walkTime;
    private Vector3 rigRest;
    private Vector3 eyeRest;
    private float[] legRest;
    private float jawRest;
    private float jawAngle;
    private readonly List<WarningCircle> circles = new List<WarningCircle>();

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

        FindAnchor();
        Place(); // Start off-screen right away
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (var c in circles)
            if (c != null) Destroy(c.gameObject);
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
            Place();
            yield return null;
        }
        slide = 0f;
        Place();
        entering = false;
        StartCoroutine(AttackLoop());
    }

    // ===== ATTACKS =====

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(firstVolleyDelay);
        while (!isDefeated)
        {
            yield return Volley();
            if (isDefeated) yield break;
            yield return new WaitForSeconds(Random.Range(restTime.x, restTime.y));
        }
    }

    private IEnumerator Volley()
    {
        if (claws == null || claws.Length == 0 || player == null) yield break;

        // Faster strikes when enraged
        bool enraged = IsEnraged;
        foreach (var c in claws)
        {
            if (c == null) continue;
            c.approachTime = enraged ? enragedApproachTime : approachTime;
            c.lockTime = enraged ? enragedLockTime : lockTime;
        }

        // Pick the spots and show every circle at once
        int count = Random.Range(spotCount.x, spotCount.y + 1);
        List<Vector3> spots = PickSpots(count);
        if (spots.Count == 0) yield break;

        float radius = circleRadius > 0f ? circleRadius : claws[0].SnapWorldRadius();
        var volley = new List<WarningCircle>();
        foreach (var spot in spots)
        {
            var circle = WarningCircle.Create(spot, radius, 40);
            volley.Add(circle);
            circles.Add(circle);
        }

        yield return new WaitForSeconds(warningLead);

        // Claws take turns: spot 0 → claw A, spot 1 → claw B, ... (the second claw starts a bit later)
        int first = Random.Range(0, claws.Length);
        for (int c = 0; c < claws.Length && !isDefeated; c++)
        {
            CrabClaw claw = claws[(first + c) % claws.Length];
            if (claw == null) continue;
            for (int i = c; i < spots.Count; i += claws.Length)
                claw.QueueStrike(spots[i], volley[i]);
            if (c < claws.Length - 1) yield return new WaitForSeconds(clawStagger);
        }

        // Wait for the claws to finish and come back
        bool busy = true;
        while (busy && !isDefeated)
        {
            busy = false;
            foreach (var c in claws)
                if (c != null && c.IsBusy) busy = true;
            yield return null;
        }

        circles.RemoveAll(c => c == null);
    }

    // Random spots on screen, left of the crab, above the floor, within both claws' reach.
    // Nearest to the crab first, so the claws work their way outwards.
    private List<Vector3> PickSpots(int count)
    {
        var spots = new List<Vector3>();
        Camera cam = Camera.main;
        if (cam == null) return spots;

        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        float front = transform.TransformPoint(new Vector3(frontEdge, 0f, 0f)).x;
        float xMin = c.x - w + screenMargin, xMax = Mathf.Min(c.x + w - screenMargin, front - 0.5f);
        float yMin = Mathf.Max(c.y - h + screenMargin, FloorTop() + 0.6f), yMax = c.y + h - screenMargin;
        if (xMax <= xMin || yMax <= yMin) return spots;

        bool playerSpotUsed = Random.value >= playerSpotChance;
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector3 p;
                if (!playerSpotUsed && player != null)
                {
                    p = (Vector2)player.position + Random.insideUnitCircle * 0.4f;
                    playerSpotUsed = true;
                }
                else p = new Vector3(Random.Range(xMin, xMax), Random.Range(yMin, yMax), 0f);
                p.z = 0f;

                if (!Reachable(p)) continue;
                bool tooClose = false;
                foreach (var s in spots)
                    if (Vector2.Distance(s, p) < minSpotSpacing) { tooClose = true; break; }
                if (tooClose) continue;

                spots.Add(p);
                break;
            }
        }

        Vector3 origin = transform.position;
        spots.Sort((a, b) => Vector2.Distance(a, origin).CompareTo(Vector2.Distance(b, origin)));
        return spots;
    }

    private bool Reachable(Vector3 p)
    {
        foreach (var c in claws)
            if (c != null && !c.CanReach(p)) return false;
        return true;
    }

    /// <summary>A claw snapped shut: hurts the player if they're inside its snap zone.</summary>
    public void OnClawSnap(CrabClaw claw, Vector3 center, float radius)
    {
        PlayAttackSound();
        if (isDefeated) return;
        if (circleRadius > 0f) radius = circleRadius; // Damage area = the warning circle

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

    void Update()
    {
        Walk(IsEnraged ? enragedWalkSpeed : walkSpeed);
    }

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

        // Mouth opens while a claw is striking
        bool attacking = false;
        if (claws != null)
            foreach (var c in claws)
                if (c != null && c.IsAttacking) attacking = true;
        float jawTarget = attacking ? jawOpenAngle : Mathf.Sin(phase * 0.5f) * 1.5f;
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

    // ===== PLACEMENT =====

    // Right screen edge (at the reference zoom), standing on the floor. Measured once: the crab doesn't follow the camera.
    private void FindAnchor()
    {
        transform.localScale = new Vector3(baseScale, baseScale, 1f);

        Camera cam = Camera.main;
        float right = cam != null ? cam.transform.position.x + referenceOrthoSize * cam.aspect : 9f;
        anchorX = right - (extentRight - rightOverhang) * baseScale;
        anchorY = FloorTop() + (extentBottom - bottomOverhang) * baseScale;
    }

    private float FloorTop()
    {
        GameObject ground = GameObject.FindWithTag("Ground");
        Collider2D col = ground != null ? ground.GetComponent<Collider2D>() : null;
        return col != null ? col.bounds.max.y : floorY;
    }

    private void Place()
    {
        transform.position = new Vector3(
            anchorX + slide * fullWidth * baseScale,
            anchorY - sink * baseScale,
            0f);
    }

    // ===== DEFEAT =====

    protected override void OnDefeated()
    {
        if (claws != null)
            foreach (var c in claws)
                if (c != null) c.Stop();
        foreach (var c in circles)
            if (c != null) c.Pop();

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
            Place();
            yield return null;
        }
    }
}
