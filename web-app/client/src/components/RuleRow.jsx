import { ComparisonOperator, ComparisonOperatorLabels, WeatherTypeLabels } from '../constants.js';

export default function RuleRow({ rule, index, onChange, onDelete }) {
  function update(field, value) {
    onChange(index, { ...rule, [field]: value });
  }

  const isAll = rule.Operator === ComparisonOperator.All;

  return (
    <div className="rule-row">
      <div className="rule-header">
        <label>Rule name:
          <input
            type="text"
            value={rule.DisplayName}
            onChange={e => update('DisplayName', e.target.value)}
            placeholder="Rule name..."
          />
        </label>
        <button className="btn-danger btn-small" onClick={() => onDelete(index)}>X</button>
      </div>

      <div className="rule-condition">
        <span className="label">If</span>
        <input
          type="text"
          value={rule.AttributeName}
          onChange={e => update('AttributeName', e.target.value)}
          placeholder="attribute"
          disabled={isAll}
        />
        <select
          value={rule.Operator}
          onChange={e => update('Operator', parseInt(e.target.value, 10))}
        >
          {ComparisonOperatorLabels.map((label, i) => (
            <option key={i} value={i}>{label}</option>
          ))}
        </select>
        <input
          type="text"
          value={rule.CompareValue}
          onChange={e => update('CompareValue', e.target.value)}
          placeholder="value"
          disabled={isAll}
        />
      </div>

      <div className="rule-effect">
        <label>Speed:
          <input
            type="number"
            step="0.1"
            value={rule.SpeedDelta}
            onChange={e => update('SpeedDelta', parseFloat(e.target.value) || 0)}
          />
        </label>
        <label>Duration(s):
          <input
            type="number"
            step="0.1"
            min="0"
            value={rule.Duration}
            onChange={e => update('Duration', parseFloat(e.target.value) || 0)}
          />
        </label>
        <label>Weather:
          <select
            value={rule.Weather}
            onChange={e => update('Weather', parseInt(e.target.value, 10))}
          >
            {WeatherTypeLabels.map((label, i) => (
              <option key={i} value={i}>{label}</option>
            ))}
          </select>
        </label>
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={rule.AllowRepeat}
            onChange={e => update('AllowRepeat', e.target.checked)}
          />
          Repeat
        </label>
      </div>
    </div>
  );
}
