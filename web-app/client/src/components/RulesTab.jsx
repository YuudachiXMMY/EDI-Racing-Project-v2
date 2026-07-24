import RuleRow from './RuleRow.jsx';
import { ComparisonOperator, WeatherType, LogicOperator, MAX_RULES } from '../constants.js';

export default function RulesTab({ rules, onChange }) {
  function handleAdd() {
    if (rules.length >= MAX_RULES) return;
    onChange([...rules, {
      DisplayName: '',
      Logic: LogicOperator.And,
      Conditions: [],
      AttributeName: '',
      Operator: ComparisonOperator.Equals,
      CompareValue: '',
      SpeedDelta: -10,
      Duration: 8,
      Weather: WeatherType.None,
      AllowRepeat: false,
    }]);
  }

  function handleChange(index, updated) {
    const next = rules.map((r, i) => i === index ? updated : r);
    onChange(next);
  }

  function handleDelete(index) {
    onChange(rules.filter((_, i) => i !== index));
  }

  return (
    <div className="rules-tab">
      <div className="tab-toolbar">
        <button
          onClick={handleAdd}
          disabled={rules.length >= MAX_RULES}
          className="btn-primary"
        >
          + Add Rule
        </button>
        {rules.length >= MAX_RULES && (
          <span className="warning">Max {MAX_RULES} (keys 1-9)</span>
        )}
      </div>

      {rules.length === 0 ? (
        <p className="empty">No rules. Add one to configure race events.</p>
      ) : (
        rules.map((r, i) => (
          <RuleRow
            key={i}
            rule={r}
            index={i}
            onChange={handleChange}
            onDelete={handleDelete}
          />
        ))
      )}
    </div>
  );
}
