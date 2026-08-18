using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

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
    private Material sharedTrailMaterial;

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
            else if (SpawnPoint != null)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-Config.SpawnOffsetX, Config.SpawnOffsetX),
                    0,
                    Random.Range(-Config.SpawnOffsetZ, Config.SpawnOffsetZ)
                );
                spawnPos = SpawnPoint.position + Config.SpawnSpreadMultiplier * randomOffset;
                spawnRot = SpawnPoint.rotation;
            }
            else
            {
                Debug.LogWarning($"[CarSpawner] No SpawnPoint assigned and no grid position for car {i} ('{data.TeamName}'). Using origin.");
                spawnPos = Vector3.zero;
                spawnRot = Quaternion.identity;
            }

            spawnPos = SnapToNavMesh(spawnPos, data.TeamName, agentTypeID);

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

            // Deactivate before adding NavMeshAgent so it doesn't try to initialize
            // with the default agent type (Humanoid) before we set the correct one
            car.SetActive(false);

            var agent = car.GetComponent<NavMeshAgent>();
            if (agent == null) agent = car.AddComponent<NavMeshAgent>();
            agent.agentTypeID = agentTypeID;
            agent.baseOffset = Config.BaseOffset;
            agent.radius = Config.AgentRadius;
            agent.obstacleAvoidanceType = Config.AvoidanceQuality;

            car.SetActive(true);
            agent.Warp(spawnPos);

            // Add trigger collider fitted to car mesh for inelastic collision detection
            var trigger = car.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            FitTriggerToRenderers(trigger, car.transform);

            AddTrailRenderer(car, data.ColorIndex);

            var controller = car.GetComponent<CarController>();
            if (controller == null) controller = car.AddComponent<CarController>();
            controller.Initialize(WaypointPath, Config.DefaultSpeed, Config.AngularSpeed, Config.Acceleration,
                Config.WaypointLateralOffset, Config.StuckTimeThreshold,
                Config.StuckDistanceThreshold, Config.StuckRecoveryOffset,
                Config.CollisionSpeedFactor, Config.CollisionRecoveryTime,
                Config.LookAheadDistance, Config.CurveSlowdownFactor,
                Config.ForwardDetectionRange, Config.ForwardSlowdownFactor);

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

    private Vector3 SnapToNavMesh(Vector3 position, string carName, int agentTypeID)
    {
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeID,
            areaMask = NavMesh.AllAreas
        };
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, NavMeshSampleRadius, filter))
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

            AddTrailRenderer(car, data.ColorIndex);

            spawnedCars.Add(car);
        }
        return spawnedCars;
    }

    private void AddTrailRenderer(GameObject car, int colorIndex)
    {
        var trail = car.AddComponent<TrailRenderer>();
        trail.time = Config.TrailDuration;
        trail.startWidth = Config.TrailStartWidth;
        trail.endWidth = Config.TrailEndWidth;
        var mat = GetSharedTrailMaterial();
        if (mat != null)
            trail.sharedMaterial = mat;
        else
            Debug.LogWarning("[CarSpawner] No suitable trail shader found; using default material");
        trail.startColor = GetTrailColor(colorIndex);
        trail.endColor = new Color(trail.startColor.r, trail.startColor.g, trail.startColor.b, 0f);
        trail.minVertexDistance = Config.TrailMinVertexDistance;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    private static Color GetTrailColor(int colorIndex)
    {
        switch (colorIndex)
        {
            case 0: return new Color(0.2f, 0.8f, 0.2f);  // green
            case 1: return new Color(0.3f, 0.3f, 0.3f);  // black/gray
            case 2: return new Color(0.9f, 0.2f, 0.2f);  // red
            case 3: return new Color(0.2f, 0.4f, 0.9f);  // blue
            case 4: return new Color(0.9f, 0.9f, 0.9f);  // white
            default: return Color.white;
        }
    }

    private Material GetSharedTrailMaterial()
    {
        if (sharedTrailMaterial != null) return sharedTrailMaterial;
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("UI/Default");
        if (shader != null)
            sharedTrailMaterial = new Material(shader);
        return sharedTrailMaterial;
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
