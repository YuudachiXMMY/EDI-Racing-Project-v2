import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { getSurveys, createSurvey, archiveSurvey, duplicateSurvey, getTemplates, toggleSurveyActive, logout, clearToken, requestHostToken } from '../api.js';
import { submitHostLaunch, buildShareUrl } from '../gameLaunch.js';
import HostRoomPanel from '../components/HostRoomPanel.jsx';

export default function DashboardPage() {
  const navigate = useNavigate();
  const [surveys, setSurveys] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [loading, setLoading] = useState(true);
  // Survey whose Host Room panel (student link + QR) is open, or null when dismissed.
  const [hostingSurveyId, setHostingSurveyId] = useState(null);
  // Whether the collapsible "Archived" section is expanded.
  const [showArchived, setShowArchived] = useState(false);

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
    const url = buildShareUrl(shareCode);
    navigator.clipboard.writeText(url).catch(() => {});
  }

  async function handleArchive(id, name) {
    if (!confirm(`Archive "${name}"? It will be hidden from your active surveys but can be restored anytime.`)) return;
    const result = await archiveSurvey(id, true);
    if (result.success) setSurveys(prev => prev.map(s => s.id === id ? { ...s, is_archived: 1 } : s));
  }

  async function handleRestore(id) {
    const result = await archiveSurvey(id, false);
    if (result.success) setSurveys(prev => prev.map(s => s.id === id ? { ...s, is_archived: 0 } : s));
  }

  async function handleDuplicate(id, e) {
    e.stopPropagation();
    const result = await duplicateSurvey(id);
    if (result.success) loadData();
    else alert(`Failed to duplicate: ${result.error || 'unknown error'}`);
  }

  // Launch the game in professor host mode: mint a host token, then POST it to the access
  // gateway (/api/game/enter) which sets the game-access cookie and 302-redirects into the
  // Unity build at /game/#role/token/survey. Unity auto-creates the room carrying the token —
  // no manual in-game Host click or room-code handoff. The token goes via a form POST, never a
  // URL query, so it is not captured by access logs or browser history (see submitHostLaunch).
  async function handleHostGame(surveyId) {
    // Open the game tab SYNCHRONOUSLY inside the click handler. If we opened it after the
    // `await` below, Safari (and Chrome under some settings) treat the popup as non-user-
    // initiated and block it silently — the professor clicks "Host Game" and nothing happens.
    // We can't pass 'noopener' here (that makes window.open return null, so we couldn't write
    // the launch form into the tab after the token resolves). The target is same-origin, so
    // keeping the opener link is not a cross-origin tabnabbing vector — and we must NOT sever it
    // (opener=null): disowning the popup makes submitHostLaunch's same-origin document write miss,
    // stranding the tab on about:blank.
    const gameTab = window.open('about:blank', '_blank');
    const result = await requestHostToken(surveyId);
    if (!result.success) {
      if (gameTab) gameTab.close();
      alert(`Failed to start host session: ${result.error || 'unknown error'}`);
      return;
    }
    // Popup-blocked case (gameTab null): submitHostLaunch falls back to a new '_blank' tab.
    submitHostLaunch(result.data.token, surveyId, gameTab);
    // Surface the student join link + QR here in the dashboard. The panel polls the server for the
    // room code the game tab is about to create (Unity owns room creation; the code isn't known yet).
    setHostingSurveyId(surveyId);
  }

  async function handleLogout() {
    await logout();
    clearToken();
    navigate('/login');
  }

  const activeSurveys = surveys.filter(s => !s.is_archived);
  const archivedSurveys = surveys.filter(s => s.is_archived);

  return (
    <div className="dashboard-page">
      <header className="app-header">
        <h1>EDI Survey Dashboard</h1>
        <button onClick={() => navigate('/history')} className="btn-secondary">Session History</button>
        <button onClick={handleLogout} className="btn-secondary">Log Out</button>
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
      ) : activeSurveys.length === 0 ? (
        <p className="empty">No surveys yet. Create one to get started.</p>
      ) : (
        <div className="survey-grid">
          {activeSurveys.map(s => (
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
                  value={buildShareUrl(s.share_code)}
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
                    Host Game
                  </button>
                )}
                <button
                  className="btn-secondary btn-small"
                  onClick={e => handleDuplicate(s.id, e)}
                >
                  Duplicate
                </button>
                <button
                  className="btn-ghost-danger btn-small"
                  onClick={e => { e.stopPropagation(); handleArchive(s.id, s.config_name); }}
                >
                  Archive
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {!loading && archivedSurveys.length > 0 && (
        <section className="archived-section">
          <button
            className="btn-secondary archived-toggle"
            onClick={() => setShowArchived(v => !v)}
          >
            {showArchived ? '▾' : '▸'} Archived ({archivedSurveys.length})
          </button>

          {showArchived && (
            <div className="survey-grid archived-grid">
              {archivedSurveys.map(s => (
                <div key={s.id} className="survey-card archived-card">
                  <div className="card-title-row">
                    <h3>{s.config_name}</h3>
                  </div>
                  {s.description && <p className="description">{s.description}</p>}
                  <div className="card-meta">
                    <span className="response-count">{s.response_count ?? 0} response(s)</span>
                    <span className="updated">Updated: {new Date(s.updated_at).toLocaleDateString()}</span>
                  </div>
                  <div className="card-actions">
                    <button
                      className="btn-primary btn-small"
                      onClick={() => handleRestore(s.id)}
                    >
                      Restore
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      )}

      {hostingSurveyId !== null && (
        <HostRoomPanel surveyId={hostingSurveyId} onClose={() => setHostingSurveyId(null)} />
      )}
    </div>
  );
}
