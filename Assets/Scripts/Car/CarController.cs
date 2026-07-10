using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// NavMeshAgent-based autonomous car movement.
/// Uses look-ahead targeting for smooth cornering, curvature-based speed
/// control, forward collision detection, and per-car racing line offsets.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CarIdentity))]
public class CarController : MonoBehaviour
{
    private NavMeshAgent agent;
    private WaypointPath waypointPath;
    private int currentWaypointIndex;
    private float baseSpeed;

    // Lateral offset — persistent per car for consistent racing line
    private float lateralOffsetRange;
    private float persistentLateralOffset;

    // Look-ahead and curvature
    private float lookAheadDistance;
    private float curveSlowdownFactor;
    private float curvatureSpeedMultiplier = 1f;

    // Forward collision detection
    private float forwardDetectionRange;
    private float forwardSlowdownFactor;
    private float forwardSpeedMultiplier = 1f;

    // Stuck detection
    private float stuckTimeThreshold;
    private float stuckDistanceThreshold;
    private float stuckRecoveryOffset;
    private Vector3 lastCheckedPosition;
    private float stuckTimer;
    private int consecutiveStuckCount;
    private bool isRecovering;

    // Inelastic collision (trigger-based)
    private float collisionSpeedFactor;
    private float collisionRecoveryTime;
    private int collisionCount;
    private float collisionSpeedMultiplier = 1f;

    // Event speed modifier stacking
    private int activeModifierCount;

    private const int MaxRecoveryAttempts = 3;

    public void Initialize(WaypointPath path, float speed, float angularSpeed, float acceleration,
                           float lateralOffset = 3f, float stuckTime = 2f,
                           float stuckDist = 0.5f, float recoveryOffset = 5f,
                           float colSpeedFactor = 0.4f, float colRecoveryTime = 1f,
                           float lookAhead = 15f, float curveSlowdown = 0.5f,
                           float fwdDetectRange = 12f, float fwdSlowdown = 0.6f)
    {
        agent = GetComponent<NavMeshAgent>();
        waypointPath = path;
        baseSpeed = speed;
        lateralOffsetRange = lateralOffset;
        stuckTimeThreshold = stuckTime;
        stuckDistanceThreshold = stuckDist;
        stuckRecoveryOffset = recoveryOffset;
        collisionSpeedFactor = colSpeedFactor;
        collisionRecoveryTime = colRecoveryTime;
        lookAheadDistance = lookAhead;
        curveSlowdownFactor = curveSlowdown;
        forwardDetectionRange = fwdDetectRange;
        forwardSlowdownFactor = fwdSlowdown;

        agent.speed = speed;
        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
        agent.autoBraking = false;

        // Randomize avoidance priority so cars yield differently instead of deadlocking
        agent.avoidancePriority = Random.Range(20, 80);

        // Each car picks a persistent racing line offset — small random per-waypoint
        // variation is added on top, but the base lane stays consistent
        persistentLateralOffset = Random.Range(-lateralOffsetRange, lateralOffsetRange);

        currentWaypointIndex = FindClosestWaypointIndex();
        lastCheckedPosition = transform.position;
        stuckTimer = 0f;
        consecutiveStuckCount = 0;
        isRecovering = false;

        SetNextDestination();
    }

    private int FindClosestWaypointIndex()
    {
        float minDist = float.MaxValue;
        int closest = 0;
        Vector3 pos = transform.position;

        for (int i = 0; i < waypointPath.Count; i++)
        {
            Transform wp = waypointPath.GetWaypoint(i);
            float dist = Vector3.SqrMagnitude(wp.position - pos);
            if (dist < minDist)
            {
                minDist = dist;
                closest = i;
            }
        }

        // Target the NEXT waypoint after the closest one, so the car drives forward
        return closest + 1;
    }

    private void Update()
    {
        if (agent == null || waypointPath == null) return;

        if (isRecovering)
        {
            if (!agent.pathPending && agent.remainingDistance < 2f)
            {
                isRecovering = false;
                SetNextDestination();
            }
        }
        else if (!agent.pathPending && (agent.remainingDistance < 2f || HasPassedWaypoint()))
        {
            currentWaypointIndex++;
            SetNextDestination();
        }

        UpdateCurvatureSpeed();
        DetectCarsAhead();
        UpdateCollisionSpeed();
        ApplyCompositSpeed();

        CheckStuck();
    }

    private bool HasPassedWaypoint()
    {
        Transform current = waypointPath.GetWaypoint(currentWaypointIndex);
        Transform next = waypointPath.GetWaypoint(currentWaypointIndex + 1);

        Vector3 trackDir = next.position - current.position;
        trackDir.y = 0f;
        Vector3 tocar = transform.position - current.position;
        tocar.y = 0f;

        // Positive dot = car is beyond the waypoint plane in the track direction
        return Vector3.Dot(trackDir.normalized, tocar) > 0f;
    }

    // ── Look-Ahead Targeting ──────────────────────────────

    private void SetNextDestination()
    {
        Vector3 destination = ComputeLookAheadTarget();

        // Apply persistent racing line offset with small per-waypoint variation
        Transform target = waypointPath.GetWaypoint(currentWaypointIndex);
        Transform nextWp = waypointPath.GetWaypoint(currentWaypointIndex + 1);
        Vector3 forward = nextWp.position - target.position;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.01f)
        {
            Vector3 perpendicular = Vector3.Cross(forward.normalized, Vector3.up);
            // ±30% random variation on top of persistent offset for natural movement
            float variation = Random.Range(-0.3f, 0.3f) * lateralOffsetRange;
            float totalOffset = persistentLateralOffset + variation;
            Vector3 offsetPos = destination + perpendicular * totalOffset;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(offsetPos, out hit, lateralOffsetRange * 2f, NavMesh.AllAreas))
            {
                destination = hit.position;
            }
        }

        agent.SetDestination(destination);
    }

    /// <summary>
    /// Finds a target point along the waypoint path that is approximately
    /// lookAheadDistance meters ahead of the car. Falls back to the immediate
    /// next waypoint if the path is too short.
    /// </summary>
    private Vector3 ComputeLookAheadTarget()
    {
        Vector3 carPos = transform.position;
        Transform currentWp = waypointPath.GetWaypoint(currentWaypointIndex);
        float remainingLookAhead = lookAheadDistance;

        // Start from current waypoint
        Vector3 from = currentWp.position;
        float distToFirst = Vector3.Distance(carPos, from);

        // If car is closer than look-ahead to the current waypoint, walk forward
        if (distToFirst < remainingLookAhead)
        {
            remainingLookAhead -= distToFirst;
        }
        else
        {
            // Look-ahead point is between car and current waypoint — just use current
            return from;
        }

        // Walk along subsequent waypoints, consuming look-ahead distance
        int maxSteps = Mathf.Min(waypointPath.Count, 8);
        for (int step = 0; step < maxSteps; step++)
        {
            Transform next = waypointPath.GetWaypoint(currentWaypointIndex + 1 + step);
            float segLen = Vector3.Distance(from, next.position);

            if (segLen >= remainingLookAhead)
            {
                // Interpolate within this segment
                float t = remainingLookAhead / segLen;
                return Vector3.Lerp(from, next.position, t);
            }

            remainingLookAhead -= segLen;
            from = next.position;
        }

        // Look-ahead exceeds available path — use the farthest waypoint found
        return from;
    }

    // ── Curvature-Based Speed Control ─────────────────────

    private void UpdateCurvatureSpeed()
    {
        // Measure curvature by the angle between the two upcoming waypoint segments
        Transform wp0 = waypointPath.GetWaypoint(currentWaypointIndex);
        Transform wp1 = waypointPath.GetWaypoint(currentWaypointIndex + 1);
        Transform wp2 = waypointPath.GetWaypoint(currentWaypointIndex + 2);

        Vector3 seg1 = (wp1.position - wp0.position);
        Vector3 seg2 = (wp2.position - wp1.position);
        seg1.y = 0f;
        seg2.y = 0f;

        if (seg1.sqrMagnitude < 0.01f || seg2.sqrMagnitude < 0.01f)
        {
            curvatureSpeedMultiplier = 1f;
            return;
        }

        // Angle between segments: 0° = straight, 180° = hairpin
        float angle = Vector3.Angle(seg1, seg2);
        // Normalize to [0, 1] where 0 = straight, 1 = U-turn
        float curvature = angle / 180f;

        // Apply slowdown: speed *= (1 - factor * curvature²)
        // Squared curvature means gentle curves barely affect speed
        float target = 1f - curveSlowdownFactor * curvature * curvature;
        target = Mathf.Clamp(target, 0.3f, 1f);

        // Smooth transition so braking/acceleration feels natural
        curvatureSpeedMultiplier = Mathf.MoveTowards(
            curvatureSpeedMultiplier, target, Time.deltaTime * 2f);
    }

    // ── Forward Collision Detection ───────────────────────

    private void DetectCarsAhead()
    {
        if (forwardDetectionRange <= 0f)
        {
            forwardSpeedMultiplier = 1f;
            return;
        }

        float targetMultiplier = 1f;

        // SphereCast forward to detect cars ahead (wider than a raycast for reliability)
        float castRadius = agent.radius * 0.8f;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir = agent.velocity.sqrMagnitude > 0.1f
            ? agent.velocity.normalized
            : transform.forward;

        RaycastHit hit;
        if (Physics.SphereCast(origin, castRadius, dir, out hit, forwardDetectionRange))
        {
            var otherCar = hit.collider.GetComponentInParent<CarController>();
            if (otherCar != null && otherCar != this)
            {
                // Scale slowdown by proximity — closer = slower
                float proximity = 1f - (hit.distance / forwardDetectionRange);
                targetMultiplier = Mathf.Lerp(1f, forwardSlowdownFactor, proximity);
            }
        }

        // Smooth transition to avoid abrupt speed changes
        forwardSpeedMultiplier = Mathf.MoveTowards(
            forwardSpeedMultiplier, targetMultiplier, Time.deltaTime * 3f);
    }

    // ── Composite Speed ───────────────────────────────────

    private void ApplyCompositSpeed()
    {
        // Only apply composite when no event modifiers are active —
        // event modifiers manage agent.speed directly via coroutines
        if (activeModifierCount > 0) return;

        agent.speed = baseSpeed
            * collisionSpeedMultiplier
            * curvatureSpeedMultiplier
            * forwardSpeedMultiplier;
    }

    // ── Stuck Detection & Recovery ────────────────────────

    private void CheckStuck()
    {
        float distMoved = Vector3.Distance(transform.position, lastCheckedPosition);

        if (distMoved < stuckDistanceThreshold)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTimeThreshold)
            {
                consecutiveStuckCount++;
                stuckTimer = 0f;

                if (consecutiveStuckCount >= MaxRecoveryAttempts)
                {
                    // Multiple recovery attempts failed — warp to next waypoint
                    consecutiveStuckCount = 0;
                    isRecovering = false;
                    currentWaypointIndex++;
                    WarpToWaypoint(currentWaypointIndex);
                    SetNextDestination();
                }
                else
                {
                    AttemptRecovery();
                }

                lastCheckedPosition = transform.position;
            }
        }
        else
        {
            stuckTimer = 0f;
            consecutiveStuckCount = 0;
            lastCheckedPosition = transform.position;
        }
    }

    private void AttemptRecovery()
    {
        isRecovering = true;

        // Progressive recovery: alternate sides, increase offset with each attempt
        float sideSign = (consecutiveStuckCount % 2 == 0) ? 1f : -1f;
        float escalation = 1f + consecutiveStuckCount * 0.5f;
        Vector3 lateral = transform.right * sideSign;
        Vector3 recoveryTarget = transform.position
                                 + lateral * stuckRecoveryOffset * escalation
                                 + transform.forward * stuckRecoveryOffset;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(recoveryTarget, out hit, stuckRecoveryOffset * 2f * escalation, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            // No valid NavMesh nearby — skip waypoint and warp
            isRecovering = false;
            currentWaypointIndex++;
            WarpToWaypoint(currentWaypointIndex);
            SetNextDestination();
        }
    }

    private void WarpToWaypoint(int index)
    {
        Transform wp = waypointPath.GetWaypoint(index);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(wp.position, out hit, 10f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
    }

    // ── Trigger-Based Collision ───────────────────────────

    private void UpdateCollisionSpeed()
    {
        float target = collisionCount > 0 ? collisionSpeedFactor : 1f;
        // Slow down fast on impact, recover gradually
        float rate = collisionCount > 0 ? 0.15f : collisionRecoveryTime;
        collisionSpeedMultiplier = Mathf.MoveTowards(collisionSpeedMultiplier, target, Time.deltaTime / rate);
    }

    private void OnTriggerEnter(Collider other)
    {
        var otherCar = other.GetComponentInParent<CarController>();
        if (otherCar != null && otherCar != this)
            collisionCount++;
    }

    private void OnTriggerExit(Collider other)
    {
        var otherCar = other.GetComponentInParent<CarController>();
        if (otherCar != null && otherCar != this)
            collisionCount = Mathf.Max(0, collisionCount - 1);
    }

    // ── Event Speed Modifiers ─────────────────────────────

    public void ApplySpeedModifier(float delta, float duration)
    {
        StartCoroutine(SpeedModifierCoroutine(delta, duration));
    }

    private IEnumerator SpeedModifierCoroutine(float delta, float duration)
    {
        activeModifierCount++;
        agent.speed += delta;
        yield return new WaitForSeconds(duration);
        agent.speed -= delta;
        activeModifierCount--;

        // Safety: if all modifiers expired, let composite speed take over
        if (activeModifierCount <= 0)
        {
            activeModifierCount = 0;
        }
    }

    public float BaseSpeed => baseSpeed;
}
