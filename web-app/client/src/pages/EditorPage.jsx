import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getSurvey, updateSurvey, exportExcel, exportCsv, getResponseCount, toggleSurveyActive, requestHostToken, duplicateSurvey } from '../api.js';
import { submitHostLaunch } from '../gameLaunch.js';
import QuestionsTab from '../components/QuestionsTab.jsx';
import MappingsTab from '../components/MappingsTab.jsx';
import RulesTab from '../components/RulesTab.jsx';
import ResponsesTab from '../components/ResponsesTab.jsx';
import ResultsTab from '../components/ResultsTab.jsx';
import SharePanel from '../components/SharePanel.jsx';
import HostRoomPanel from '../components/HostRoomPanel.jsx';
import { downloadBlobObject } from '../utils/csvExport.js';

const TABS = ['Questions', 'Mappings', 'Rules', 'Responses', 'Results'];
const SAVE_DELAY = 2000;

export default function EditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [survey, setSurvey] = useState(null);
  const [activeTab, setActiveTab] = useState(0);
  const [saveStatus, setSaveStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [responseCount, setResponseCount] = useState(0);
  // True while the Host Room panel (student link + QR) is open for this survey.
  const [hostPanelOpen, setHostPanelOpen] = useState(false);
  const saveTimer = useRef(null);
  const latestData = useRef(null);

  useEffect(() => {
    loadSurvey();
    return () => { if (saveTimer.current) clearTimeout(saveTimer.current); };
  }, [id]);

  async function loadSurvey() {
    setLoading(true);
    const [surveyRes, countRes] = await Promise.all([getSurvey(id), getResponseCount(id)]);
    if (surveyRes.success) {
      setSurvey(surveyRes.data);
      latestData.current = surveyRes.data;
    }
    if (countRes.success) setResponseCount(countRes.data.count);
    setLoading(false);
  }

  // Poll response count every 10s
  useEffect(() => {
    const timer = setInterval(async () => {
      const countRes = await getResponseCount(id);
      if (countRes.success) setResponseCount(countRes.data.count);
    }, 10000);
    return () => clearInterval(timer);
  }, [id]);

  // Launch the game in professor host mode: mint a host token, then POST it to the access gateway
  // (/api/game/enter), which sets the game-access cookie and redirects into /game/#role/token/survey.
  // Mirrors the dashboard's host flow — the game tab is opened synchronously inside the click handler
  // so the browser treats it as user-initiated (not a blocked popup); the token is fetched after, then
  // submitted via a form POST (never a URL query, so it stays out of access logs / browser history).
  async function handleHostGame() {
    const gameTab = window.open('about:blank', '_blank');
    const result = await requestHostToken(id);
    if (!result.success) {
      if (gameTab) gameTab.close();
      alert(`Failed to start host session: ${result.error || 'unknown error'}`);
      return;
    }
    // Do NOT sever the opener (opener=null): the game tab is same-origin (not a tabnabbing
    // vector), and disowning it makes submitHostLaunch's same-origin document write miss,
    // stranding the tab on about:blank.
    // Popup-blocked case (gameTab null): submitHostLaunch falls back to a new '_blank' tab.
    submitHostLaunch(result.data.token, id, gameTab);
    // Show the student join link + QR here; the panel polls for the room code the game creates.
    setHostPanelOpen(true);
  }

  async function handleDuplicate() {
    const result = await duplicateSurvey(id);
    if (result.success) navigate(`/surveys/${result.data.id}`);
    else alert(`Failed to duplicate: ${result.error || 'unknown error'}`);
  }

  async function handleExportExcel() {
    const result = await exportExcel(id);
    if (result.success) downloadBlobObject(result.blob, result.filename);
  }

  async function handleExportCsv() {
    const result = await exportCsv(id);
    if (result.success) downloadBlobObject(result.blob, result.filename);
  }

  const handleChange = useCallback((field, value) => {
    setSurvey(prev => {
      const updated = { ...prev, [field]: value };
      latestData.current = updated;
      return updated;
    });

    // Debounced auto-save
    if (saveTimer.current) clearTimeout(saveTimer.current);
    setSaveStatus('Unsaved changes...');
    saveTimer.current = setTimeout(() => doSave(), SAVE_DELAY);
  }, [id]);

  async function doSave() {
    const data = latestData.current;
    if (!data) return;
    setSaveStatus('Saving...');
    const result = await updateSurvey(id, {
      configName: data.config_name,
      description: data.description || '',
      questions: data.questions,
      mappings: data.mappings,
      rules: data.rules,
      postProcessing: data.postProcessing || [],
    });
    setSaveStatus(result.success ? 'Saved' : 'Error saving');
  }

  async function handleToggleActive(isActive) {
    const result = await toggleSurveyActive(id, isActive);
    if (result.success) {
      setSurvey(prev => ({ ...prev, is_active: isActive ? 1 : 0 }));
    }
  }

  function handleNameChange(e) {
    handleChange('config_name', e.target.value);
  }

  if (loading) return <p className="loading">Loading survey...</p>;
  if (!survey) return <p className="error">Survey not found</p>;

  return (
    <div className="editor-page">
      <header className="editor-header">
        <button onClick={() => navigate('/dashboard')} className="btn-secondary">← Back to Dashboard</button>
        <input
          type="text"
          className="survey-name-input"
          value={survey.config_name}
          onChange={handleNameChange}
          placeholder="Survey name"
        />
        <div className="editor-meta">
          <span className="save-status">{saveStatus}</span>
          <span className="response-count">{responseCount} response(s)</span>
        </div>
        <div className="editor-actions">
          <button
            onClick={handleHostGame}
            className="btn-primary"
            disabled={responseCount === 0}
            title={responseCount === 0 ? 'Collect at least one response before hosting' : 'Launch the game and host a room'}
          >
            Host Game
          </button>
          <div className="toolbar-group" role="group" aria-label="Export data">
            <button onClick={handleExportExcel} className="btn-secondary" disabled={responseCount === 0}>
              Export Excel
            </button>
            <button onClick={handleExportCsv} className="btn-secondary" disabled={responseCount === 0}>
              Export CSV
            </button>
          </div>
          <button onClick={handleDuplicate} className="btn-secondary">
            Duplicate
          </button>
        </div>
      </header>

      <SharePanel
        shareCode={survey.share_code}
        isActive={!!survey.is_active}
        onToggleActive={handleToggleActive}
      />

      <div className="tab-bar">
        {TABS.map((tab, i) => (
          <button
            key={tab}
            className={`tab-btn ${activeTab === i ? 'active' : ''}`}
            onClick={() => setActiveTab(i)}
          >
            {tab}
          </button>
        ))}
      </div>

      <div className="tab-content">
        {activeTab === 0 && (
          <QuestionsTab
            questions={survey.questions || []}
            onChange={qs => handleChange('questions', qs)}
          />
        )}
        {activeTab === 1 && (
          <MappingsTab
            mappings={survey.mappings || []}
            questions={survey.questions || []}
            onChange={ms => handleChange('mappings', ms)}
          />
        )}
        {activeTab === 2 && (
          <RulesTab
            rules={survey.rules || []}
            onChange={rs => handleChange('rules', rs)}
          />
        )}
        {activeTab === 3 && (
          <ResponsesTab surveyId={id} />
        )}
        {activeTab === 4 && (
          <ResultsTab surveyId={id} />
        )}
      </div>

      {hostPanelOpen && (
        <HostRoomPanel surveyId={id} onClose={() => setHostPanelOpen(false)} />
      )}
    </div>
  );
}
