using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages race events: listens for keyboard triggers,
/// matches affected cars, applies speed modifiers.
/// </summary>
public class EventManager : MonoBehaviour
{
    [Header("Configuration")]
    public EventSchedule Schedule;

    private readonly List<CarIdentity> registeredCars = new List<CarIdentity>();
    private bool isActive;

    public event Action<RaceEventConfig, int> OnEventTriggered;

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
        Debug.Log($"[EventManager] Activated with {Schedule.Events.Length} events configured");
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

        var config = Schedule.Events[eventIndex];

        if (config.HasBeenTriggered && !config.AllowRepeat)
        {
            Debug.Log($"[EventManager] '{config.DisplayName}' already triggered (repeat disabled)");
            return;
        }

        int affectedCount = 0;
        foreach (var car in registeredCars)
        {
            if (EventMatcher.IsAffected(config, car))
            {
                var controller = car.GetComponent<CarController>();
                if (controller != null)
                {
                    controller.ApplySpeedModifier(config.SpeedDelta, config.Duration);
                    affectedCount++;
                }
            }
        }

        Schedule.Events[eventIndex].HasBeenTriggered = true;

        Debug.Log($"[EventManager] '{config.DisplayName}' triggered: {affectedCount}/{registeredCars.Count} cars affected (speed {config.SpeedDelta:+#;-#;0} for {config.Duration}s)");

        OnEventTriggered?.Invoke(config, affectedCount);
    }

    /// <summary>
    /// Trigger event by type (for programmatic access / future UI / network sync).
    /// </summary>
    public void TriggerEventByType(RaceEventType type)
    {
        for (int i = 0; i < Schedule.Events.Length; i++)
        {
            if (Schedule.Events[i].EventType == type)
            {
                TriggerEvent(i);
                return;
            }
        }
        Debug.LogWarning($"[EventManager] No event of type '{type}' found in schedule");
    }

    public int RegisteredCarCount => registeredCars.Count;
    public bool IsActive => isActive;
}
