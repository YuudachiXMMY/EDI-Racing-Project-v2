using UnityEngine;
using System.Collections;

/// <summary>
/// Tracks active weather state. Phase 7 adds visual effects
/// (snow particles, night skybox) based on this state.
/// </summary>
public class WeatherEffect : MonoBehaviour
{
    public bool IsSnowActive { get; private set; }
    public bool IsNightActive { get; private set; }

    public void ActivateSnow(float duration)
    {
        IsSnowActive = true;
        Debug.Log("[Weather] Snow started");
        StartCoroutine(DeactivateAfter(duration, () =>
        {
            IsSnowActive = false;
            Debug.Log("[Weather] Snow ended");
        }));
    }

    public void ActivateNight(float duration)
    {
        IsNightActive = true;
        Debug.Log("[Weather] Night started");
        StartCoroutine(DeactivateAfter(duration, () =>
        {
            IsNightActive = false;
            Debug.Log("[Weather] Night ended");
        }));
    }

    private IEnumerator DeactivateAfter(float duration, System.Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        onComplete?.Invoke();
    }
}
