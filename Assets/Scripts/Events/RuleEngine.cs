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

        string attributeValue = ResolveAttributeValue(rule.AttributeName, car);

        switch (rule.Operator)
        {
            case ComparisonOperator.Equals:
                return string.Equals(attributeValue, rule.CompareValue, StringComparison.OrdinalIgnoreCase);

            case ComparisonOperator.NotEquals:
                return !string.Equals(attributeValue, rule.CompareValue, StringComparison.OrdinalIgnoreCase);

            case ComparisonOperator.Contains:
                return ContainsValue(attributeValue, rule.CompareValue);

            case ComparisonOperator.NotContains:
                return !ContainsValue(attributeValue, rule.CompareValue);

            case ComparisonOperator.GreaterThan:
                return CompareNumeric(attributeValue, rule.CompareValue) > 0;

            case ComparisonOperator.LessThan:
                return CompareNumeric(attributeValue, rule.CompareValue) < 0;

            case ComparisonOperator.LengthGreaterThan:
                return CompareLengthNumeric(attributeValue, rule.CompareValue) > 0;

            case ComparisonOperator.LengthLessThan:
                return CompareLengthNumeric(attributeValue, rule.CompareValue) < 0;

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
