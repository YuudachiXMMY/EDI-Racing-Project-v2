import MappingRow from './MappingRow.jsx';
import { MAX_MAPPINGS } from '../constants.js';

export default function MappingsTab({ mappings, questions, onChange }) {
  const questionIds = (questions || []).map(q => q.Id).filter(Boolean);

  function handleAdd() {
    if (mappings.length >= MAX_MAPPINGS) return;
    onChange([...mappings, {
      QuestionId: '',
      AttributeName: '',
      DefaultValue: '',
      TransformType: 'direct',
      LookupEntries: [],
    }]);
  }

  function handleChange(index, updated) {
    const next = mappings.map((m, i) => i === index ? updated : m);
    onChange(next);
  }

  function handleDelete(index) {
    onChange(mappings.filter((_, i) => i !== index));
  }

  return (
    <div className="mappings-tab">
      <div className="tab-toolbar">
        <button
          onClick={handleAdd}
          disabled={mappings.length >= MAX_MAPPINGS}
          className="btn-primary"
        >
          + 添加映射
        </button>
        {mappings.length >= MAX_MAPPINGS && (
          <span className="warning">Max {MAX_MAPPINGS} reached</span>
        )}
      </div>

      {mappings.length === 0 ? (
        <p className="empty">No mappings. Add one to map question responses to car attributes.</p>
      ) : (
        mappings.map((m, i) => (
          <MappingRow
            key={i}
            mapping={m}
            index={i}
            questionIds={questionIds}
            onChange={handleChange}
            onDelete={handleDelete}
          />
        ))
      )}
    </div>
  );
}
