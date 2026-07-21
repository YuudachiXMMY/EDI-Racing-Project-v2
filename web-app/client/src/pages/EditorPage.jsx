import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getSurvey, updateSurvey, exportSurvey, getResponseCount, toggleSurveyActive, linkRoom, unlinkRoom } from '../api.js';
import QuestionsTab from '../components/QuestionsTab.jsx';
import MappingsTab from '../components/MappingsTab.jsx';
import RulesTab from '../components/RulesTab.jsx';
import ResponsesTab from '../components/ResponsesTab.jsx';
import ResultsTab from '../components/ResultsTab.jsx';
import SharePanel from '../components/SharePanel.jsx';
import SendToGameModal from '../components/SendToGameModal.jsx';

const TABS = ['Questions', 'Mappings', 'Rules', 'Responses', 'Results'];
const SAVE_DELAY = 2000;

export default function EditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [survey, setSurvey] = useState(null);
  const [activeTab, setActiveTab] = useState(0);
  const [saveStatus, setSaveStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [exportData, setExportData] = useState(null);
  const [exportLoading, setExportLoading] = useState(false);
  const [responseCount, setResponseCount] = useState(0);
  const [showSendModal, setShowSendModal] = useState(false);
  const [linkedRoom, setLinkedRoom] = useState(null);
  const [linkInput, setLinkInput] = useState('');
  const [linkStatus, setLinkStatus] = useState('');
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
    if (surveyRes.success && surveyRes.data.linked_room_code) {
      setLinkedRoom(surveyRes.data.linked_room_code);
    }
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

  async function handleLinkRoom() {
    const code = linkInput.trim().toUpperCase();
    if (!code) return;
    setLinkStatus('Linking...');
    const result = await linkRoom(id, code);
    if (result.success) {
      setLinkedRoom(result.data.linkedRoomCode);
      setLinkInput('');
      setLinkStatus('');
    } else {
      setLinkStatus(result.error || 'Failed to link');
    }
  }

  async function handleUnlinkRoom() {
    const result = await unlinkRoom(id);
    if (result.success) {
      setLinkedRoom(null);
      setLinkStatus('');
    }
  }

  async function handleExport() {
    setExportLoading(true);
    const result = await exportSurvey(id);
    setExportLoading(false);
    if (result.success) {
      setExportData(result.data);
    }
  }

  function downloadExportJson() {
    if (!exportData) return;
    const json = JSON.stringify(exportData, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    const safeName = (exportData.configName || 'export').replace(/[^a-zA-Z0-9_-]/g, '_');
    a.href = url;
    a.download = `${safeName}-export.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  function copyExportJson() {
    if (!exportData) return;
    navigator.clipboard.writeText(JSON.stringify(exportData, null, 2));
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
        <button onClick={() => navigate('/dashboard')} className="btn-secondary">← Dashboard</button>
        <input
          type="text"
          className="survey-name-input"
          value={survey.config_name}
          onChange={handleNameChange}
          placeholder="Survey name"
        />
        <span className="save-status">{saveStatus}</span>
        <span className="response-count">{responseCount} response(s)</span>

        {linkedRoom ? (
          <span className="room-link-badge">
            Linked: {linkedRoom}
            <button onClick={handleUnlinkRoom} className="btn-secondary btn-small">Unlink</button>
          </span>
        ) : (
          <span className="room-link-inline">
            <input
              type="text"
              value={linkInput}
              onChange={e => setLinkInput(e.target.value.toUpperCase())}
              placeholder="Room code"
              maxLength={8}
              className="room-link-input"
            />
            <button onClick={handleLinkRoom} className="btn-secondary btn-small" disabled={!linkInput.trim()}>
              Link
            </button>
            {linkStatus && <span className="link-status">{linkStatus}</span>}
          </span>
        )}

        <button onClick={() => setShowSendModal(true)} className="btn-primary" disabled={responseCount === 0}>
          Send to Game
        </button>
        <button onClick={handleExport} className="btn-primary" disabled={exportLoading}>
          {exportLoading ? 'Exporting...' : 'Export for Unity'}
        </button>
      </header>

      <SharePanel
        shareCode={survey.share_code}
        isActive={!!survey.is_active}
        onToggleActive={handleToggleActive}
      />

      {exportData && (
        <div className="export-panel">
          <div className="export-header">
            <h3>Export: {exportData.configName}</h3>
            <span>{exportData.carData.length} car(s), {exportData.eventRules.length} rule(s)</span>
            <button onClick={() => setExportData(null)} className="btn-secondary btn-small">Close</button>
          </div>
          {exportData.carData.length === 0 && (
            <p className="export-warning">No responses yet. Share the survey link with students first.</p>
          )}
          <div className="export-actions">
            <button onClick={downloadExportJson} className="btn-primary">Download JSON</button>
            <button onClick={copyExportJson} className="btn-secondary">Copy to Clipboard</button>
          </div>
          <textarea
            className="export-preview"
            readOnly
            value={JSON.stringify(exportData, null, 2)}
            rows={12}
          />
        </div>
      )}

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

      {showSendModal && <SendToGameModal surveyId={id} onClose={() => setShowSendModal(false)} />}
    </div>
  );
}
