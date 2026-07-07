using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// NavMeshAgent-based autonomous car movement.
/// Follows waypoints with random lateral offset and stuck recovery.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CarIdentity))]
public class CarController : MonoBehaviour
{
    private NavMeshAgent agent;
    private WaypointPath waypointPath;
    private int currentWaypointIndex;
    private float baseSpeed;

    // Lateral offset
    private float lateralOffsetRange;

    // Stuck detection
    private float stuckTimeThreshold;
    private float stuckDistanceThreshold;
    private float stuckRecoveryOffset;
    private Vector3 lastCheckedPosition;
    private float stuckTimer;
    private int consecutiveStuckCount;
    private bool isRecovering;

    // Inelastic collision
    private float collisionSpeedFactor;
    private float collisionRecoveryTime;
    private int collisionCount;
    private float speedMultiplier = 1f;

    // Event speed modifier stacking
    private int activeModifierCount;

    private const int MaxRecoveryAttempts = 3;

    public void Initialize(WaypointPath path, float speed, float angularSpeed, float acceleration,
                           float lateralOffset = 3f, float stuckTime = 2f,
                           float stuckDist = 0.5f, float recoveryOffset = 5f,
                           float colSpeedFactor = 0.4f, float colRecoveryTime = 1f)
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

        agent.speed = speed;
        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
        agent.autoBraking = false;

        // Randomize avoidance priority so cars yield differently instead of deadlocking
        agent.avoidancePriority = Random.Range(20, 80);

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

        CheckStuck();
        UpdateCollisionSpeed();
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
                    // Multiple recovery attempts failed — skip to next waypoint
                    consecutiveStuckCount = 0;
                    isRecovering = false;
                    currentWaypointIndex++;
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

        // Pick a random lateral direction to escape
        Vector3 lateral = transform.right * (Random.value > 0.5f ? 1f : -1f);
        Vector3 recoveryTarget = transform.position
                                 + lateral * stuckRecoveryOffset
                                 + transform.forward * stuckRecoveryOffset;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(recoveryTarget, out hit, stuckRecoveryOffset * 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            // No valid NavMesh nearby — skip waypoint instead
            isRecovering = false;
            currentWaypointIndex++;
            SetNextDestination();
        }
    }

    private void SetNextDestination()
    {
        Transform target = waypointPath.GetWaypoint(currentWaypointIndex);
        Vector3 destination = target.position;

        if (lateralOffsetRange > 0f)
        {
            // Compute perpendicular direction from track heading at this waypoint
            Transform nextWp = waypointPath.GetWaypoint(currentWaypointIndex + 1);
            Vector3 forward = (nextWp.position - target.position);
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.01f)
            {
                Vector3 perpendicular = Vector3.Cross(forward.normalized, Vector3.up);
                float offset = Random.Range(-lateralOffsetRange, lateralOffsetRange);
                Vector3 offsetPos = destination + perpendicular * offset;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(offsetPos, out hit, lateralOffsetRange * 2f, NavMesh.AllAreas))
                {
                    destination = hit.position;
                }
            }
        }

        agent.SetDestination(destination);
    }

    /// <summary>
    /// Temporarily modify speed. Used by Event System (Phase 2).
    /// </summary>
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

        // Safety: if all modifiers expired, snap back to base accounting for collision multiplier
        if (activeModifierCount <= 0)
        {
            activeModifierCount = 0;
            agent.speed = baseSpeed * speedMultiplier;
        }
    }

    public float BaseSpeed => baseSpeed;

    private void UpdateCollisionSpeed()
    {
        float target = collisionCount > 0 ? collisionSpeedFactor : 1f;
        // Slow down fast on impact, recover gradually
        float rate = collisionCount > 0 ? 0.15f : collisionRecoveryTime;
        speedMultiplier = Mathf.MoveTowards(speedMultiplier, target, Time.deltaTime / rate);
        agent.speed = baseSpeed * speedMultiplier;
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
}
