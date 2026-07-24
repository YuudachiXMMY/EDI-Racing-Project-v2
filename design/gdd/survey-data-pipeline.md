# Survey & Data Pipeline

> **Status**: Accepted — reverse-engineered from codebase (v2)
> **Last Updated**: 2026-07-23
> **Source**: `SurveyConfig.cs`, `SurveyQuestion.cs`, `SurveyTemplates.cs`,
> `SurveyResponseMapper.cs`, `AttributeMapping.cs`, `CarData.cs`,
> `SurveyConfigManager.cs`, `CsvParser.cs`, `JsonImporter.cs`
> **ADR**: ADR-0005 (Dynamic Attribute Model), ADR-0007 (Web App Stack)

---

## 1. Overview

The survey data pipeline transforms student survey responses into car attributes
that drive race outcomes. It supports two input paths: CSV file import (v1
legacy) and JSON import from the EDI Survey Web App (v2). Survey configurations
define questions, attribute mappings, and event rules as a single portable JSON
bundle. Four built-in templates cover common EDI themes.

---

## 2. Player Fantasy

"I pick the 'Accessibility' survey template, my students fill it out on their
phones, and within minutes their responses become car attributes. I don't write
any code — the mapping is already configured. When I trigger 'Inaccessible
Building', students with disabilities see their cars slow down, and the class
discusses why."

---

## 3. Detailed Rules

### Data Flow

```
┌─────────────┐     ┌──────────────┐     ┌────────────────┐     ┌──────────┐
│ Web App      │────►│ JSON Export   │────►│ SurveyResponse │────►│ CarData  │
│ (SurveyJS)   │     │ (SurveyConfig)│     │ Mapper         │     │ (attrs)  │
└─────────────┘     └──────────────┘     └────────────────┘     └──────────┘
                                                                       │
┌─────────────┐     ┌──────────────┐                                   ▼
│ CSV File     │────►│ CsvParser    │──────────────────────────►│ CarData  │
│ (Legacy)     │     │              │                           │ (attrs)  │
└─────────────┘     └──────────────┘                           └──────────┘
```

### SurveyConfig Structure

```
SurveyConfig {
    ConfigName      // "Accessibility", "ENGG*1100 Survey"
    Description     // human-readable purpose
    Version         // "1.0"
    Questions[]     // SurveyQuestion array
    Mappings[]      // AttributeMapping array
    Rules[]         // SavedEventRule array
}
```

### Question Types

| Type | Value | Description |
|------|-------|-------------|
| Text | 0 | Free-text input |
| MultipleChoice | 1 | Single selection from options |
| MultiSelect | 2 | Multiple selections (limited by MaxValue) |
| Numeric | 3 | Number input with MinValue/MaxValue range |

### Attribute Mapping

```
AttributeMapping {
    QuestionId      // matches SurveyQuestion.Id
    AttributeName   // target attribute name on CarData
    DefaultValue    // fallback when response is missing
    TransformType   // "direct", "lookup", or "numeric"
    LookupEntries[] // for "lookup": response text → attribute value
}
```

### Transform Types

| Type | Behavior | Example |
|------|----------|---------|
| `direct` | Raw response becomes attribute value | "French" → language="French" |
| `lookup` | Map response text to value via table | "Blue" → colorIndex="3" |
| `numeric` | Parse as float, pass through | "25" → work_hours="25" |

### Mapping Process (SurveyResponseMapper)

```
for each mapping:
    response = FindResponse(responses, mapping.QuestionId)
    if response is null:
        value = mapping.DefaultValue
    else:
        value = ApplyTransform(response, mapping)
    add AttributeEntry(mapping.AttributeName, value)
return CarData(teamName, attributes)
```

### Dynamic Attribute Storage (CarData)

```
CarData {
    TeamName        // string
    Attributes[]    // AttributeEntry[] (Key-Value pairs)
}
```

Typed accessors: `GetAttribute()`, `GetIntAttribute()`, `GetFloatAttribute()`,
`HasAttribute()`, `ToDictionary()`.

Backward-compatible properties: `ColorIndex` (reads "colorIndex"), `Functions`
(reads "functions", splits by `/`).

### Built-In Templates

| Template | Questions | Mappings | Rules | Theme |
|----------|-----------|----------|-------|-------|
| V1 Parity | 0 | 0 | 7 | Original ENGG*1100 (CSV import) |
| Accessibility | 3 | 3 | 3 | Disability & assistive tech |
| Diversity | 3 | 3 | 4 | Language, first-gen, work hours |
| ENGG*1100 Survey | 14 | 7 | 7 | Full original questionnaire |

---

## 4. Formulas

### Lookup Transform

```
for each LookupEntry:
    if entry.Key == responseValue (case-insensitive):
        return entry.Value
return DefaultValue  // fallback
```

### Numeric Transform

```
if float.TryParse(responseValue):
    return responseValue  // valid number, pass through
else:
    return DefaultValue   // "0" typically
```

### CSV Parsing

```
Row 0: headers (column names)
Row 1+: data rows
Column 0: TeamName
Column 1+: each becomes AttributeEntry(header, value)
```

---

## 5. Edge Cases

| Scenario | Handling |
|----------|----------|
| Missing response for a mapping | Uses `DefaultValue` from mapping |
| Lookup key not found in table | Returns `DefaultValue` (falls back to raw response if no default) |
| Non-numeric value for numeric transform | Returns `DefaultValue` ("0") |
| Empty mappings array | Returns CarData with no attributes (name only) |
| Null responses array | Returns CarData with all defaults |
| Duplicate QuestionId in responses | First match returned |
| CSV with no data rows | Empty car list |
| SurveyConfig with no Questions | Valid — used by V1 Parity (CSV-only import) |

---

## 6. Dependencies

| Dependency | Role |
|-----------|------|
| CarData / AttributeEntry | Output data structure |
| SurveyConfigManager | Manages active config, applies rules to schedule |
| EventSchedule | Receives SavedEventRule[] from config |
| CsvParser | Legacy CSV import path |
| JsonImporter | JSON import from Web App |
| Web App (external) | React+Express+SQLite survey creation/collection |

---

## 7. Tuning Knobs

| Parameter | Location | Effect |
|-----------|----------|--------|
| Template selection | SurveyTemplates.TemplateNames | Which question set + rules |
| DefaultValue per mapping | AttributeMapping | Fallback for missing responses |
| TransformType per mapping | AttributeMapping | How raw responses become attributes |
| LookupEntries per mapping | AttributeMapping | Response-to-value translation table |
| Rules per config | SurveyConfig.Rules | Event rules bundled with survey |

---

## 8. Acceptance Criteria

- [ ] CSV import produces CarData with correct TeamName and attributes
- [ ] JSON import from Web App produces identical CarData to CSV for same data
- [ ] All 4 built-in templates load without error
- [ ] Lookup transform correctly maps response text to attribute values
- [ ] Numeric transform rejects non-numeric input and uses default
- [ ] Direct transform passes raw response through unchanged
- [ ] Missing responses fall back to DefaultValue
- [ ] Loaded SurveyConfig correctly populates EventSchedule rules
- [ ] CarData backward-compat: `ColorIndex` and `Functions` work from attributes
- [ ] Survey data → event rules → speed modifiers flow works end-to-end
