using System;
using System.Collections.Generic;

/// <summary>
/// Converts survey responses into CarData by applying AttributeMappings.
/// Pure static utility — no MonoBehaviour, no state.
/// </summary>
public static class SurveyResponseMapper
{
    /// <summary>
    /// Maps survey responses to car attributes using the provided mappings.
    /// </summary>
    /// <param name="teamName">Team name for the car.</param>
    /// <param name="responses">Survey responses as key=questionId, value=answer.</param>
    /// <param name="mappings">Attribute mappings defining how responses become car attributes.</param>
    /// <returns>CarData with teamName and mapped attributes.</returns>
    public static CarData MapResponses(string teamName,
        AttributeEntry[] responses, AttributeMapping[] mappings)
    {
        if (mappings == null || mappings.Length == 0)
            return new CarData(teamName, Array.Empty<AttributeEntry>());

        var attributes = new List<AttributeEntry>();

        for (int m = 0; m < mappings.Length; m++)
        {
            var mapping = mappings[m];
            string responseValue = FindResponse(responses, mapping.QuestionId);

            string attributeValue;
            if (responseValue == null)
            {
                attributeValue = mapping.DefaultValue ?? "";
            }
            else
            {
                attributeValue = ApplyTransform(responseValue, mapping);
            }

            attributes.Add(new AttributeEntry
            {
                Key = mapping.AttributeName,
                Value = attributeValue
            });
        }

        return new CarData(teamName, attributes.ToArray());
    }

    private static string FindResponse(AttributeEntry[] responses, string questionId)
    {
        if (responses == null || string.IsNullOrEmpty(questionId))
            return null;

        for (int i = 0; i < responses.Length; i++)
        {
            if (string.Equals(responses[i].Key, questionId, StringComparison.OrdinalIgnoreCase))
                return responses[i].Value;
        }
        return null;
    }

    private static string ApplyTransform(string responseValue, AttributeMapping mapping)
    {
        string transformType = mapping.TransformType ?? "direct";

        switch (transformType.ToLower())
        {
            case "lookup":
                return ApplyLookup(responseValue, mapping.LookupEntries, mapping.DefaultValue);

            case "numeric":
                if (float.TryParse(responseValue, out float _))
                    return responseValue;
                return mapping.DefaultValue ?? "0";

            case "direct":
            default:
                return responseValue;
        }
    }

    private static string ApplyLookup(string responseValue, AttributeEntry[] lookupEntries, string defaultValue)
    {
        if (lookupEntries == null || lookupEntries.Length == 0)
            return responseValue;

        for (int i = 0; i < lookupEntries.Length; i++)
        {
            if (string.Equals(lookupEntries[i].Key, responseValue, StringComparison.OrdinalIgnoreCase))
                return lookupEntries[i].Value;
        }

        return defaultValue ?? responseValue;
    }
}
