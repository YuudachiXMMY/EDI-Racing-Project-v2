# Plan: Dynamic Data Model

## Summary
Refactor the hardcoded 3-field `CarData` struct (TeamName, ColorIndex, Functions) into a dynamic attribute system supporting arbitrary key-value pairs. Update all downstream consumers: CsvParser (header-based dynamic columns), CarIdentity (runtime attribute storage), SessionData/NetworkMessages (serialization), CarSpawner (attribute-based prefab selection), EventMatcher (attribute-based access), ScoreManager/ResultsExporter (dynamic attribute output).

## User Story
As a professor teaching any EDI-related course,
I want car data to support arbitrary survey attributes (not just teamName/colorIndex/functions),
So that I can import CSV files with custom columns and later build event rules on any attribute.

## Problem → Solution
CarData has 3 hardcoded fields → CarData stores TeamName + Dictionary<string,string> of arbitrary attributes, with backward-compatible accessors for colorIndex/functions.

## Metadata
- **Complexity**: Large
- **Source PRD**: `.claude/PRPs/prds/flexible-survey-and-mapping.prd.md`
- **PRD Phase**: Phase 1 — Dynamic Data Model
- **Estimated Files**: 11 modified, 0 created

---

## UX Design

Internal change — no user-facing UX transformation.

The only visible change is CSV format: the parser now expects a header row. All existing gameplay, UI, and camera behavior remains identical. Professors won't notice any difference until Phase 2+ adds new configuration UI.

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| CSV import | No header; fixed 3 columns | Header row required; any number of columns | First column = teamName, rest become attributes |
| Race gameplay | Unchanged | Unchanged | Backward-compat accessors ensure identical behavior |
| Results export | Fixed `ColorIndex` column | Dynamic attribute columns | All attributes included |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Data/CarData.cs` | all | Core struct being refactored |
| P0 | `Assets/Scripts/Data/CsvParser.cs` | all | Parser being rewritten for dynamic columns |
| P0 | `Assets/Scripts/Car/CarIdentity.cs` | all | Runtime state mirrors CarData |
| P1 | `Assets/Scripts/Race/CarSpawner.cs` | 30-109 | Uses ColorIndex for prefab/trail selection |
| P1 | `Assets/Scripts/Events/EventMatcher.cs` | all | Uses ColorIndex, Functions for matching |
| P1 | `Assets/Scripts/Data/SessionData.cs` | all | Serialization of CarData, CarResult |
| P1 | `Assets/Scripts/Network/NetworkMessages.cs` | 68-99 | NetCarData serialization |
| P1 | `Assets/Scripts/Race/ScoreManager.cs` | 43-66 | CollectResults uses ColorIndex |
| P1 | `Assets/Scripts/Data/ResultsExporter.cs` | all | CSV export uses ColorIndex |
| P2 | `Assets/Scripts/Race/RaceManager.cs` | 205-238 | BuildSessionData creates CarData |
| P2 | `Assets/Scripts/Network/NetworkSync.cs` | 143-151 | BroadcastRaceStart converts CarData |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| JsonUtility limitations | Unity docs | Does not serialize Dictionary, generics, or properties. Use serializable struct arrays. |
| Struct vs class for data | C# spec | Structs are value types — large attribute arrays may cause copying overhead. Keep CarData as struct but be mindful of passing by ref. |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/Data/CarData.cs:1-20
// PascalCase for public fields in structs, PascalCase for classes/methods
// No namespaces — all classes in global namespace
public struct CarData
{
    public string TeamName;
    public int ColorIndex;
}
```

### ERROR_HANDLING
```csharp
// SOURCE: Assets/Scripts/Data/CsvParser.cs:19-28
// Silent skip with continue for invalid data; no exceptions thrown
if (string.IsNullOrEmpty(trimmed)) continue;
var columns = trimmed.Split(',');
if (columns.Length < 2) continue;
if (!int.TryParse(columns[1].Trim(), out int colorIndex))
    colorIndex = 0;
```

### LOGGING_PATTERN
```csharp
// SOURCE: Assets/Scripts/Race/CarSpawner.cs:141
// [ClassName] prefix, descriptive message, variable interpolation
Debug.LogWarning($"[CarSpawner] No NavMesh found within {NavMeshSampleRadius}m of spawn for '{carName}'. Using raw position.");
// SOURCE: Assets/Scripts/Race/RaceManager.cs:62
Debug.Log($"[RaceManager] Parsed {carDataList.Count} cars from CSV");
```

### SERIALIZATION_PATTERN
```csharp
// SOURCE: Assets/Scripts/Data/SessionData.cs:30-57
// Serializable structs with static factory methods for conversion
[Serializable]
public struct SavedRaceConfig
{
    public float DefaultSpeed;
    // ...
    public static SavedRaceConfig FromScriptableObject(RaceConfig config) { ... }
    public void ApplyTo(RaceConfig config) { ... }
}
```

### NETWORK_DATA_PATTERN
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:76-99
// Compact structs with short field names, static conversion methods
[Serializable]
public struct NetCarData
{
    public string teamName;
    public int colorIndex;
    public string functions; // slash-separated
    public static NetCarData FromCarData(CarData cd) { ... }
    public CarData ToCarData() { ... }
}
```

### COMPONENT_INITIALIZE_PATTERN
```csharp
// SOURCE: Assets/Scripts/Car/CarIdentity.cs:20-29
// Initialize method copies data from CarData to MonoBehaviour fields
public void Initialize(CarData data)
{
    TeamName = data.TeamName;
    ColorIndex = data.ColorIndex;
    Functions = data.Functions;
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/Data/CarData.cs` | UPDATE | Core refactor: add Attributes array + accessor methods |
| `Assets/Scripts/Data/CsvParser.cs` | UPDATE | Rewrite for header-based dynamic column parsing |
| `Assets/Scripts/Car/CarIdentity.cs` | UPDATE | Store/expose dynamic attributes, replace fixed fields |
| `Assets/Scripts/Race/CarSpawner.cs` | UPDATE | Use attribute accessors instead of direct field access |
| `Assets/Scripts/Events/EventMatcher.cs` | UPDATE | Use attribute accessors on CarIdentity |
| `Assets/Scripts/Data/SessionData.cs` | UPDATE | Update CarResult to include dynamic attributes |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Update NetCarData for dynamic attributes |
| `Assets/Scripts/Race/ScoreManager.cs` | UPDATE | Use attribute accessors in CollectResults |
| `Assets/Scripts/Data/ResultsExporter.cs` | UPDATE | Export all dynamic attributes in CSV |
| `Assets/Scripts/Race/RaceManager.cs` | UPDATE | BuildSessionData uses new CarData constructor |
| `Assets/Scripts/Network/NetworkSync.cs` | UPDATE | Minor — uses NetCarData conversion (auto-fixed by NetCarData changes) |

## NOT Building

- Custom rule engine (Phase 2)
- Survey builder UI (Phase 4)
- Student survey system (Phase 5)
- JSON config file system (Phase 3)
- Any new UI panels or screens
- Attribute validation or type enforcement beyond basic accessors

---

## Step-by-Step Tasks

### Task 1: Refactor CarData struct
- **ACTION**: Replace `int ColorIndex` and `string[] Functions` with a serializable attribute system. Keep `TeamName` as first-class field.
- **IMPLEMENT**:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public struct AttributeEntry
{
    public string Key;
    public string Value;
}

[Serializable]
public struct CarData
{
    public string TeamName;
    public AttributeEntry[] Attributes;

    // --- Constructors ---

    public CarData(string teamName, AttributeEntry[] attributes)
    {
        TeamName = teamName;
        Attributes = attributes ?? Array.Empty<AttributeEntry>();
    }

    public CarData(string teamName, Dictionary<string, string> attributes)
    {
        TeamName = teamName;
        Attributes = attributes != null
            ? attributes.Select(kv => new AttributeEntry { Key = kv.Key, Value = kv.Value }).ToArray()
            : Array.Empty<AttributeEntry>();
    }

    // --- Generic Accessors ---

    public string GetAttribute(string key, string defaultValue = "")
    {
        if (Attributes == null) return defaultValue;
        for (int i = 0; i < Attributes.Length; i++)
            if (string.Equals(Attributes[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return Attributes[i].Value;
        return defaultValue;
    }

    public int GetIntAttribute(string key, int defaultValue = 0)
    {
        string val = GetAttribute(key, null);
        if (val != null && int.TryParse(val, out int result)) return result;
        return defaultValue;
    }

    public float GetFloatAttribute(string key, float defaultValue = 0f)
    {
        string val = GetAttribute(key, null);
        if (val != null && float.TryParse(val, out float result)) return result;
        return defaultValue;
    }

    public bool HasAttribute(string key)
    {
        if (Attributes == null) return false;
        for (int i = 0; i < Attributes.Length; i++)
            if (string.Equals(Attributes[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public string[] GetAttributeKeys()
    {
        if (Attributes == null) return Array.Empty<string>();
        return Attributes.Select(a => a.Key).ToArray();
    }

    public Dictionary<string, string> ToDictionary()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Attributes != null)
            foreach (var attr in Attributes)
                if (!string.IsNullOrEmpty(attr.Key))
                    dict[attr.Key] = attr.Value;
        return dict;
    }

    // --- Backward-Compatible Accessors (v1 parity) ---

    public int ColorIndex => GetIntAttribute("colorIndex", 0);

    public string[] Functions
    {
        get
        {
            string val = GetAttribute("functions", "");
            if (string.IsNullOrEmpty(val)) return Array.Empty<string>();
            return val.Split('/').Select(f => f.Trim().ToLower()).Where(f => f.Length > 0).ToArray();
        }
    }
}
```
- **MIRROR**: NAMING_CONVENTION (PascalCase fields), SERIALIZATION_PATTERN (static helpers)
- **IMPORTS**: `System`, `System.Collections.Generic`, `System.Linq`
- **GOTCHA**: `JsonUtility` cannot serialize `Dictionary<K,V>`. The `AttributeEntry[]` array is the workaround. All dictionary access goes through accessor methods, never direct field access. Structs are value types — the `Attributes` array reference is copied on struct copy, but array contents are shared. This is fine for read-only usage.
- **VALIDATE**: Verify `ColorIndex` and `Functions` properties return correct values when attributes contain `colorIndex` and `functions` keys. Verify `GetAttribute` is case-insensitive.

### Task 2: Rewrite CsvParser for dynamic columns
- **ACTION**: Support header row with arbitrary columns. First column always becomes TeamName; all other columns become attributes keyed by header name.
- **IMPLEMENT**:
```csharp
using System.Collections.Generic;
using System.Linq;

public static class CsvParser
{
    public static List<CarData> Parse(string csvContent)
    {
        var cars = new List<CarData>();
        if (string.IsNullOrEmpty(csvContent)) return cars;

        var lines = csvContent.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        if (lines.Length < 2) return cars; // need header + at least one data row

        // Parse header row
        string[] headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        if (headers.Length == 0) return cars;

        // Data rows
        for (int row = 1; row < lines.Length; row++)
        {
            string[] columns = lines[row].Split(',');
            if (columns.Length == 0) continue;

            string teamName = columns[0].Trim();
            if (string.IsNullOrEmpty(teamName)) continue;

            var attributes = new List<AttributeEntry>();
            for (int col = 1; col < headers.Length && col < columns.Length; col++)
            {
                string key = headers[col];
                if (string.IsNullOrEmpty(key)) continue;
                string value = columns[col].Trim();
                attributes.Add(new AttributeEntry { Key = key, Value = value });
            }

            cars.Add(new CarData(teamName, attributes.ToArray()));
        }

        return cars;
    }
}
```
- **MIRROR**: ERROR_HANDLING (silent skip for invalid rows), LOGGING_PATTERN
- **IMPORTS**: `System.Collections.Generic`, `System.Linq`
- **GOTCHA**: The first column header name is ignored — it always maps to `TeamName` regardless of what the header says. This allows headers like "teamName", "Team", or "Name" to all work. CSV values with commas inside quotes are NOT handled (matches v1 behavior — v1 didn't handle quoted CSV either). Empty column values produce empty-string attributes (not missing attributes).
- **VALIDATE**: Parse a CSV with headers `teamName,colorIndex,functions,language,score` and verify: (1) correct car count, (2) `car.ColorIndex` returns correct int, (3) `car.Functions` returns correct array, (4) `car.GetAttribute("language")` returns correct value.

### Task 3: Update CarIdentity
- **ACTION**: Replace fixed `ColorIndex` and `Functions` fields with `AttributeEntry[]` and accessor methods mirroring CarData.
- **IMPLEMENT**:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CarIdentity : MonoBehaviour
{
    [Header("Identity")]
    public string TeamName;
    public AttributeEntry[] Attributes;

    [Header("Race Progress")]
    public int CurrentCheckpointIndex;
    public int TotalCheckpointsPassed;
    public int CurrentLap;
    public float CheckpointTime;

    public void Initialize(CarData data)
    {
        TeamName = data.TeamName;
        Attributes = data.Attributes != null
            ? (AttributeEntry[])data.Attributes.Clone()
            : Array.Empty<AttributeEntry>();
        CurrentCheckpointIndex = 0;
        TotalCheckpointsPassed = 0;
        CurrentLap = 0;
        CheckpointTime = 0f;
    }

    // --- Attribute Accessors (mirror CarData) ---

    public string GetAttribute(string key, string defaultValue = "")
    {
        if (Attributes == null) return defaultValue;
        for (int i = 0; i < Attributes.Length; i++)
            if (string.Equals(Attributes[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return Attributes[i].Value;
        return defaultValue;
    }

    public int GetIntAttribute(string key, int defaultValue = 0)
    {
        string val = GetAttribute(key, null);
        if (val != null && int.TryParse(val, out int result)) return result;
        return defaultValue;
    }

    public bool HasAttribute(string key)
    {
        if (Attributes == null) return false;
        for (int i = 0; i < Attributes.Length; i++)
            if (string.Equals(Attributes[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // --- Backward-Compatible Accessors ---

    public int ColorIndex => GetIntAttribute("colorIndex", 0);

    public string[] Functions
    {
        get
        {
            string val = GetAttribute("functions", "");
            if (string.IsNullOrEmpty(val)) return Array.Empty<string>();
            return val.Split('/').Select(f => f.Trim().ToLower()).Where(f => f.Length > 0).ToArray();
        }
    }

    private void Update()
    {
        CheckpointTime += Time.deltaTime;
    }
}
```
- **MIRROR**: COMPONENT_INITIALIZE_PATTERN, NAMING_CONVENTION
- **IMPORTS**: `System`, `System.Collections.Generic`, `System.Linq`, `UnityEngine`
- **GOTCHA**: `Clone()` the `Attributes` array to prevent shared mutation between CarData and CarIdentity. The `ColorIndex` and `Functions` properties must match CarData behavior exactly for EventMatcher compatibility.
- **VALIDATE**: After `Initialize(carData)`, verify `identity.ColorIndex` matches `carData.ColorIndex` and `identity.Functions` matches `carData.Functions`.

### Task 4: Update CarSpawner
- **ACTION**: Replace direct `data.ColorIndex` field access with `data.ColorIndex` property (the backward-compat accessor). Replace `data.ColorIndex` parameter in `AddTrailRenderer`.
- **IMPLEMENT**: Change line 38 and line 96:
  - Line 38: `int prefabIndex = Mathf.Clamp(data.ColorIndex, 0, CarPrefabs.Length - 1);` — **no change needed**, `data.ColorIndex` is now a property that returns the same value.
  - Line 96: `AddTrailRenderer(car, data.ColorIndex);` — **no change needed**, same property.
  - Line 169: `AddTrailRenderer(car, data.ColorIndex);` — **no change needed**.
- **MIRROR**: N/A — accessor property is transparent
- **IMPORTS**: No changes
- **GOTCHA**: `CarSpawner` already uses `data.ColorIndex` which becomes a property accessor. C# struct properties work identically to fields for read access. **No code changes required** — the backward-compat property handles this transparently.
- **VALIDATE**: Spawn cars from a CSV with `colorIndex` column values 0-4 and verify correct prefab selection and trail colors.

### Task 5: Update EventMatcher
- **ACTION**: Replace `car.ColorIndex` field access and `car.Functions` field access with the new property accessors.
- **IMPLEMENT**: The existing code at `EventMatcher.cs:15,19,25`:
  - `car.ColorIndex` → already works (now a property)
  - `car.Functions` → already works (now a property)
  - `car.TeamName` → unchanged (still a field)
- **MIRROR**: N/A — accessor properties are transparent
- **IMPORTS**: No changes
- **GOTCHA**: `car.Functions` property now creates a new array on each call (splits from attribute string). This is fine since `IsAffected` is called infrequently (once per event trigger per car, not per frame). If performance becomes a concern, cache in CarIdentity. **No code changes required** — backward-compat properties handle this.
- **VALIDATE**: Trigger each of the 7 event types and verify same cars are affected as before.

### Task 6: Update SessionData
- **ACTION**: Update `CarResult` to include dynamic attributes. Update serialization helpers.
- **IMPLEMENT**:
  Replace `CarResult.ColorIndex` field with `AttributeEntry[]` and add backward-compat property:
```csharp
[Serializable]
public struct CarResult
{
    public int Rank;
    public string TeamName;
    public AttributeEntry[] Attributes;
    public int LapsCompleted;
    public int CheckpointsPassed;
    public float TotalTime;

    // Backward compatibility
    public int ColorIndex
    {
        get
        {
            if (Attributes == null) return 0;
            for (int i = 0; i < Attributes.Length; i++)
                if (string.Equals(Attributes[i].Key, "colorIndex", System.StringComparison.OrdinalIgnoreCase))
                    if (int.TryParse(Attributes[i].Value, out int val)) return val;
            return 0;
        }
    }
}
```
- **MIRROR**: SERIALIZATION_PATTERN
- **IMPORTS**: `System` (already imported)
- **GOTCHA**: `ColorIndex` is now a read-only property, not a field. Any code that assigns `ColorIndex = c.ColorIndex` must change to populate the `Attributes` array instead. This affects `ScoreManager.CollectResults`.
- **VALIDATE**: Save and load a session. Verify `CarResult.ColorIndex` returns the correct value. Verify all attributes survive round-trip serialization.

### Task 7: Update ScoreManager.CollectResults
- **ACTION**: Replace `ColorIndex = c.ColorIndex` with attribute copy from CarIdentity.
- **IMPLEMENT**: In `ScoreManager.cs:43-66`, change the CarResult construction:
```csharp
rankings[i] = new CarResult
{
    Rank = i + 1,
    TeamName = c.TeamName,
    Attributes = c.Attributes != null
        ? (AttributeEntry[])c.Attributes.Clone()
        : Array.Empty<AttributeEntry>(),
    LapsCompleted = c.CurrentLap,
    CheckpointsPassed = c.TotalCheckpointsPassed,
    TotalTime = c.CheckpointTime
};
```
- **MIRROR**: SERIALIZATION_PATTERN
- **IMPORTS**: `System` (for `Array.Empty`)
- **GOTCHA**: Must clone the array to avoid shared references between live CarIdentity and snapshot CarResult.
- **VALIDATE**: During a race, call `CollectResults` and verify all car attributes appear in the results.

### Task 8: Update ResultsExporter
- **ACTION**: Export all dynamic attributes as CSV columns instead of just `ColorIndex`.
- **IMPLEMENT**:
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class ResultsExporter
{
    public static string ExportRankingsCsv(RaceResults results)
    {
        if (results.Rankings == null || results.Rankings.Length == 0)
            return "Rank,TeamName,LapsCompleted,CheckpointsPassed,Time\n";

        // Collect all unique attribute keys across all cars
        var allKeys = new List<string>();
        foreach (var car in results.Rankings)
        {
            if (car.Attributes == null) continue;
            foreach (var attr in car.Attributes)
                if (!string.IsNullOrEmpty(attr.Key) && !allKeys.Contains(attr.Key))
                    allKeys.Add(attr.Key);
        }

        var sb = new StringBuilder();

        // Header
        sb.Append("Rank,TeamName");
        foreach (var key in allKeys)
            sb.Append($",{EscapeCsv(key)}");
        sb.AppendLine(",LapsCompleted,CheckpointsPassed,Time");

        // Data rows
        foreach (var car in results.Rankings)
        {
            sb.Append($"{car.Rank},{EscapeCsv(car.TeamName)}");
            foreach (var key in allKeys)
            {
                string val = "";
                if (car.Attributes != null)
                {
                    for (int i = 0; i < car.Attributes.Length; i++)
                    {
                        if (string.Equals(car.Attributes[i].Key, key, System.StringComparison.OrdinalIgnoreCase))
                        {
                            val = car.Attributes[i].Value;
                            break;
                        }
                    }
                }
                sb.Append($",{EscapeCsv(val)}");
            }
            sb.AppendLine($",{car.LapsCompleted},{car.CheckpointsPassed},{car.TotalTime:F2}");
        }
        return sb.ToString();
    }

    public static string ExportEventLogCsv(RaceResults results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,EventName,AffectedCount,TotalCars");
        if (results.EventLog == null) return sb.ToString();

        foreach (var entry in results.EventLog)
            sb.AppendLine($"{entry.Timestamp:F2},{EscapeCsv(entry.EventName)},{entry.AffectedCount},{entry.TotalCars}");

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\""))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
```
- **MIRROR**: NAMING_CONVENTION, ERROR_HANDLING (null checks)
- **IMPORTS**: `System.Collections.Generic`, `System.Linq`, `System.Text`
- **GOTCHA**: Attribute key order must be consistent across all cars. Use insertion-order list (not HashSet) to preserve column order matching the first car's attribute order. Cars with missing attributes get empty string values.
- **VALIDATE**: Export results from a race with custom attributes. Verify CSV has dynamic columns matching the input CSV headers.

### Task 9: Update NetworkMessages (NetCarData)
- **ACTION**: Replace fixed `colorIndex`/`functions` fields with dynamic attribute array.
- **IMPLEMENT**:
```csharp
[Serializable]
public struct NetAttribute
{
    public string k; // short names for network efficiency
    public string v;
}

[Serializable]
public struct NetCarData
{
    public string teamName;
    public NetAttribute[] attrs;

    public static NetCarData FromCarData(CarData cd)
    {
        NetAttribute[] netAttrs;
        if (cd.Attributes != null && cd.Attributes.Length > 0)
        {
            netAttrs = new NetAttribute[cd.Attributes.Length];
            for (int i = 0; i < cd.Attributes.Length; i++)
            {
                netAttrs[i] = new NetAttribute
                {
                    k = cd.Attributes[i].Key,
                    v = cd.Attributes[i].Value
                };
            }
        }
        else
        {
            netAttrs = Array.Empty<NetAttribute>();
        }
        return new NetCarData { teamName = cd.TeamName, attrs = netAttrs };
    }

    public CarData ToCarData()
    {
        AttributeEntry[] entries;
        if (attrs != null && attrs.Length > 0)
        {
            entries = new AttributeEntry[attrs.Length];
            for (int i = 0; i < attrs.Length; i++)
            {
                entries[i] = new AttributeEntry
                {
                    Key = attrs[i].k,
                    Value = attrs[i].v
                };
            }
        }
        else
        {
            entries = Array.Empty<AttributeEntry>();
        }
        return new CarData(teamName, entries);
    }
}
```
- **MIRROR**: NETWORK_DATA_PATTERN (short field names for bandwidth)
- **IMPORTS**: `System` (already imported)
- **GOTCHA**: Short field names `k`/`v` in `NetAttribute` minimize JSON payload size. `JsonUtility` serializes arrays of serializable structs correctly. Ensure `Array.Empty<NetAttribute>()` is used (not null) to avoid JsonUtility serialization issues.
- **VALIDATE**: Serialize a `RaceStartMessage` with dynamic attributes to JSON and back. Verify all attributes survive the round trip.

### Task 10: Update RaceManager.BuildSessionData
- **ACTION**: Update the CarData construction in `BuildSessionData` to use new constructor with attributes from CarIdentity.
- **IMPLEMENT**: In `RaceManager.cs:210-218`, change:
```csharp
// Before:
carList.Add(new CarData(id.TeamName, id.ColorIndex, id.Functions));
// After:
carList.Add(new CarData(id.TeamName, id.Attributes != null
    ? (AttributeEntry[])id.Attributes.Clone()
    : Array.Empty<AttributeEntry>()));
```
- **MIRROR**: SERIALIZATION_PATTERN
- **IMPORTS**: No changes needed
- **GOTCHA**: The old 3-parameter constructor `(string, int, string[])` no longer exists. This line MUST be updated or compilation fails.
- **VALIDATE**: During a race, press P (save session). Load the saved JSON and verify all car attributes are preserved.

### Task 11: Update default CSV data file
- **ACTION**: Add header row to the default CSV TextAsset referenced by RaceManager.DefaultCsvData.
- **IMPLEMENT**: Find the default CSV file in the project and add a header row:
```
teamName,colorIndex,functions
Bimonliftz,0,facerecog/glasses/password/distance/male
Zoom,2,password
...
```
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: The TextAsset reference in the scene must point to the updated file. If the CSV is embedded as a `.txt` or `.csv` asset, just add the header line. All existing data rows remain unchanged.
- **VALIDATE**: Start the game with the default CSV. Verify the same number of cars spawn with correct colors and functions.

---

## Testing Strategy

### Unit Tests

This is a Unity project without CLI test pipeline. Validation is done via Play Mode testing.

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Parse CSV with headers | `teamName,colorIndex,functions\nTeam1,2,password` | 1 car, ColorIndex=2, Functions=["password"] | No |
| Parse CSV with custom columns | `name,lang,score\nTeam1,en,7` | 1 car, GetAttribute("lang")="en", GetAttribute("score")="7" | No |
| Parse empty CSV | `""` | Empty list | Yes |
| Parse header-only CSV | `teamName,color` | Empty list | Yes |
| Parse CSV with missing columns | `name,a,b\nTeam1,val1` | 1 car, HasAttribute("b")=false | Yes |
| CarData.ColorIndex default | No "colorIndex" attribute | Returns 0 | Yes |
| CarData.Functions empty | No "functions" attribute | Returns empty array | Yes |
| NetCarData round trip | CarData with 5 attributes | Identical CarData after FromCarData→ToCarData | No |
| Session save/load round trip | SessionData with dynamic attributes | All attributes preserved | No |
| Results export dynamic columns | 3 cars with different attribute sets | CSV has union of all attribute columns | Yes |

### Edge Cases Checklist
- [x] Empty CSV content → empty car list
- [x] Header-only CSV → empty car list
- [x] Missing attribute values → empty string default
- [x] Non-numeric colorIndex → defaults to 0
- [x] Missing colorIndex attribute → defaults to 0
- [x] Missing functions attribute → empty array
- [x] Null Attributes array → safe empty defaults everywhere
- [x] Cars with different attribute sets → ResultsExporter handles missing keys
- [x] 50 cars with 10 attributes each → no performance issue (evaluated once)

---

## Validation Commands

### Build Verification
```
Unity Editor > File > Build Settings > Build (WebGL)
```
EXPECT: Zero compilation errors

### Play Mode Verification
```
Unity Editor > Play Mode in complete_track_demo scene
```
EXPECT: Cars spawn, race runs, events trigger, scoreboard works

### Manual Validation
- [ ] Open Unity, enter Play Mode in `complete_track_demo` scene
- [ ] Verify default CSV loads correctly (same number of cars, same colors)
- [ ] Press T — scoreboard shows team names and laps
- [ ] Press 1-7 — events trigger and affect correct cars
- [ ] Press P — session saves without error
- [ ] Press L — session loads and race restarts correctly
- [ ] Press X — results CSV exports with dynamic attribute columns
- [ ] Create a test CSV with custom headers (e.g., `name,language,accessibility,colorIndex,functions`) and assign as DefaultCsvData
- [ ] Verify custom attributes accessible via `CarIdentity.GetAttribute("language")`
- [ ] Test with NetworkManager: host room, verify student client receives dynamic attributes

---

## Acceptance Criteria
- [ ] `CarData` supports arbitrary key-value attributes via `AttributeEntry[]`
- [ ] `CsvParser` reads header row and maps columns dynamically
- [ ] `CarIdentity` stores and exposes dynamic attributes
- [ ] Backward-compatible accessors (`ColorIndex`, `Functions`) work identically to v2.0
- [ ] All 7 v1 event types work without modification to EventMatcher logic
- [ ] Session save/load preserves all dynamic attributes
- [ ] Network sync (NetCarData) carries all attributes
- [ ] Results export includes all attribute columns
- [ ] Default CSV with added header row works identically to before
- [ ] Zero compilation errors

## Completion Checklist
- [ ] Code follows discovered patterns (PascalCase, no namespaces, `[Serializable]` structs)
- [ ] Error handling matches codebase style (silent skip, default values)
- [ ] Logging follows `[ClassName]` prefix convention
- [ ] No hardcoded values (attribute keys are string constants via backward-compat properties)
- [ ] No unnecessary scope additions (no new UI, no rule engine, no config system)
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Struct copy overhead with large Attributes array | LOW | LOW | Arrays are reference types inside structs; only pointer is copied. Actual data shared. |
| JsonUtility fails to serialize AttributeEntry[] | LOW | HIGH | AttributeEntry is a plain [Serializable] struct with string fields — JsonUtility handles this. Test early. |
| EventMatcher.Functions property creates array each call | LOW | LOW | Called once per event trigger per car (not per frame). Cache in CarIdentity if needed. |
| Default CSV TextAsset not found or wrong format | LOW | MEDIUM | Verify the asset reference in the scene Inspector after adding header row. |
| Breaking change affects saved session files from v2.0 | MEDIUM | LOW | Old session JSONs won't load (different CarData shape). Acceptable per PRD decision. |

## Notes

- The `AttributeEntry` struct is intentionally simple (two strings) rather than supporting typed values. Type coercion happens at access time via `GetIntAttribute`/`GetFloatAttribute`. This keeps serialization simple and avoids JsonUtility limitations.
- `CarData.ColorIndex` and `CarData.Functions` are backward-compatible **read-only properties**, not fields. This means code that previously assigned `data.ColorIndex = x` must change to use the attribute system. The only place that constructed CarData with the old 3-param constructor is `RaceManager.BuildSessionData` (Task 10).
- The old `CarData(string, int, string[])` constructor is intentionally removed. Any compilation error from this removal will surface all call sites that need updating — this is a safety net.
- `AttributeEntry` is defined in `CarData.cs` since it's the foundational type used by CarData, CarIdentity, SessionData, and NetworkMessages. No separate file needed.
