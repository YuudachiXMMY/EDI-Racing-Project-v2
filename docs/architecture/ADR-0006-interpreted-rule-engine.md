# ADR-0006: Interpreted Rule Engine

## Status

Accepted

## Date

2025-03-15

## Last Verified

2025-03-15

## Decision Makers

Project lead (professor + developer)

## Summary

Professors define event rules through a UI (e.g., "if colorIndex == 3 then speed +15 for 6s"). An interpreted rule engine evaluates these rules at runtime using configurable comparison operators, chosen over code generation (over-complex) and expression trees (unnecessary for non-performance-critical evaluation).

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core (gameplay logic) |
| **Knowledge Risk** | LOW — pure C# logic, no engine-specific APIs |
| **References Consulted** | N/A |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | Rules trigger correct speed deltas during race events |

## Context

### Problem Statement

V1 hardcoded event rules. V2 requires professors to define custom rules that react to any car attribute. Rules are evaluated per-event (professor presses a key), not per-frame, so performance is not a concern.

### Constraints

- Rules must be configurable via UI (not code)
- Rules reference dynamic attributes (see ADR-0005)
- Evaluation frequency: per-event (~7 times per race), not per-frame
- Must support: ==, !=, <, >, <=, >=, contains, string length comparisons

## Decision

Use an **interpreted rule engine** with configurable operators. Each rule is a `SavedEventRule` containing: `AttributeName`, `Operator` (enum), `CompareValue`, `SpeedDelta`, `Duration`, `Weather`, `AllowRepeat`.

At event trigger time, `RuleEngine.Evaluate(rule, carData)` checks if the car's attribute satisfies the comparison, then applies the speed delta for the specified duration.

Operators defined in `ComparisonOperator` enum: Equal, NotEqual, LessThan, GreaterThan, LessOrEqual, GreaterOrEqual, StringLength, Contains, AlwaysTrue.

## Alternatives Considered

### Alternative 1: Code Generation

- **Pros**: Compile-time optimization; type safety
- **Cons**: Complex implementation; requires runtime compilation or IL emission; fragile
- **Rejection Reason**: Over-complex for ~7 rules evaluated ~7 times per race

### Alternative 2: Expression Trees

- **Pros**: .NET native; optimizable
- **Cons**: Complex API; limited WebGL support; unnecessary optimization for non-hot-path
- **Rejection Reason**: Unnecessary complexity; expression trees may have WebGL limitations

## Consequences

### Positive

- Simple to implement and debug
- Professors can create rules without coding knowledge
- Extensible — new operators added by extending the enum
- JSON-serializable for save/load

### Negative

- No compile-time validation of rule logic
- String-based attribute lookups (typos cause silent failures)
- Not suitable for per-frame evaluation (but not needed)

## Related

- [ADR-0005](ADR-0005-dynamic-attribute-model.md) — Rule engine reads dynamic attributes from CarData
