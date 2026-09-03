using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages race events: listens for keyboard triggers,
/// evaluates rules against car attributes, applies speed modifiers.
/// </summary>
public class EventManager : MonoBehaviour
{
    [Header("Configuration")]
    public EventSchedule Schedule;

    private readonly List<CarIdentity> registeredCars = new List<CarIdentity>();
    private bool isActive;

    public event Action<EventRule, int> OnEventTriggered;

    public void RegisterCar(CarIdentity car)
    {
        registeredCars.Add(car);
    }

    public void RegisterCars(List<GameObject> cars)
    {
        foreach (var car in cars)
        {
            var identity = car.GetComponent<CarIdentity>();
            if (identity != null)
                registeredCars.Add(identity);
        }
    }

    public void Activate()
    {
        isActive = true;
        Schedule.ResetRuntimeState();
        Debug.Log($"[EventManager] Activated with {Schedule.Events.Length} event rules configured");
    }

    public void Deactivate()
    {
        isActive = false;
    }

    // Update() and its digit-key polling were removed: the professor's live control is now the
    // parameterized event menu (EventPanel), which builds rules at trigger time and calls
    // TriggerRule. Snow/Night weather keys (9/0) are handled there too. TriggerEvent(index) is
    // kept for programmatic/schedule access and the EditMode tests.

    public void TriggerEvent(int eventIndex)
    {
        if (eventIndex < 0 || eventIndex >= Schedule.Events.Length) return;

        var rule = Schedule.Events[eventIndex];

        if (rule.HasBeenTriggered && !rule.AllowRepeat)
        {
            Debug.Log($"[EventManager] '{rule.DisplayName}' already triggered (repeat disabled)");
            return;
        }

        Schedule.Events[eventIndex].HasBeenTriggered = true;
        ApplyRule(rule);
    }

    /// <summary>
    /// Apply an ad-hoc rule built at trigger time (the parameterized event menu). Runs the same
    /// affected-cars loop as TriggerEvent and fires OnEventTriggered — so weather VFX
    /// (RaceManager) and the student network broadcast (NetworkSync), which both hang off that
    /// event, keep working. Has no schedule slot, so HasBeenTriggered/AllowRepeat do not apply.
    /// Gated on IsActive (mirrors the old Update() guard).
    /// </summary>
    /// <returns>Number of cars affected.</returns>
    public int TriggerRule(EventRule rule)
    {
        if (!isActive) return 0;
        return ApplyRule(rule);
    }

    // Shared core: evaluate the rule against every registered car, apply the speed modifier to
    // matches, log, and fire OnEventTriggered. Returns the affected count.
    private int ApplyRule(EventRule rule)
    {
        int affectedCount = 0;
        foreach (var car in registeredCars)
        {
            if (RuleEngine.IsAffected(rule, car))
            {
                var controller = car.GetComponent<CarController>();
                if (controller != null)
                {
                    controller.ApplySpeedModifier(rule.SpeedDelta, rule.Duration);
                    affectedCount++;
                }
            }
        }

        Debug.Log($"[EventManager] '{rule.DisplayName}' triggered: {affectedCount}/{registeredCars.Count} cars affected (speed {rule.SpeedDelta:+#;-#;0} for {rule.Duration}s)");

        OnEventTriggered?.Invoke(rule, affectedCount);
        return affectedCount;
    }

    /// <summary>
    /// Trigger event by display name (for programmatic access / future UI / network sync).
    /// </summary>
    public void TriggerEventByName(string displayName)
    {
        for (int i = 0; i < Schedule.Events.Length; i++)
        {
            if (string.Equals(Schedule.Events[i].DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
            {
                TriggerEvent(i);
                return;
            }
        }
        Debug.LogWarning($"[EventManager] No event named '{displayName}' found in schedule");
    }

    public void ClearRegisteredCars()
    {
        registeredCars.Clear();
        isActive = false;
    }

    public int RegisteredCarCount => registeredCars.Count;
    public bool IsActive => isActive;
}
