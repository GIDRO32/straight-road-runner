using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One claw of the Black Ink Crab. Moves on its own; BlackInkCrab decides when it attacks.
///
/// An attack, built to be readable and dodgeable:
/// 1. Aim: the claw opens wide and follows the player, hovering a little short of them.
///    A red ring marks the spot it's aiming at.
/// 2. Lock: the claw stops following and shakes, the ring turns solid. Get out of the ring now!
/// 3. Lunge: the open claw shoots at the locked spot. Still harmless.
/// 4. Snap: the claw snaps shut. Only this moment hurts, and only inside the Snap Zone.
/// 5. Hold, then retract to the body.
///
/// Positions are in the claw object's space (= the crab rig's space), so the claw follows the crab.
/// The arm is a chain of Segment copies stretched from the Shoulder to the Head (wrist).
/// </summary>
public class CrabClaw : MonoBehaviour
{
    public enum Phase { Idle, Aim, Lock, Lunge, Hold, Retract }

    [Header("Rig")]
    [Tooltip("Pivot at the wrist. Its rotation 0 is the rest pose.")]
    public Transform head;
    [Tooltip("Hinge of the moving finger")]
    public Transform fingerPivot;
    [Tooltip("Where the arm leaves the body")]
    public Transform shoulder;
    [Tooltip("Arm segment, copied as needed when the claw reaches out")]
    public SpriteRenderer armSegment;
    [Tooltip("Angle of the segment sprite's long axis (degrees)")]
    public float armAxisAngle = 0f;
    [Tooltip("Length of the segment sprite along its axis (local units)")]
    public float armSegmentLength = 3.6f;
    [Tooltip("Between the fingers: the only part that hurts, at the moment of the snap")]
    public CircleCollider2D snapZone;

    [Header("Fingers (degrees, relative to the drawn pose)")]
    public float fingerIdleAngle = 0f;
    public float fingerOpenAngle = -12f;
    public float fingerClosedAngle = 20f;

    [Header("Reach (world units)")]
    [Tooltip("How far the snap point can get from the shoulder")]
    public float maxReach = 15f;
    [Tooltip("While aiming, the claw hovers this far short of the player")]
    public float hoverDistance = 2.5f;
    [Tooltip("The lunge goes this far past the locked spot")]
    public float overshoot = 0.4f;
    [Tooltip("Max rotation of the claw away from its drawn direction")]
    public float maxTurn = 80f;

    [Header("Idle")]
    public float idleSway = 0.3f;
    public float idleSwaySpeed = 1.6f;

    [Header("Attack Timing (seconds, BlackInkCrab overrides them per phase)")]
    public float aimTime = 0.8f;
    public float lockTime = 0.45f;
    public float lungeTime = 0.16f;
    public float snapTime = 0.06f;
    public float holdTime = 0.35f;
    public float retractTime = 0.55f;
    [Tooltip("Rest after retracting before this claw can attack again")]
    public float cooldown = 0.5f;
    [Tooltip("Shake while locked (world units)")]
    public float lockShake = 0.06f;

    [Header("Drawing")]
    [Tooltip("Added to the pincer's sorting orders while it attacks, so it's drawn over the crab's head and the player")]
    public int attackSortingBoost = 14;
    public Color markerAimColor = new Color(1f, 0.15f, 0.15f, 0.35f);
    public Color markerLockColor = new Color(1f, 0.1f, 0.1f, 0.9f);

    public Phase CurrentPhase { get; private set; } = Phase.Idle;
    public bool IsReady => CurrentPhase == Phase.Idle && !stopped && Time.time >= readyTime;
    public bool IsAiming => CurrentPhase == Phase.Aim || CurrentPhase == Phase.Lock;

    private BlackInkCrab crab;
    private Transform target;
    private bool stopped;
    private float readyTime;
    private float phaseTimer;

    private Vector3 restPos;
    private float fingerRest;
    private Vector2 snapOffset;   // Snap point in head space
    private float restAngle;     // Direction wrist → snap point at rotation 0

    private float headAngle;
    private Vector3 velocity;
    private Vector3 lockedSnapPoint;    // Claw space
    private Vector3 lockWrist;
    private Vector3 lungeFrom, lungeTo; // Wrist positions
    private Vector3 retractFrom;
    private float retractAngleFrom, retractFingerFrom;
    private float fingerAngle;
    private float swayPhase;

    private readonly List<SpriteRenderer> segments = new List<SpriteRenderer>();
    private SpriteRenderer[] renderers;
    private int[] baseOrders;
    private int armBaseOrder;
    private SpriteRenderer marker;

    public void Init(BlackInkCrab owner)
    {
        crab = owner;
    }

    void Awake()
    {
        restPos = head.localPosition;
        fingerRest = fingerPivot != null ? fingerPivot.localEulerAngles.z : 0f;
        if (snapZone != null)
        {
            Vector3 p = head.InverseTransformPoint(snapZone.transform.TransformPoint(snapZone.offset));
            snapOffset = new Vector2(p.x * head.localScale.x, p.y * head.localScale.y);
        }
        else snapOffset = Vector2.left;
        restAngle = Mathf.Atan2(snapOffset.y, snapOffset.x) * Mathf.Rad2Deg;
        fingerAngle = fingerIdleAngle;
        swayPhase = Random.value * 10f;

        if (armSegment != null)
        {
            segments.Add(armSegment);
            armBaseOrder = armSegment.sortingOrder;
        }
        CacheRenderers();

        marker = new GameObject(name + " Target").AddComponent<SpriteRenderer>();
        marker.sprite = RingSprite();
        marker.sortingOrder = 47; // Above the lifted pincers
        marker.enabled = false;
    }

    void OnDestroy()
    {
        if (marker != null) Destroy(marker.gameObject);
    }

    // ===== CONTROL (from BlackInkCrab) =====

    public void Attack(Transform player)
    {
        if (player == null || !IsReady) return;
        target = player;
        SetBoost(true);
        Enter(Phase.Aim);
    }

    /// <summary>Stop attacking for good (crab defeated): go back to rest and stay there.</summary>
    public void Stop()
    {
        stopped = true;
        if (CurrentPhase != Phase.Idle && CurrentPhase != Phase.Retract) StartRetract();
        if (marker != null) marker.enabled = false;
    }

    // ===== UPDATE =====

    void Update()
    {
        float dt = Time.deltaTime;
        phaseTimer += dt;

        switch (CurrentPhase)
        {
            case Phase.Idle: UpdateIdle(); break;
            case Phase.Aim: UpdateAim(dt); break;
            case Phase.Lock: UpdateLock(); break;
            case Phase.Lunge: UpdateLunge(); break;
            case Phase.Hold: if (phaseTimer >= holdTime) StartRetract(); break;
            case Phase.Retract: UpdateRetract(); break;
        }

        ApplyPose();
    }

    void LateUpdate()
    {
        UpdateArm();
    }

    private void UpdateIdle()
    {
        float t = Time.time * idleSwaySpeed + swayPhase;
        head.localPosition = restPos + new Vector3(Mathf.Sin(t), Mathf.Cos(t * 1.3f) * 0.6f, 0f) * idleSway;
        headAngle = Mathf.Sin(t * 0.8f) * 4f;
        fingerAngle = fingerIdleAngle + Mathf.Sin(t * 1.7f) * 3f;
    }

    private void UpdateAim(float dt)
    {
        if (target == null) { StartRetract(); return; }

        // Hover a bit short of the player, pointing at them
        Vector3 player = ToLocal(target.position);
        Vector3 from = shoulder.localPosition;
        Vector3 dir = player - from;
        float dist = dir.magnitude;
        dir = dist > 0.001f ? dir / dist : Vector3.left;

        Vector3 snapPoint = ClampReach(player - dir * WorldToLocal(hoverDistance));
        AimAt(player, snapPoint);
        Vector3 wrist = snapPoint - (Vector3)Rotate(snapOffset, headAngle);
        head.localPosition = Vector3.SmoothDamp(head.localPosition, wrist, ref velocity, 0.18f, Mathf.Infinity, dt);

        fingerAngle = Mathf.Lerp(fingerIdleAngle, fingerOpenAngle, Mathf.Clamp01(phaseTimer / (aimTime * 0.5f)));
        ShowMarker(target.position, markerAimColor, 0.8f);

        if (phaseTimer >= aimTime)
        {
            lockedSnapPoint = ClampReach(player + dir * WorldToLocal(overshoot));
            lockWrist = head.localPosition;
            Enter(Phase.Lock);
        }
    }

    private void UpdateLock()
    {
        // Stop following: turn to the locked spot, shake in place and show the solid ring where it will strike
        AimAt(lockedSnapPoint, lockWrist + (Vector3)Rotate(snapOffset, headAngle));
        head.localPosition = lockWrist + (Vector3)(Random.insideUnitCircle * WorldToLocal(lockShake));
        fingerAngle = fingerOpenAngle - 4f;
        ShowMarker(transform.TransformPoint(lockedSnapPoint), markerLockColor, 1f);

        if (phaseTimer >= lockTime)
        {
            head.localPosition = lockWrist;
            lungeFrom = lockWrist;
            lungeTo = lockedSnapPoint - (Vector3)Rotate(snapOffset, headAngle);
            Enter(Phase.Lunge);
        }
    }

    private void UpdateLunge()
    {
        float k = Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, lungeTime));
        head.localPosition = Vector3.Lerp(lungeFrom, lungeTo, k * k); // Speeds up

        if (k >= 1f)
        {
            // Snap!
            fingerAngle = Mathf.MoveTowards(fingerAngle, fingerClosedAngle,
                Mathf.Abs(fingerClosedAngle - fingerOpenAngle) * Time.deltaTime / Mathf.Max(0.01f, snapTime));
            if (Mathf.Approximately(fingerAngle, fingerClosedAngle))
            {
                if (marker != null) marker.enabled = false;
                if (crab != null) crab.OnClawSnap(this, SnapWorldPosition(), SnapWorldRadius());
                Enter(Phase.Hold);
            }
        }
    }

    private void StartRetract()
    {
        retractFrom = head.localPosition;
        retractAngleFrom = headAngle;
        retractFingerFrom = fingerAngle;
        if (marker != null) marker.enabled = false;
        Enter(Phase.Retract);
    }

    private void UpdateRetract()
    {
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, retractTime)));
        head.localPosition = Vector3.Lerp(retractFrom, restPos, k);
        headAngle = Mathf.LerpAngle(retractAngleFrom, 0f, k);
        fingerAngle = Mathf.Lerp(retractFingerFrom, fingerIdleAngle, k);

        if (k >= 1f)
        {
            SetBoost(false);
            readyTime = Time.time + cooldown;
            Enter(Phase.Idle);
        }
    }

    private void Enter(Phase phase)
    {
        CurrentPhase = phase;
        phaseTimer = 0f;
        velocity = Vector3.zero;
    }

    // ===== POSE =====

    // Turn the claw so the wrist → snap point direction points at the target
    private void AimAt(Vector3 targetPoint, Vector3 snapPoint)
    {
        Vector3 wrist = snapPoint - (Vector3)Rotate(snapOffset, headAngle);
        Vector3 d = targetPoint - wrist;
        if (d.sqrMagnitude < 0.0001f) return;
        float desired = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float turn = Mathf.Clamp(Mathf.DeltaAngle(restAngle, desired), -maxTurn, maxTurn);
        headAngle = Mathf.MoveTowardsAngle(headAngle, turn, 360f * Time.deltaTime);
    }

    private void ApplyPose()
    {
        head.localRotation = Quaternion.Euler(0f, 0f, headAngle);
        if (fingerPivot != null)
            fingerPivot.localRotation = Quaternion.Euler(0f, 0f, fingerRest + fingerAngle);
    }

    // Arm: segment copies evenly spaced from the shoulder to the wrist
    private void UpdateArm()
    {
        if (armSegment == null || shoulder == null) return;

        Vector3 a = shoulder.localPosition;
        Vector3 b = head.localPosition;
        Vector3 d = b - a;
        float length = d.magnitude;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        int count = Mathf.Max(1, Mathf.CeilToInt((length - armSegmentLength * 0.2f) / (armSegmentLength * 0.8f)));
        while (segments.Count < count) AddSegment();

        for (int i = 0; i < segments.Count; i++)
        {
            bool on = i < count;
            segments[i].enabled = on;
            if (!on) continue;

            Vector3 p = a + d * ((i + 0.5f) / count);
            Transform t = segments[i].transform;
            t.position = transform.TransformPoint(p);
            t.rotation = transform.rotation * Quaternion.Euler(0f, 0f, angle - armAxisAngle);
        }
    }

    private void AddSegment()
    {
        SpriteRenderer copy = Instantiate(armSegment, armSegment.transform.parent);
        copy.name = armSegment.name + " " + segments.Count;
        copy.sortingOrder = armBaseOrder;
        segments.Add(copy);
    }

    // Only the pincer (palm + finger) is lifted while attacking; the arm keeps its order
    private void CacheRenderers()
    {
        renderers = head.GetComponentsInChildren<SpriteRenderer>(true);
        baseOrders = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseOrders[i] = renderers[i].sortingOrder;
    }

    private void SetBoost(bool on)
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].sortingOrder = baseOrders[i] + (on ? attackSortingBoost : 0);
    }

    private void ShowMarker(Vector3 world, Color color, float pulse)
    {
        if (marker == null) return;
        marker.enabled = true;
        marker.transform.position = new Vector3(world.x, world.y, 0f);
        float r = SnapWorldRadius() * 2.2f;
        float s = r * (1f + (1f - pulse) * 0.15f * Mathf.Sin(Time.time * 18f));
        marker.transform.localScale = Vector3.one * s;
        marker.color = color;
    }

    // ===== HELPERS =====

    public Vector3 SnapWorldPosition() => head.TransformPoint(snapOffset.x / head.localScale.x, snapOffset.y / head.localScale.y, 0f);

    public float SnapWorldRadius()
    {
        if (snapZone == null) return 1f;
        return snapZone.radius * Mathf.Abs(snapZone.transform.lossyScale.x);
    }

    private Vector3 ToLocal(Vector3 world)
    {
        Vector3 p = transform.InverseTransformPoint(world);
        p.z = 0f;
        return p;
    }

    private float WorldToLocal(float d) => d / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));

    private Vector3 ClampReach(Vector3 snapPoint)
    {
        Vector3 from = shoulder.localPosition;
        Vector3 d = snapPoint - from;
        float max = WorldToLocal(maxReach);
        return d.magnitude > max ? from + d.normalized * max : snapPoint;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }

    // Red target ring, generated once
    private static Sprite ring;
    private static Sprite RingSprite()
    {
        if (ring != null) return ring;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.12f);   // Ring
                a = Mathf.Max(a, d < 0.75f ? 0.25f : 0f);                       // Soft fill
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size); // 1 unit wide
        return ring;
    }
}
