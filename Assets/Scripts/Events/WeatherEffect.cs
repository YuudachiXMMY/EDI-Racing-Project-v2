using UnityEngine;
using System.Collections;

/// <summary>
/// Manages weather visual effects using Customizable Skybox materials.
/// Smoothly transitions between day/night/snow/sunset skyboxes and
/// adjusts directional light + ambient color to match.
/// Supports automatic day/sunset cycling driven by race time.
/// Snow also spawns a camera-following particle blizzard.
/// </summary>
public class WeatherEffect : MonoBehaviour
{
    public bool IsSnowActive { get; private set; }
    public bool IsNightActive { get; private set; }
    public bool IsSunsetActive { get; private set; }

    [Header("Skybox Materials (Customizable Skybox)")]
    [Tooltip("Default daytime skybox material")]
    public Material DaySkybox;

    [Tooltip("Night skybox material")]
    public Material NightSkybox;

    [Tooltip("Snow/overcast skybox material (use a pale Day variant)")]
    public Material SnowSkybox;

    [Tooltip("Sunset skybox material")]
    public Material SunsetSkybox;

    [Header("Day/Sunset Cycle")]
    [Tooltip("Automatically cycle between day and sunset during the race")]
    public bool DayCycleEnabled = true;

    [Tooltip("Total seconds for one full Day → Sunset → Day cycle")]
    public float DayCycleDuration = 90f;

    [Tooltip("Fraction of the cycle spent in daytime (0-1). The rest is sunset.")]
    [Range(0.2f, 0.8f)]
    public float DayFraction = 0.6f;

    [Header("Transition")]
    [Tooltip("Seconds to blend between skybox states")]
    public float TransitionTime = 1.5f;

    [Header("Day Lighting")]
    [Tooltip("Ambient color during daytime")]
    public Color DayAmbientColor = new Color(0.53f, 0.53f, 0.53f);

    [Header("Night Lighting")]
    [Tooltip("Directional light intensity during night")]
    public float NightLightIntensity = 0.15f;

    [Tooltip("Ambient color during night")]
    public Color NightAmbientColor = new Color(0.05f, 0.05f, 0.15f);

    [Header("Sunset Lighting")]
    [Tooltip("Directional light intensity during sunset")]
    public float SunsetLightIntensity = 0.6f;

    [Tooltip("Ambient color during sunset")]
    public Color SunsetAmbientColor = new Color(0.45f, 0.25f, 0.15f);

    [Tooltip("Directional light color during sunset")]
    public Color SunsetLightColor = new Color(1f, 0.55f, 0.2f);

    [Header("Snow Lighting")]
    [Tooltip("Ambient color during snow (pale overcast)")]
    public Color SnowAmbientColor = new Color(0.6f, 0.65f, 0.7f);

    // Snow particles
    private ParticleSystem snowParticles;
    private Transform snowTransform;

    // Light originals
    private Light directionalLight;
    private Color originalAmbientColor;
    private Color originalLightColor;
    private float originalLightIntensity;
    private Material originalSkybox;
    private bool hasStoredOriginals;

    // Transition state
    private Coroutine activeTransition;

    // Day cycle state
    private bool cycleRunning;
    private float cycleStartTime;
    private bool cycleInSunset; // tracks current phase to avoid re-triggering
    private bool eventOverrideActive; // true when snow/night/manual-sunset overrides cycle

    private void Awake()
    {
        CreateSnowSystem();
    }

    private void Start()
    {
        directionalLight = FindDirectionalLight();
        if (directionalLight != null)
        {
            originalLightIntensity = directionalLight.intensity;
            originalLightColor = directionalLight.color;
        }
        originalAmbientColor = RenderSettings.ambientLight;
        originalSkybox = RenderSettings.skybox;
        hasStoredOriginals = true;

        if (DaySkybox != null && originalSkybox != DaySkybox)
        {
            RenderSettings.skybox = DaySkybox;
            originalSkybox = DaySkybox;
        }
    }

    // ── Day/Sunset Cycle ─────────────────────────────────

    /// <summary>
    /// Start the automatic day/sunset cycle. Called when race begins.
    /// </summary>
    public void StartCycle()
    {
        if (!DayCycleEnabled) return;
        cycleRunning = true;
        cycleStartTime = Time.time;
        cycleInSunset = false;
        eventOverrideActive = false;
        Debug.Log($"[Weather] Day/sunset cycle started ({DayCycleDuration}s period, {DayFraction * 100:F0}% day)");
    }

    /// <summary>
    /// Stop the automatic cycle. Called on race reset.
    /// </summary>
    public void StopCycle()
    {
        cycleRunning = false;
    }

    private void Update()
    {
        if (!cycleRunning || !DayCycleEnabled || eventOverrideActive) return;
        if (DayCycleDuration <= 0f) return;

        float elapsed = Time.time - cycleStartTime;
        float phase = (elapsed % DayCycleDuration) / DayCycleDuration;

        bool shouldBeSunset = phase >= DayFraction;

        if (shouldBeSunset && !cycleInSunset)
        {
            cycleInSunset = true;
            TransitionTo(SunsetSkybox, SunsetLightIntensity, SunsetLightColor, SunsetAmbientColor);
            Debug.Log("[Weather] Cycle → Sunset");
        }
        else if (!shouldBeSunset && cycleInSunset)
        {
            cycleInSunset = false;
            TransitionTo(DaySkybox, originalLightIntensity, originalLightColor, DayAmbientColor);
            Debug.Log("[Weather] Cycle → Day");
        }
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
        // Vary fall speed so flakes don't move as a rigid sheet.
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        // Vary flake size for depth; small flakes read as distant snow.
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 2000;
        main.startColor = new Color(0.95f, 0.95f, 1f, 0.85f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;

        var emission = snowParticles.emission;
        emission.rateOverTime = 500f;

        var shape = snowParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(100f, 1f, 100f);

        // Gentle turbulence so flakes drift and swirl instead of falling straight.
        var noise = snowParticles.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.3f;

        var renderer = snowObj.GetComponent<ParticleSystemRenderer>();
        var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Particles/Standard Unlit")
                             ?? Shader.Find("UI/Default");
        if (particleShader != null)
        {
            var mat = new Material(particleShader);
            var flakeTex = CreateSnowflakeTexture();

            // Assign the soft-dot texture under every base-map name the fallback
            // shaders use, so flakes render as soft rounded points, not squares.
            mat.mainTexture = flakeTex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", flakeTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", flakeTex);

            // Switch to alpha blending so the soft edges are translucent
            // (URP Particles/Unlit defaults to Opaque, which ignores flake alpha).
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // 0 = Alpha
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            renderer.material = mat;
        }
        else
        {
            Debug.LogWarning("[WeatherEffect] No suitable particle shader found; using default material");
        }
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;

        snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// Builds a soft radial-gradient "snowflake" sprite at runtime so the
    /// particle billboards read as round, feathered flakes instead of hard
    /// white squares. Avoids shipping a texture asset for a single effect.
    /// </summary>
    private static Texture2D CreateSnowflakeTexture()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "SnowflakeSoftDot"
        };

        float center = (size - 1) * 0.5f;
        float radius = center;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                // Smooth falloff: fully opaque at center, transparent at the rim.
                float alpha = Mathf.Clamp01(1f - dist);
                alpha *= alpha; // steeper edge → softer, rounder flake
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void LateUpdate()
    {
        if (snowTransform != null && IsSnowActive && Camera.main != null)
            snowTransform.position = Camera.main.transform.position + Vector3.up * 50f;
    }

    public void ActivateSnow(float duration)
    {
        IsSnowActive = true;
        eventOverrideActive = true;
        if (snowParticles != null) snowParticles.Play();
        TransitionTo(SnowSkybox, originalLightIntensity * 0.7f, originalLightColor, SnowAmbientColor);
        Debug.Log("[Weather] Snow started");

        StartCoroutine(DeactivateAfter(duration, () =>
        {
            IsSnowActive = false;
            if (snowParticles != null)
                snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            EndEventOverride();
            Debug.Log("[Weather] Snow ended");
        }));
    }

    // ── Night ─────────────────────────────────────────────

    public void ActivateNight(float duration)
    {
        IsNightActive = true;
        eventOverrideActive = true;
        TransitionTo(NightSkybox, NightLightIntensity, originalLightColor, NightAmbientColor);
        Debug.Log("[Weather] Night started");

        StartCoroutine(DeactivateAfter(duration, () =>
        {
            IsNightActive = false;
            EndEventOverride();
            Debug.Log("[Weather] Night ended");
        }));
    }

    // ── Sunset (event-triggered, not cycle) ───────────────

    public void ActivateSunset(float duration)
    {
        IsSunsetActive = true;
        eventOverrideActive = true;
        TransitionTo(SunsetSkybox, SunsetLightIntensity, SunsetLightColor, SunsetAmbientColor);
        Debug.Log("[Weather] Sunset started (event)");

        StartCoroutine(DeactivateAfter(duration, () =>
        {
            IsSunsetActive = false;
            EndEventOverride();
            Debug.Log("[Weather] Sunset ended (event)");
        }));
    }

    // ── Override Management ───────────────────────────────

    /// <summary>
    /// Called when an event-triggered weather ends.
    /// Resumes the day/sunset cycle at the correct phase.
    /// </summary>
    private void EndEventOverride()
    {
        if (IsSnowActive || IsNightActive || IsSunsetActive) return;
        eventOverrideActive = false;

        // Immediately snap to the correct cycle phase
        if (cycleRunning && DayCycleEnabled)
        {
            float elapsed = Time.time - cycleStartTime;
            float phase = (elapsed % DayCycleDuration) / DayCycleDuration;
            bool shouldBeSunset = phase >= DayFraction;

            cycleInSunset = shouldBeSunset;
            if (shouldBeSunset)
                TransitionTo(SunsetSkybox, SunsetLightIntensity, SunsetLightColor, SunsetAmbientColor);
            else
                TransitionTo(DaySkybox, originalLightIntensity, originalLightColor, DayAmbientColor);
        }
        else
        {
            TransitionTo(originalSkybox, originalLightIntensity, originalLightColor, originalAmbientColor);
        }
    }

    // ── Skybox + Lighting Transition ──────────────────────

    private void TransitionTo(Material targetSkybox, float targetIntensity,
                              Color targetLightColor, Color targetAmbient)
    {
        if (activeTransition != null)
            StopCoroutine(activeTransition);
        activeTransition = StartCoroutine(SkyTransition(targetSkybox, targetIntensity,
                                                        targetLightColor, targetAmbient));
    }

    private IEnumerator SkyTransition(Material targetSkybox, float targetIntensity,
                                      Color targetLightColor, Color targetAmbient)
    {
        if (!hasStoredOriginals) yield break;

        if (targetSkybox != null)
            RenderSettings.skybox = targetSkybox;

        float startIntensity = directionalLight != null ? directionalLight.intensity : originalLightIntensity;
        Color startLightColor = directionalLight != null ? directionalLight.color : originalLightColor;
        Color startAmbient = RenderSettings.ambientLight;

        float elapsed = 0f;
        while (elapsed < TransitionTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / TransitionTime));

            if (directionalLight != null)
            {
                directionalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
                directionalLight.color = Color.Lerp(startLightColor, targetLightColor, t);
            }
            RenderSettings.ambientLight = Color.Lerp(startAmbient, targetAmbient, t);

            yield return null;
        }

        if (directionalLight != null)
        {
            directionalLight.intensity = targetIntensity;
            directionalLight.color = targetLightColor;
        }
        RenderSettings.ambientLight = targetAmbient;
        activeTransition = null;
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
        activeTransition = null;
        cycleRunning = false;
        eventOverrideActive = false;
        cycleInSunset = false;

        IsSnowActive = false;
        IsNightActive = false;
        IsSunsetActive = false;

        if (snowParticles != null)
            snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (hasStoredOriginals)
        {
            if (directionalLight != null)
            {
                directionalLight.intensity = originalLightIntensity;
                directionalLight.color = originalLightColor;
            }
            RenderSettings.ambientLight = originalAmbientColor;
            if (originalSkybox != null)
                RenderSettings.skybox = originalSkybox;
        }
    }
}
