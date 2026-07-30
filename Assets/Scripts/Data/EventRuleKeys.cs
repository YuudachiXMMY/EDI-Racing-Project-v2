using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared Digit1-9 key bindings and SavedEventRule[] → EventRule[] conversion.
/// Single source of truth for the "assign sequential digit keys" logic that
/// was duplicated verbatim in SurveyConfigManager and RaceManager.
/// </summary>
public static class EventRuleKeys
{
    /// <summary>Digit1..Digit9 in order — the available trigger key bindings (max 9 rules).</summary>
    public static readonly Key[] DigitKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    /// <summary>
    /// Convert SavedEventRule[] to EventRule[], assigning Digit1..9 in order.
    /// Rules beyond the 9 available keys are truncated (caller may warn).
    /// </summary>
    public static EventRule[] AssignKeys(SavedEventRule[] rules)
    {
        if (rules == null) return System.Array.Empty<EventRule>();

        int count = Mathf.Min(rules.Length, DigitKeys.Length);
        var eventRules = new EventRule[count];
        for (int i = 0; i < count; i++)
            eventRules[i] = rules[i].ToRule(DigitKeys[i]);
        return eventRules;
    }
}
