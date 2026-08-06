import { ComparisonOperator, ComparisonOperatorLabels, WeatherTypeLabels, LogicOperator, LogicOperatorLabels } from '../constants.js';

export default function RuleRow({ rule, index, onChange, onDelete }) {
  function update(field, value) {
    onChange(index, { ...rule, [field]: value });
  }

  const isAll = rule.Operator === ComparisonOperator.All;
  const conditions = rule.Conditions || [];
  const hasCompound = conditions.length > 0;

  function updateCondition(ci, field, value) {
    const next = conditions.map((c, i) => i === ci ? { ...c, [field]: value } : c);
    update('Conditions', next);
  }

  function addCondition() {
    // When adding the first compound condition, migrate legacy fields
    if (conditions.length === 0 && rule.AttributeName) {
      update('Conditions', [
        { AttributeName: rule.AttributeName, Operator: rule.Operator, CompareValue: rule.CompareValue },
        { AttributeName: '', Operator: ComparisonOperator.Equals, CompareValue: '' }
      ]);
    } else {
      update('Conditions', [...conditions, { AttributeName: '', Operator: ComparisonOperator.Equals, CompareValue: '' }]);
    }
  }

  function removeCondition(ci) {
    const next = conditions.filter((_, i) => i !== ci);
    // When removing to 1 condition, migrate back to legacy fields
    if (next.length === 1) {
      onChange(index, {
        ...rule,
        AttributeName: next[0].AttributeName,
        Operator: next[0].Operator,
        CompareValue: next[0].CompareValue,
        Conditions: []
      });
    } else {
      update('Conditions', next);
    }
  }

  function renderConditionRow(cond, ci) {
    return (
      <div className="condition-row" key={ci}>
        <input
          type="text"
          value={cond.AttributeName}
          onChange={e => updateCondition(ci, 'AttributeName', e.target.value)}
          placeholder="attribute"
        />
        <select
          value={cond.Operator}
          onChange={e => updateCondition(ci, 'Operator', parseInt(e.target.value, 10))}
        >
          {ComparisonOperatorLabels.map((label, i) => (
            <option key={i} value={i}>{label}</option>
          ))}
        </select>
        <input
          type="text"
          value={cond.CompareValue}
          onChange={e => updateCondition(ci, 'CompareValue', e.target.value)}
          placeholder="value"
        />
        {conditions.length > 2 && (
          <button className="btn-danger btn-small" onClick={() => removeCondition(ci)}>×</button>
        )}
      </div>
    );
  }

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

      {hasCompound ? (
        <div className="rule-condition">
          <div className="logic-toggle">
            <span className="label">Logic:</span>
            <select
              value={rule.Logic ?? LogicOperator.And}
              onChange={e => update('Logic', parseInt(e.target.value, 10))}
            >
              {LogicOperatorLabels.map((label, i) => (
                <option key={i} value={i}>{label}</option>
              ))}
            </select>
          </div>
          <div className="condition-list">
            {conditions.map((c, ci) => renderConditionRow(c, ci))}
          </div>
          <button className="btn-small add-condition-btn" onClick={addCondition}>+ 添加条件</button>
        </div>
      ) : (
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
          {!isAll && (
            <button className="btn-small add-condition-btn" onClick={addCondition}>+ 添加条件</button>
          )}
        </div>
      )}

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
