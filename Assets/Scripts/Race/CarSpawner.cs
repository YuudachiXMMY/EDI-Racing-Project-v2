using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Instantiates car prefabs from CarData at the spawn point.
/// Adds runtime components: Rigidbody, NavMeshAgent, CarController.
/// All spawn positions are snapped to the NavMesh to prevent collisions.
/// </summary>
public class CarSpawner : MonoBehaviour
{
    [Tooltip("Car prefabs indexed by color: 0=green, 1=black, 2=red, 3=blue, 4=white")]
    public GameObject[] CarPrefabs;
    public Transform SpawnPoint;
    public WaypointPath WaypointPath;
    public RaceConfig Config;

    [Tooltip("Specific grid positions for each car. Cars spawn at these transforms in order. Extra cars beyond the grid count fall back to random offsets around SpawnPoint.")]
    public Transform[] StartingGridPositions;

    [Tooltip("NavMesh agent type name (must match a type defined in Navigation settings)")]
    public string AgentTypeName = "Car";

    [Tooltip("Max distance to search for a valid NavMesh position from the intended spawn point")]
    public float NavMeshSampleRadius = 20f;

    private int cachedAgentTypeID = -1;

    public List<GameObject> SpawnCars(List<CarData> carDataList)
    {
        int agentTypeID = FindAgentTypeID(AgentTypeName);

        var spawnedCars = new List<GameObject>();
        for (int i = 0; i < carDataList.Count; i++)
        {
            var data = carDataList[i];
            int prefabIndex = Mathf.Clamp(data.ColorIndex, 0, CarPrefabs.Length - 1);
            GameObject prefab = CarPrefabs[prefabIndex];

            Vector3 spawnPos;
            Quaternion spawnRot;

            if (StartingGridPositions != null && i < StartingGridPositions.Length && StartingGridPositions[i] != null)
            {
                spawnPos = StartingGridPositions[i].position;
                spawnRot = StartingGridPositions[i].rotation;
            }
            else
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-Config.SpawnOffsetX, Config.SpawnOffsetX),
                    0,
                    Random.Range(-Config.SpawnOffsetZ, Config.SpawnOffsetZ)
                );
                spawnPos = SpawnPoint.position + Config.SpawnSpreadMultiplier * randomOffset;
                spawnRot = SpawnPoint.rotation;
            }

            spawnPos = SnapToNavMesh(spawnPos, data.TeamName);

            GameObject car = Instantiate(prefab, spawnPos, spawnRot);
            car.transform.localScale *= Config.CarScale;
            car.name = data.TeamName;

            var identity = car.GetComponent<CarIdentity>();
            if (identity == null) identity = car.AddComponent<CarIdentity>();
            identity.Initialize(data);

            var rb = car.GetComponent<Rigidbody>();
            if (rb == null) rb = car.AddComponent<Rigidbody>();
            rb.mass = Config.RigidbodyMass;
            rb.angularDamping = Config.RigidbodyAngularDrag;
            rb.isKinematic = true; // NavMeshAgent controls movement; non-kinematic Rigidbody conflicts

            var agent = car.GetComponent<NavMeshAgent>();
            if (agent == null) agent = car.AddComponent<NavMeshAgent>();
            agent.agentTypeID = agentTypeID;
            agent.baseOffset = Config.BaseOffset;
            agent.radius = Config.AgentRadius;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            // Add trigger collider fitted to car mesh for inelastic collision detection
            var trigger = car.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            FitTriggerToRenderers(trigger, car.transform);

            var controller = car.GetComponent<CarController>();
            if (controller == null) controller = car.AddComponent<CarController>();
            controller.Initialize(WaypointPath, Config.DefaultSpeed, Config.AngularSpeed, Config.Acceleration,
                Config.WaypointLateralOffset, Config.StuckTimeThreshold,
                Config.StuckDistanceThreshold, Config.StuckRecoveryOffset,
                Config.CollisionSpeedFactor, Config.CollisionRecoveryTime);

            spawnedCars.Add(car);
        }
        return spawnedCars;
    }

    private void FitTriggerToRenderers(BoxCollider collider, Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        collider.center = root.InverseTransformPoint(bounds.center);
        Vector3 scale = root.lossyScale;
        collider.size = new Vector3(
            bounds.size.x / scale.x * 0.85f,
            bounds.size.y / scale.y * 0.85f,
            bounds.size.z / scale.z * 0.85f
        );
    }

    private Vector3 SnapToNavMesh(Vector3 position, string carName)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, NavMeshSampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        Debug.LogWarning($"[CarSpawner] No NavMesh found within {NavMeshSampleRadius}m of spawn for '{carName}'. Using raw position.");
        return position;
    }

    /// <summary>
    /// Spawns visual-only cars for student clients. No NavMeshAgent, CarController,
    /// Rigidbody, or BoxCollider — positions are set by NetworkSync.
    /// </summary>
    public List<GameObject> SpawnVisualCars(List<CarData> carDataList)
    {
        var spawnedCars = new List<GameObject>();
        for (int i = 0; i < carDataList.Count; i++)
        {
            var data = carDataList[i];
            int prefabIndex = Mathf.Clamp(data.ColorIndex, 0, CarPrefabs.Length - 1);
            GameObject prefab = CarPrefabs[prefabIndex];

            Vector3 spawnPos = SpawnPoint != null ? SpawnPoint.position : Vector3.zero;
            Quaternion spawnRot = SpawnPoint != null ? SpawnPoint.rotation : Quaternion.identity;

            GameObject car = Instantiate(prefab, spawnPos, spawnRot);
            car.transform.localScale *= Config.CarScale;
            car.name = data.TeamName;

            var identity = car.GetComponent<CarIdentity>();
            if (identity == null) identity = car.AddComponent<CarIdentity>();
            identity.Initialize(data);

            spawnedCars.Add(car);
        }
        return spawnedCars;
    }

    private int FindAgentTypeID(string typeName)
    {
        if (cachedAgentTypeID != -1) return cachedAgentTypeID;

        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(settings.agentTypeID) == typeName)
            {
                cachedAgentTypeID = settings.agentTypeID;
                return cachedAgentTypeID;
            }
        }
        Debug.LogWarning($"[CarSpawner] NavMesh agent type '{typeName}' not found. Falling back to default (Humanoid). Create it in Window > AI > Navigation.");
        cachedAgentTypeID = 0;
        return 0;
    }
}
