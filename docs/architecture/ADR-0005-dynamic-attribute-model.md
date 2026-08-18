# ADR-0005: Dynamic Attribute Model (Dictionary-Based)

## Status

Accepted

## Date

2025-03-15

## Last Verified

2025-03-15

## Decision Makers

Project lead (professor + developer)

## Summary

Survey questions must map to car game attributes without code changes. A Dictionary<string, string> attribute model was chosen over fixed struct fields (inflexible) and ECS components (overengineered), allowing any survey question to create a new car attribute dynamically.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core (data model) |
| **Knowledge Risk** | LOW — Dictionary is standard C# |
| **References Consulted** | N/A |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | CarData can store and retrieve arbitrary key-value pairs |

## Context

### Problem Statement

V1 used a hardcoded `CarData` struct with fixed fields (teamName, colorIndex, functions). Any new survey question required code changes to add fields. The flexible survey system needs arbitrary attributes from any survey template without modifying C# code.

### Constraints

- Unity's `JsonUtility` does not support `Dictionary` serialization
- Existing race pipeline (CarSpawner → CarController → EventManager) must continue working
- Type coercion needed at access time (string storage, typed retrieval)

## Decision

Replace fixed `CarData` fields with `string TeamName + Dictionary<string, string> Attributes`. Type safety is handled via typed accessor methods (`GetFloat()`, `GetInt()`, `GetBool()`). Serialization uses a custom wrapper or Newtonsoft.Json.

Key classes:
- `CarData`: stores `teamName` + flat `Dictionary<string, string>`
- `AttributeMapping`: defines `QuestionId → AttributeName` with `TransformType` (direct, lookup, numeric)
- `SurveyResponseMapper`: applies mappings to produce `CarData` instances

## Alternatives Considered

### Alternative 1: Additional Fixed Fields

- **Pros**: Simple; type-safe at compile time
- **Cons**: Every new survey question requires code changes; breaks open-closed principle
- **Rejection Reason**: Inflexible — contradicts the goal of professor-configurable surveys

### Alternative 2: ECS-Style Components

- **Pros**: Maximum flexibility; cache-friendly
- **Cons**: Overengineered for this use case; Unity DOTS not used in project; complex API
- **Rejection Reason**: Unnecessary complexity for ~20 attributes per car

## Consequences

### Positive

- Any survey question maps to a car attribute without code changes
- Professor can create new survey templates independently
- Backward compatible — old CSV data still importable

### Negative

- No compile-time type checking on attribute names (runtime errors possible)
- String-based keys require careful naming conventions
- Serialization requires extra handling (JsonUtility limitation)

## Related

- [ADR-0006](ADR-0006-interpreted-rule-engine.md) — Rule engine reads dynamic attributes
- [ADR-0007](ADR-0007-web-app-stack-react-express-sqlite.md) — Web app produces JSON matching this model
