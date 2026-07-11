using UnityEngine;
using System.Collections;

/// <summary>
/// Manages weather visual effects: snow particles and night-mode lighting.
/// Snow creates a camera-following particle blizzard.
/// Night dims the directional light and ambient color with smooth transitions.
/// </summary>
public class WeatherEffect : MonoBehaviour
{
    public bool IsSnowActive { get; private set; }
    public bool IsNightActive { get; private set; }

    // Snow
    private ParticleSystem snowParticles;
    private Transform snowTransform;

    // Night
    private Light directionalLight;
    private Color originalAmbientColor;
    private float originalLightIntensity;
    private bool hasStoredOriginals;
    private Coroutine nightTransitionCoroutine;

    [Header("Night Settings")]
    [Tooltip("Target directional light intensity during night")]
    public float NightLightIntensity = 0.15f;

    [Tooltip("Ambient color during night")]
    public Color NightAmbientColor = new Color(0.05f, 0.05f, 0.15f);

    [Tooltip("Transition time for night on/off (seconds)")]
    public float NightTransitionTime = 0.5f;

    private void Awake()
    {
        CreateSnowSystem();
    }

    private void Start()
    {
        directionalLight = FindDirectionalLight();
        if (directionalLight != null)
            originalLightIntensity = directionalLight.intensity;
        originalAmbientColor = RenderSettings.ambientLight;
        hasStoredOriginals = true;
    }

    // ── Snow ──────────────────────────────────────────────

    private void CreateSnowSystem()
    {
        GameObject snowObj = new GameObject("SnowParticles");
        snowObj.transform.SetParent(transform);
        snowObj.transform.localPosition = Vector3.up * 50f;
        snowTransform = snowObj.transform;

        snowParticles = snowObj.AddComponent<ParticleSystem>();

        var main = snowParticles.main;
        main.loop = true;
        main.startLifetime = 5f;
        main.startSpeed = 8f;
        main.startSize = 0.3f;
        main.maxParticles = 2000;
        main.startColor = new Color(0.95f, 0.95f, 1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;

        var emission = snowParticles.emission;
        emission.rateOverTime = 500f;

        var shape = snowParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(100f, 1f, 100f);

        var renderer = snowObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void LateUpdate()
    {
        if (snowTransform != null && IsSnowActive && Camera.main != null)
            snowTransform.position = Camera.main.transform.position + Vector3.up * 50f;
    }

    public void ActivateSnow(float duration)
    {
        IsSnowActive = true;
        if (snowParticles != null) snowParticles.Play();
        Debug.Log("[Weather] Snow started");
        StartCoroutine(DeactivateAfter(duration, () =>
        {
            IsSnowActive = false;
            if (snowParticles != null)
                snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Debug.Log("[Weather] Snow ended");
        }));
    }

    // ── Night ─────────────────────────────────────────────

    public void ActivateNight(float duration)
    {
        IsNightActive = true;
        Debug.Log("[Weather] Night started");
        if (nightTransitionCoroutine != null) StopCoroutine(nightTransitionCoroutine);
        nightTransitionCoroutine = StartCoroutine(NightTransition(true));

        StartCoroutine(DeactivateAfter(duration, () =>
        {
            IsNightActive = false;
            if (nightTransitionCoroutine != null) StopCoroutine(nightTransitionCoroutine);
            nightTransitionCoroutine = StartCoroutine(NightTransition(false));
            Debug.Log("[Weather] Night ended");
        }));
    }

    private IEnumerator NightTransition(bool toNight)
    {
        if (!hasStoredOriginals) yield break;

        float targetIntensity = toNight ? NightLightIntensity : originalLightIntensity;
        Color targetAmbient = toNight ? NightAmbientColor : originalAmbientColor;

        float startIntensity = directionalLight != null ? directionalLight.intensity : originalLightIntensity;
        Color startAmbient = RenderSettings.ambientLight;

        float elapsed = 0f;
        while (elapsed < NightTransitionTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / NightTransitionTime);

            if (directionalLight != null)
                directionalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            RenderSettings.ambientLight = Color.Lerp(startAmbient, targetAmbient, t);

            yield return null;
        }

        if (directionalLight != null)
            directionalLight.intensity = targetIntensity;
        RenderSettings.ambientLight = targetAmbient;
    }

    private Light FindDirectionalLight()
    {
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional) return light;
        }
        return null;
    }

    // ── Shared ────────────────────────────────────────────

    private IEnumerator DeactivateAfter(float duration, System.Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        onComplete?.Invoke();
    }

    /// <summary>
    /// Force-restores all weather to default state. Called on race reset.
    /// </summary>
    public void ResetAll()
    {
        StopAllCoroutines();
        nightTransitionCoroutine = null;

        IsSnowActive = false;
        IsNightActive = false;

        if (snowParticles != null)
            snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (hasStoredOriginals)
        {
            if (directionalLight != null)
                directionalLight.intensity = originalLightIntensity;
            RenderSettings.ambientLight = originalAmbientColor;
        }
    }
}
