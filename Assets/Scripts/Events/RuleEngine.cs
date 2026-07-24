using System;
using System.Linq;

/// <summary>
/// Evaluates EventRule conditions against car attributes.
/// Pure static utility — no MonoBehaviour, no state.
/// Replaces the hardcoded EventMatcher switch statement.
/// </summary>
public static class RuleEngine
{
    public static bool IsAffected(EventRule rule, CarIdentity car)
    {
        if (rule.Operator == ComparisonOperator.All)
            return true;

        // Compound: evaluate Conditions array when present
        if (rule.Conditions != null && rule.Conditions.Length > 0)
            return EvaluateConditions(rule.Conditions, rule.Logic, car);

        // Legacy: single condition (backward compat)
        return EvaluateSingleCondition(rule.AttributeName, rule.Operator, rule.CompareValue, car);
    }

    private static bool EvaluateConditions(RuleCondition[] conditions, LogicOperator logic, CarIdentity car)
    {
        if (logic == LogicOperator.And)
        {
            foreach (var c in conditions)
                if (!EvaluateSingleCondition(c.AttributeName, c.Operator, c.CompareValue, car))
                    return false;
            return true;
        }
        else // Or
        {
            foreach (var c in conditions)
                if (EvaluateSingleCondition(c.AttributeName, c.Operator, c.CompareValue, car))
                    return true;
            return false;
        }
    }

    private static bool EvaluateSingleCondition(string attributeName, ComparisonOperator op, string compareValue, CarIdentity car)
    {
        string attributeValue = ResolveAttributeValue(attributeName, car);

        switch (op)
        {
            case ComparisonOperator.Equals:
                return string.Equals(attributeValue, compareValue, StringComparison.OrdinalIgnoreCase);

            case ComparisonOperator.NotEquals:
                return !string.Equals(attributeValue, compareValue, StringComparison.OrdinalIgnoreCase);

            case ComparisonOperator.Contains:
                return ContainsValue(attributeValue, compareValue);

            case ComparisonOperator.NotContains:
                return !ContainsValue(attributeValue, compareValue);

            case ComparisonOperator.GreaterThan:
                return CompareNumeric(attributeValue, compareValue) > 0;

            case ComparisonOperator.LessThan:
                return CompareNumeric(attributeValue, compareValue) < 0;

            case ComparisonOperator.LengthGreaterThan:
                return CompareLengthNumeric(attributeValue, compareValue) > 0;

            case ComparisonOperator.LengthLessThan:
                return CompareLengthNumeric(attributeValue, compareValue) < 0;

            default:
                return false;
        }
    }

    private static string ResolveAttributeValue(string attributeName, CarIdentity car)
    {
        if (string.IsNullOrEmpty(attributeName))
            return "";

        if (string.Equals(attributeName, "teamName", StringComparison.OrdinalIgnoreCase))
            return car.TeamName ?? "";

        return car.GetAttribute(attributeName, "");
    }

    private static bool ContainsValue(string attributeValue, string target)
    {
        if (string.IsNullOrEmpty(attributeValue) || string.IsNullOrEmpty(target))
            return false;

        string trimmedTarget = target.Trim().ToLower();

        if (attributeValue.Contains("/"))
        {
            return attributeValue.Split('/')
                .Select(v => v.Trim().ToLower())
                .Any(v => v.Equals(trimmedTarget, StringComparison.OrdinalIgnoreCase));
        }

        return attributeValue.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CompareNumeric(string attributeValue, string compareValue)
    {
        if (float.TryParse(attributeValue, out float attrNum) &&
            float.TryParse(compareValue, out float compNum))
        {
            return attrNum.CompareTo(compNum);
        }
        return 0;
    }

    private static int CompareLengthNumeric(string attributeValue, string compareValue)
    {
        int length = (attributeValue ?? "").Length;
        if (int.TryParse(compareValue, out int threshold))
            return length.CompareTo(threshold);
        return 0;
    }
}
