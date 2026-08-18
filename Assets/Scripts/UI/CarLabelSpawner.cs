using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns world-space CarLabel instances for each car after race starts.
/// Listens to RaceManager.OnStateChanged to know when cars exist.
/// </summary>
public class CarLabelSpawner : MonoBehaviour
{
    [Header("References")]
    public RaceManager RaceManager;

    [Header("Label Settings")]
    [Tooltip("Font size for car name labels")]
    public int FontSize = 24;

    [Tooltip("Height offset above car")]
    public float LabelHeight = 4f;

    [Tooltip("Max distance from camera before labels are hidden. Uses RaceConfig value if RaceManager has one.")]
    public float MaxVisibleDistance = 80f;

    private readonly List<GameObject> spawnedLabels = new List<GameObject>();

    private void OnEnable()
    {
        if (RaceManager != null)
            RaceManager.OnStateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        if (RaceManager != null)
            RaceManager.OnStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Racing)
            SpawnLabels();
        else if (state == GameState.Setup)
            ClearLabels();
    }

    private void SpawnLabels()
    {
        ClearLabels();

        var cars = RaceManager.SpawnedCars;
        if (cars == null) return;

        foreach (var car in cars)
        {
            if (car == null) continue;
            var identity = car.GetComponent<CarIdentity>();
            if (identity == null) continue;

            // Create world-space Canvas
            GameObject labelObj = new GameObject($"Label_{identity.TeamName}");
            Canvas canvas = labelObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            RectTransform canvasRect = labelObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(4f, 0.5f);
            canvasRect.localScale = Vector3.one * 0.05f;

            // Add Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(labelObj.transform, false);
            Text text = textObj.AddComponent<Text>();
            text.fontSize = FontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Highlight own car with distinct label
            if (identity.IsOwnCar)
            {
                text.text = $">> {identity.TeamName} <<";
                text.color = LeaderboardFormatter.Gold;
                text.fontStyle = FontStyle.Bold;
            }
            else
            {
                text.text = identity.TeamName;
                text.color = Color.white;
            }

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Add CarLabel behavior
            CarLabel label = labelObj.AddComponent<CarLabel>();
            label.HeightOffset = LabelHeight;
            float visDist = (RaceManager != null && RaceManager.Config != null)
                ? RaceManager.Config.LabelVisibleDistance
                : MaxVisibleDistance;
            label.MaxVisibleDistance = visDist;
            label.Initialize(car.transform);

            spawnedLabels.Add(labelObj);
        }

        Debug.Log($"[CarLabelSpawner] Spawned {spawnedLabels.Count} car labels");
    }

    private void ClearLabels()
    {
        foreach (var label in spawnedLabels)
        {
            if (label != null) Destroy(label);
        }
        spawnedLabels.Clear();
    }
}
