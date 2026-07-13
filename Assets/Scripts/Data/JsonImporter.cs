using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Parses Web App export JSON into CarData[] + SavedEventRule[].
/// JSON format: { configName, carData: [{ teamName, attributes: [{ key, value }] }], eventRules: [...] }
/// Note: field names use camelCase to match the Web App JSON output.
/// </summary>
public static class JsonImporter
{
    [Serializable]
    private class WebAppExport
    {
        public string configName = "";
        public WebAppCarData[] carData = Array.Empty<WebAppCarData>();
        public SavedEventRule[] eventRules = Array.Empty<SavedEventRule>();
    }

    [Serializable]
    private class WebAppCarData
    {
        public string teamName = "";
        public WebAppAttribute[] attributes = Array.Empty<WebAppAttribute>();
    }

    [Serializable]
    private class WebAppAttribute
    {
        public string key = "";
        public string value = "";
    }

    public static ImportResult Parse(string json)
    {
        if (string.IsNullOrEmpty(json))
            return ImportResult.Fail("JSON content is empty");

        WebAppExport export;
        try
        {
            export = JsonUtility.FromJson<WebAppExport>(json);
        }
        catch (Exception e)
        {
            return ImportResult.Fail($"Failed to parse JSON: {e.Message}");
        }

        if (export == null)
            return ImportResult.Fail("Failed to parse JSON: result was null");

        var cars = new List<CarData>();
        if (export.carData != null)
        {
            foreach (var webCar in export.carData)
            {
                if (string.IsNullOrEmpty(webCar.teamName))
                    continue;

                var attrs = new List<AttributeEntry>();
                if (webCar.attributes != null)
                {
                    foreach (var a in webCar.attributes)
                    {
                        attrs.Add(new AttributeEntry { Key = a.key, Value = a.value });
                    }
                }

                cars.Add(new CarData(webCar.teamName, attrs.ToArray()));
            }
        }

        return new ImportResult
        {
            Success = true,
            ConfigName = export.configName ?? "",
            Cars = cars,
            EventRules = export.eventRules ?? Array.Empty<SavedEventRule>()
        };
    }
}

public class ImportResult
{
    public bool Success;
    public string Error;
    public string ConfigName;
    public List<CarData> Cars;
    public SavedEventRule[] EventRules;

    public static ImportResult Fail(string error)
    {
        return new ImportResult
        {
            Success = false,
            Error = error,
            Cars = new List<CarData>(),
            EventRules = Array.Empty<SavedEventRule>()
        };
    }
}
