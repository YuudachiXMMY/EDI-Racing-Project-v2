using UnityEngine;

/// <summary>
/// Lightweight FPS counter using OnGUI (no Canvas overhead).
/// Only visible in Editor and Development builds.
/// </summary>
public class FpsCounter : MonoBehaviour
{
    private float deltaTime;

    private void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        float fps = 1.0f / deltaTime;
        GUI.Label(new Rect(10, 10, 200, 30), $"FPS: {fps:0.0}");
#endif
    }
}
