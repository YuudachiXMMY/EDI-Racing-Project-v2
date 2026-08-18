using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pre-configured list of event rules for a race session.
/// Create via Assets > Create > EDI Racing > Event Schedule.
/// Professor sets up rules here before starting the race.
/// </summary>
[CreateAssetMenu(fileName = "EventSchedule", menuName = "EDI Racing/Event Schedule")]
public class EventSchedule : ScriptableObject
{
    [Tooltip("List of event rules configured for this race session")]
    public EventRule[] Events = DefaultEventRules.BaseRuntime();

    /// <summary>
    /// Reset all runtime state (HasBeenTriggered flags).
    /// Call at race start.
    /// </summary>
    public void ResetRuntimeState()
    {
        for (int i = 0; i < Events.Length; i++)
        {
            Events[i].HasBeenTriggered = false;
        }
    }
}
