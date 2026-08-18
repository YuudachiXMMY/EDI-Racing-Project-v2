/// <summary>
/// Comparison operators for the configurable rule engine.
/// Used in EventRule to match car attributes against values.
/// </summary>
public enum ComparisonOperator
{
    Equals,             // String or numeric equality (case-insensitive)
    NotEquals,          // Inverse of Equals
    Contains,           // Attribute value contains the comparison string
    NotContains,        // Inverse of Contains
    GreaterThan,        // Numeric comparison: attribute value > compare value
    LessThan,           // Numeric comparison: attribute value < compare value
    LengthGreaterThan,  // String length comparison: attribute.Length > compare value
    LengthLessThan,     // String length comparison: attribute.Length < compare value
    All                 // Matches all cars regardless of attributes (for global/weather events)
}
