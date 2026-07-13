import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getSurvey, updateSurvey } from '../api.js';
import QuestionsTab from '../components/QuestionsTab.jsx';
import MappingsTab from '../components/MappingsTab.jsx';
import RulesTab from '../components/RulesTab.jsx';

const TABS = ['Questions', 'Mappings', 'Rules'];
const SAVE_DELAY = 2000;

export default function EditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [survey, setSurvey] = useState(null);
  const [activeTab, setActiveTab] = useState(0);
  const [saveStatus, setSaveStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const saveTimer = useRef(null);
  const latestData = useRef(null);

  useEffect(() => {
    loadSurvey();
    return () => { if (saveTimer.current) clearTimeout(saveTimer.current); };
  }, [id]);

  async function loadSurvey() {
    setLoading(true);
    const result = await getSurvey(id);
    if (result.success) {
      setSurvey(result.data);
      latestData.current = result.data;
    }
    setLoading(false);
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
      </header>

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
      </div>
    </div>
  );
}
