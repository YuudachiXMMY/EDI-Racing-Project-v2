import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { getSurveys, createSurvey, deleteSurvey, getTemplates, toggleSurveyActive, logout, clearToken, requestHostToken } from '../api.js';
import { buildHostLaunchUrl } from '../gameLaunch.js';
import SendToGameModal from '../components/SendToGameModal.jsx';

export default function DashboardPage() {
  const navigate = useNavigate();
  const [surveys, setSurveys] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [loading, setLoading] = useState(true);
  const [sendModalSurveyId, setSendModalSurveyId] = useState(null);

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
      postProcessing: template.config.postProcessing || [],
    });
    if (result.success) navigate(`/surveys/${result.data.id}`);
  }

  async function handleToggleActive(id, isActive) {
    const result = await toggleSurveyActive(id, isActive);
    if (result.success) {
      setSurveys(prev => prev.map(s => s.id === id ? { ...s, is_active: isActive ? 1 : 0 } : s));
    }
  }

  function copyShareLink(shareCode, e) {
    e.stopPropagation();
    const url = `${window.location.origin}/survey/#/s/${shareCode}`;
    navigator.clipboard.writeText(url).catch(() => {});
  }

  async function handleDelete(id, name) {
    if (!confirm(`Delete "${name}"? This cannot be undone.`)) return;
    const result = await deleteSurvey(id);
    if (result.success) setSurveys(prev => prev.filter(s => s.id !== id));
  }

  // Launch the game in professor host mode: mint a host token, then open the Unity build
  // at the game root with role/token/survey in the URL hash (Phase 2). Unity auto-creates
  // the room carrying the token — no manual in-game Host click or room-code handoff.
  async function handleHostGame(surveyId) {
    const result = await requestHostToken(surveyId);
    if (!result.success) {
      alert(`Failed to start host session: ${result.error || 'unknown error'}`);
      return;
    }
    window.open(buildHostLaunchUrl(result.data.token, surveyId), '_blank', 'noopener');
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
        <button onClick={() => navigate('/history')} className="btn-secondary">Session History</button>
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
              <div className="card-title-row">
                <h3>{s.config_name}</h3>
                <button
                  className={`btn-small active-toggle ${s.is_active ? 'active' : 'inactive'}`}
                  onClick={e => { e.stopPropagation(); handleToggleActive(s.id, !s.is_active); }}
                >
                  {s.is_active ? 'Active' : 'Inactive'}
                </button>
              </div>
              {s.description && <p className="description">{s.description}</p>}
              <div className="card-share-row" onClick={e => e.stopPropagation()}>
                <input
                  type="text"
                  className="share-url-mini"
                  value={`${window.location.origin}/survey/#/s/${s.share_code}`}
                  readOnly
                  onClick={e => e.target.select()}
                />
                <button className="btn-secondary btn-small" onClick={e => copyShareLink(s.share_code, e)}>
                  Copy
                </button>
              </div>
              <div className="card-meta">
                <span className="response-count">{s.response_count ?? 0} response(s)</span>
                <span className="updated">Updated: {new Date(s.updated_at).toLocaleDateString()}</span>
              </div>
              <div className="card-actions">
                {(s.response_count ?? 0) > 0 && (
                  <button
                    className="btn-primary btn-small"
                    onClick={e => { e.stopPropagation(); handleHostGame(s.id); }}
                  >
                    主持游戏
                  </button>
                )}
                {(s.response_count ?? 0) > 0 && (
                  <button
                    className="btn-primary btn-small"
                    onClick={e => { e.stopPropagation(); setSendModalSurveyId(s.id); }}
                  >
                    Send to Game
                  </button>
                )}
                <button
                  className="btn-danger btn-small"
                  onClick={e => { e.stopPropagation(); handleDelete(s.id, s.config_name); }}
                >
                  Delete
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {sendModalSurveyId && <SendToGameModal surveyId={sendModalSurveyId} onClose={() => setSendModalSurveyId(null)} />}
    </div>
  );
}
