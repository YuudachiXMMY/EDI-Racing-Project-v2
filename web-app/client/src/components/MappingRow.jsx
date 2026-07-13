import { TransformTypes } from '../constants.js';

export default function MappingRow({ mapping, index, questionIds, onChange, onDelete }) {
  function update(field, value) {
    onChange(index, { ...mapping, [field]: value });
  }

  function updateLookupText(text) {
    const entries = text.split('|').map(pair => {
      const eqIdx = pair.indexOf('=');
      if (eqIdx > 0) {
        return { Key: pair.substring(0, eqIdx).trim(), Value: pair.substring(eqIdx + 1).trim() };
      }
      return null;
    }).filter(Boolean);
    onChange(index, { ...mapping, LookupEntries: entries, _lookupText: text });
  }

  const lookupText = mapping._lookupText ??
    (mapping.LookupEntries || []).map(e => `${e.Key}=${e.Value}`).join('|');

  return (
    <div className="mapping-row">
      <div className="row-fields">
        <label>Question:
          <select
            value={mapping.QuestionId}
            onChange={e => update('QuestionId', e.target.value)}
          >
            <option value="">-- select --</option>
            {questionIds.map(id => <option key={id} value={id}>{id}</option>)}
          </select>
        </label>

        <span className="arrow">→</span>

        <label>Attribute:
          <input
            type="text"
            value={mapping.AttributeName}
            onChange={e => update('AttributeName', e.target.value)}
            placeholder="attribute_name"
          />
        </label>

        <label>Transform:
          <select
            value={mapping.TransformType}
            onChange={e => update('TransformType', e.target.value)}
          >
            {TransformTypes.map(t => <option key={t} value={t}>{t}</option>)}
          </select>
        </label>

        <label>Default:
          <input
            type="text"
            value={mapping.DefaultValue}
            onChange={e => update('DefaultValue', e.target.value)}
            placeholder="default"
          />
        </label>

        <button className="btn-danger btn-small" onClick={() => onDelete(index)}>X</button>
      </div>

      {mapping.TransformType === 'lookup' && (
        <div className="lookup-section">
          <label>Lookup (key=val|...):
            <input
              type="text"
              value={lookupText}
              onChange={e => updateLookupText(e.target.value)}
              placeholder="Yes=yes|No=no"
            />
          </label>
        </div>
      )}
    </div>
  );
}
