// Must match Unity C# enum values exactly

export const QuestionType = { Text: 0, MultipleChoice: 1, Numeric: 2, MultiSelect: 3 };

export const ComparisonOperator = {
  Equals: 0,
  NotEquals: 1,
  Contains: 2,
  NotContains: 3,
  GreaterThan: 4,
  LessThan: 5,
  LengthGreaterThan: 6,
  LengthLessThan: 7,
  All: 8
};

export const ComparisonOperatorLabels = [
  'Equals', 'Not Equals', 'Contains', 'Not Contains',
  'Greater Than', 'Less Than', 'Length >', 'Length <', 'All (global)'
];

export const WeatherType = { None: 0, Snow: 1, Night: 2, Sunset: 3 };

export const WeatherTypeLabels = ['None', 'Snow', 'Night', 'Sunset'];

export const LogicOperator = { And: 0, Or: 1 };
export const LogicOperatorLabels = ['AND (all must match)', 'OR (any can match)'];

export const TransformTypes = ['direct', 'lookup', 'numeric'];

export const MAX_QUESTIONS = 20;
export const MAX_MAPPINGS = 20;
export const MAX_RULES = 9;
