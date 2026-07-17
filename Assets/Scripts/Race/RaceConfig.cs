using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Configurable race parameters. All v1 defaults preserved.
/// Create via Assets > Create > EDI Racing > Race Config.
/// </summary>
[CreateAssetMenu(fileName = "RaceConfig", menuName = "EDI Racing/Race Config")]
public class RaceConfig : ScriptableObject
{
    [Header("Car Settings")]
    public float DefaultSpeed = 40f;
    public float AngularSpeed = 800f;
    public float Acceleration = 60f;
    public float BaseOffset = 0.42f;
    public float CarScale = 2.5f;
    public float RigidbodyMass = 100f;
    public float RigidbodyAngularDrag = 0.5f;

    [Header("Spawn Settings")]
    public float SpawnOffsetX = 7f;
    public float SpawnOffsetZ = 1.2f;
    public float SpawnSpreadMultiplier = 5f;

    [Header("AI Navigation")]
    [Tooltip("NavMeshAgent radius (meters). Should be roughly half the car's width at CarScale. Controls how far apart cars stay.")]
    public float AgentRadius = 2.5f;

    [Tooltip("Max random lateral offset from waypoints (meters). Cars pick different lines to avoid bunching.")]
    public float WaypointLateralOffset = 3f;

    [Tooltip("Seconds without movement before a car is considered stuck.")]
    public float StuckTimeThreshold = 2f;

    [Tooltip("Minimum distance (meters) a car must move within the threshold to not be stuck.")]
    public float StuckDistanceThreshold = 0.5f;

    [Tooltip("How far (meters) a stuck car offsets sideways to attempt recovery.")]
    public float StuckRecoveryOffset = 5f;

    [Header("AI Navigation - Advanced")]
    [Tooltip("Meters ahead along the waypoint path to aim for. Higher = smoother cornering but wider lines.")]
    public float LookAheadDistance = 15f;

    [Tooltip("How much to slow down in curves (0=no slowdown, 1=full stop at hairpin). Speed is multiplied by (1 - factor * curvature).")]
    public float CurveSlowdownFactor = 0.5f;

    [Tooltip("Forward raycast range (meters) for detecting cars ahead. Triggers anticipatory braking.")]
    public float ForwardDetectionRange = 12f;

    [Tooltip("Speed multiplier when a car is detected directly ahead (0-1).")]
    public float ForwardSlowdownFactor = 0.6f;

    [Header("Collision")]
    [Tooltip("Speed multiplier while colliding with another car (0-1). Lower = more slowdown.")]
    public float CollisionSpeedFactor = 0.4f;

    [Tooltip("Seconds to recover full speed after separating from a collision.")]
    public float CollisionRecoveryTime = 1f;

    [Header("Trail Settings")]
    [Tooltip("How long the trail persists behind the car (seconds).")]
    public float TrailDuration = 0.5f;

    [Tooltip("Trail width at the car (meters).")]
    public float TrailStartWidth = 0.8f;

    [Tooltip("Trail width at the tail end (meters).")]
    public float TrailEndWidth = 0.1f;

    [Header("Race Settings")]
    public int TotalLaps = 3;

    [Header("Performance")]
    [Tooltip("Minimum distance between trail vertices. Higher = fewer vertices, less GPU cost.")]
    public float TrailMinVertexDistance = 1.5f;

    [Tooltip("Max distance from camera before car labels are hidden.")]
    public float LabelVisibleDistance = 80f;

    [Tooltip("NavMeshAgent obstacle avoidance quality. Lower = faster with more cars.")]
    public ObstacleAvoidanceType AvoidanceQuality = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
}
