import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { getSurveys, createSurvey, deleteSurvey, getTemplates, logout, clearToken } from '../api.js';

export default function DashboardPage() {
  const navigate = useNavigate();
  const [surveys, setSurveys] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    const [surveyRes, templateRes] = await Promise.all([getSurveys(), getTemplates()]);
    if (surveyRes.success) setSurveys(surveyRes.data);
    if (templateRes.success) setTemplates(templateRes.data);
    setLoading(false);
  }

  async function handleCreate() {
    const result = await createSurvey({ configName: 'Untitled Survey' });
    if (result.success) navigate(`/surveys/${result.data.id}`);
  }

  async function handleCreateFromTemplate(template) {
    const result = await createSurvey({
      configName: template.name,
      description: template.description,
      questions: template.config.questions,
      mappings: template.config.mappings,
      rules: template.config.rules,
    });
    if (result.success) navigate(`/surveys/${result.data.id}`);
  }

  async function handleDelete(id, name) {
    if (!confirm(`Delete "${name}"? This cannot be undone.`)) return;
    const result = await deleteSurvey(id);
    if (result.success) setSurveys(prev => prev.filter(s => s.id !== id));
  }

  async function handleLogout() {
    await logout();
    clearToken();
    navigate('/login');
  }

  return (
    <div className="dashboard-page">
      <header className="app-header">
        <h1>EDI Survey Dashboard</h1>
        <button onClick={handleLogout} className="btn-secondary">Logout</button>
      </header>

      <div className="dashboard-actions">
        <button onClick={handleCreate} className="btn-primary">+ New Survey</button>

        {templates.length > 0 && (
          <div className="template-dropdown">
            <span>Start from template:</span>
            {templates.map(t => (
              <button key={t.name} onClick={() => handleCreateFromTemplate(t)} className="btn-secondary">
                {t.name}
              </button>
            ))}
          </div>
        )}
      </div>

      {loading ? (
        <p className="loading">Loading...</p>
      ) : surveys.length === 0 ? (
        <p className="empty">No surveys yet. Create one to get started.</p>
      ) : (
        <div className="survey-grid">
          {surveys.map(s => (
            <div key={s.id} className="survey-card" onClick={() => navigate(`/surveys/${s.id}`)}>
              <h3>{s.config_name}</h3>
              {s.description && <p className="description">{s.description}</p>}
              <div className="card-meta">
                <span className="share-code">Code: {s.share_code}</span>
                <span className="updated">Updated: {new Date(s.updated_at).toLocaleDateString()}</span>
              </div>
              <button
                className="btn-danger btn-small"
                onClick={e => { e.stopPropagation(); handleDelete(s.id, s.config_name); }}
              >
                Delete
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
