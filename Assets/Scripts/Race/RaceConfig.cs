using UnityEngine;

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

    [Header("Collision")]
    [Tooltip("Speed multiplier while colliding with another car (0-1). Lower = more slowdown.")]
    public float CollisionSpeedFactor = 0.4f;

    [Tooltip("Seconds to recover full speed after separating from a collision.")]
    public float CollisionRecoveryTime = 1f;

    [Header("Race Settings")]
    public int TotalLaps = 3;
}
