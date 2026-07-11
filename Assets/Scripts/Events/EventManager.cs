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

    private void Update()
    {
        if (!isActive || Schedule == null) return;

        for (int i = 0; i < Schedule.Events.Length; i++)
        {
            if (Keyboard.current != null && Keyboard.current[Schedule.Events[i].TriggerKey].wasPressedThisFrame)
            {
                TriggerEvent(i);
            }
        }
    }

    public void TriggerEvent(int eventIndex)
    {
        if (eventIndex < 0 || eventIndex >= Schedule.Events.Length) return;

        var rule = Schedule.Events[eventIndex];

        if (rule.HasBeenTriggered && !rule.AllowRepeat)
        {
            Debug.Log($"[EventManager] '{rule.DisplayName}' already triggered (repeat disabled)");
            return;
        }

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

        Schedule.Events[eventIndex].HasBeenTriggered = true;

        Debug.Log($"[EventManager] '{rule.DisplayName}' triggered: {affectedCount}/{registeredCars.Count} cars affected (speed {rule.SpeedDelta:+#;-#;0} for {rule.Duration}s)");

        OnEventTriggered?.Invoke(rule, affectedCount);
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
