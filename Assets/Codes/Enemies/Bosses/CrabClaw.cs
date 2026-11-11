using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One claw of the Black Ink Crab. BlackInkCrab gives it spots to strike (each already marked
/// with a red WarningCircle); the claw strikes them one after another:
/// 1. Approach: the claw opens wide and moves near the spot.
/// 2. Lock: it stops and shakes, the circle turns solid red. Get out of the circle now!
/// 3. Lunge: the open claw shoots at the spot. Still harmless.
/// 4. Snap: the claw snaps shut. Only this moment hurts, and only inside the Snap Zone.
/// 5. Hold, then the next spot, or back to the body when there are none left.
///
/// Positions are in the claw object's space (= the crab rig's space), so the claw follows the crab.
/// The arm is a chain of Segment copies stretched from the Shoulder to the Head (wrist).
/// </summary>
public class CrabClaw : MonoBehaviour
{
    public enum Phase { Idle, Approach, Lock, Lunge, Hold, Retract }

    private struct Strike
    {
        public Vector3 point;          // World
        public WarningCircle circle;
    }

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
    [Tooltip("Before lunging, the claw waits this far short of the spot")]
    public float hoverDistance = 2.2f;
    [Tooltip("The lunge goes this far past the spot")]
    public float overshoot = 0.3f;
    [Tooltip("Max rotation of the claw away from its drawn direction")]
    public float maxTurn = 80f;

    [Header("Idle")]
    public float idleSway = 0.3f;
    public float idleSwaySpeed = 1.6f;

    [Header("Strike Timing (seconds, BlackInkCrab overrides approach/lock per phase)")]
    public float approachTime = 0.55f;
    public float lockTime = 0.35f;
    public float lungeTime = 0.14f;
    public float snapTime = 0.06f;
    public float holdTime = 0.2f;
    public float retractTime = 0.6f;
    [Tooltip("Shake while locked (world units)")]
    public float lockShake = 0.06f;

    [Header("Drawing")]
    [Tooltip("Added to the pincer's sorting orders while it attacks, so it's drawn over the crab's head and the player")]
    public int attackSortingBoost = 14;

    public Phase CurrentPhase { get; private set; } = Phase.Idle;
    /// <summary>Has spots left to strike or is still moving back.</summary>
    public bool IsBusy => CurrentPhase != Phase.Idle || queue.Count > 0;
    public bool IsAttacking => CurrentPhase == Phase.Approach || CurrentPhase == Phase.Lock || CurrentPhase == Phase.Lunge;

    private BlackInkCrab crab;
    private bool stopped;
    private float phaseTimer;
    private readonly Queue<Strike> queue = new Queue<Strike>();
    private Strike current;

    private Vector3 restPos;
    private float fingerRest;
    private Vector2 snapOffset;   // Snap point in head space
    private float restAngle;     // Direction wrist → snap point at rotation 0

    private float headAngle;
    private Vector3 velocity;
    private Vector3 hoverPoint, lockedPoint; // World
    private Vector3 lockWrist;              // Claw space
    private Vector3 lungeFrom;
    private Vector3 retractFrom;
    private float retractAngleFrom, retractFingerFrom;
    private float fingerAngle;
    private float swayPhase;

    private readonly List<SpriteRenderer> segments = new List<SpriteRenderer>();
    private SpriteRenderer[] renderers;
    private int[] baseOrders;
    private int armBaseOrder;

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

        // Only the pincer (palm + finger) is lifted while attacking; the arm keeps its order
        renderers = head.GetComponentsInChildren<SpriteRenderer>(true);
        baseOrders = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseOrders[i] = renderers[i].sortingOrder;
    }

    // ===== CONTROL (from BlackInkCrab) =====

    /// <summary>Adds a spot (world position) to strike. The circle is locked and popped by the claw.</summary>
    public void QueueStrike(Vector3 worldPoint, WarningCircle circle)
    {
        if (stopped) return;
        queue.Enqueue(new Strike { point = worldPoint, circle = circle });
        if (CurrentPhase == Phase.Idle || CurrentPhase == Phase.Retract) StartNextStrike();
    }

    /// <summary>Whether a spot (world) is within this claw's reach.</summary>
    public bool CanReach(Vector3 worldPoint, float margin = 0.3f)
    {
        return Vector2.Distance(shoulder.position, worldPoint) <= maxReach - margin;
    }

    /// <summary>Stop attacking for good (crab defeated): drop the spots and go back to rest.</summary>
    public void Stop()
    {
        stopped = true;
        while (queue.Count > 0) PopCircle(queue.Dequeue().circle);
        PopCircle(current.circle);
        if (CurrentPhase != Phase.Idle && CurrentPhase != Phase.Retract) StartRetract();
    }

    // ===== UPDATE =====

    void Update()
    {
        phaseTimer += Time.deltaTime;

        switch (CurrentPhase)
        {
            case Phase.Idle: UpdateIdle(); break;
            case Phase.Approach: UpdateApproach(); break;
            case Phase.Lock: UpdateLock(); break;
            case Phase.Lunge: UpdateLunge(); break;
            case Phase.Hold:
                if (phaseTimer >= holdTime)
                {
                    if (queue.Count > 0) StartNextStrike();
                    else StartRetract();
                }
                break;
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

    private void StartNextStrike()
    {
        current = queue.Dequeue();
        SetBoost(true);

        // Come in along the line from the shoulder to the spot
        Vector3 from = shoulder.position;
        Vector3 dir = current.point - from;
        dir.z = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.left;
        hoverPoint = ClampReach(current.point - dir * hoverDistance);
        lockedPoint = ClampReach(current.point + dir * overshoot);

        Enter(Phase.Approach);
    }

    private void UpdateApproach()
    {
        Vector3 hover = ToLocal(hoverPoint);
        AimAt(ToLocal(current.point), hover);
        Vector3 wrist = hover - (Vector3)Rotate(snapOffset, headAngle);
        head.localPosition = Vector3.SmoothDamp(head.localPosition, wrist, ref velocity, approachTime * 0.3f);
        fingerAngle = Mathf.Lerp(fingerAngle, fingerOpenAngle, Time.deltaTime * 10f);

        if (phaseTimer >= approachTime)
        {
            lockWrist = head.localPosition;
            if (current.circle != null) current.circle.Lock();
            Enter(Phase.Lock);
        }
    }

    private void UpdateLock()
    {
        // Stop, turn to the spot and shake: the circle is solid now
        Vector3 locked = ToLocal(lockedPoint);
        AimAt(locked, lockWrist + (Vector3)Rotate(snapOffset, headAngle));
        head.localPosition = lockWrist + (Vector3)(Random.insideUnitCircle * WorldToLocal(lockShake));
        fingerAngle = fingerOpenAngle - 4f;

        if (phaseTimer >= lockTime)
        {
            head.localPosition = lockWrist;
            lungeFrom = lockWrist;
            Enter(Phase.Lunge);
        }
    }

    private void UpdateLunge()
    {
        float k = Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, lungeTime));
        Vector3 lungeTo = ToLocal(lockedPoint) - (Vector3)Rotate(snapOffset, headAngle);
        head.localPosition = Vector3.Lerp(lungeFrom, lungeTo, k * k); // Speeds up

        if (k >= 1f)
        {
            // Snap!
            fingerAngle = Mathf.MoveTowards(fingerAngle, fingerClosedAngle,
                Mathf.Abs(fingerClosedAngle - fingerOpenAngle) * Time.deltaTime / Mathf.Max(0.01f, snapTime));
            if (Mathf.Approximately(fingerAngle, fingerClosedAngle))
            {
                PopCircle(current.circle);
                current.circle = null;
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
            Enter(Phase.Idle);
        }
    }

    private void Enter(Phase phase)
    {
        CurrentPhase = phase;
        phaseTimer = 0f;
        velocity = Vector3.zero;
    }

    private static void PopCircle(WarningCircle circle)
    {
        if (circle != null) circle.Pop();
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

    private void SetBoost(bool on)
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].sortingOrder = baseOrders[i] + (on ? attackSortingBoost : 0);
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

    // World point, kept within reach of the shoulder
    private Vector3 ClampReach(Vector3 world)
    {
        Vector3 from = shoulder.position;
        Vector3 d = world - from;
        d.z = 0f;
        return d.magnitude > maxReach ? from + d.normalized * maxReach : new Vector3(world.x, world.y, from.z);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }
}
