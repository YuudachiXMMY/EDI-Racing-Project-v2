import { useState, useEffect, useRef } from 'react';
import { useParams } from 'react-router-dom';
import { Model } from 'survey-core';
import { Survey } from 'survey-react-ui';
import 'survey-core/survey-core.css';
import { unityQuestionsToSurveyJS } from '../surveyjs-config.js';
import { getPublicSurvey, submitResponse } from '../api.js';

export default function StudentSurveyPage() {
  const { shareCode } = useParams();
  const [survey, setSurvey] = useState(null);
  const [status, setStatus] = useState('loading'); // loading | ready | submitting | submitted | error
  const [error, setError] = useState('');
  const [email, setEmail] = useState('');
  const [teamName, setTeamName] = useState('');
  const [formError, setFormError] = useState('');
  const modelRef = useRef(null);
  const emailRef = useRef('');
  const teamNameRef = useRef('');

  useEffect(() => {
    loadSurvey();
  }, [shareCode]);

  async function loadSurvey() {
    setStatus('loading');
    const result = await getPublicSurvey(shareCode);
    if (!result.success) {
      setError(result.error || 'Survey not found');
      setStatus('error');
      return;
    }
    setSurvey(result.data);

    const surveyJSON = unityQuestionsToSurveyJS(result.data.questions || []);
    const model = new Model(surveyJSON);
    model.showCompletedPage = false;
    model.completeText = 'Submit Survey';
    model.onComplete.add((sender) => {
      handleSubmit(sender.data);
    });
    modelRef.current = model;
    setStatus('ready');
  }

  async function handleSubmit(answers) {
    setFormError('');
    const currentEmail = emailRef.current;
    const currentTeamName = teamNameRef.current;
    if (!currentEmail.trim()) {
      setFormError('Please enter your email');
      modelRef.current?.clear(false, true);
      return;
    }
    if (!currentTeamName.trim()) {
      setFormError('Please enter your team name');
      modelRef.current?.clear(false, true);
      return;
    }

    setStatus('submitting');
    const result = await submitResponse(shareCode, {
      email: currentEmail.trim(),
      teamName: currentTeamName.trim(),
      answers,
    });

    if (result.success) {
      setStatus('submitted');
    } else {
      setFormError(result.error || 'Submission failed');
      setStatus('ready');
      modelRef.current?.clear(false, true);
    }
  }

  if (status === 'loading') {
    return <div className="student-page"><p className="loading">Loading survey...</p></div>;
  }

  if (status === 'error') {
    return (
      <div className="student-page">
        <div className="student-card">
          <h1>EDI Survey</h1>
          <p className="student-error">{error}</p>
        </div>
      </div>
    );
  }

  if (status === 'submitted') {
    return (
      <div className="student-page">
        <div className="student-card">
          <div className="student-confirmation">
            <h2>Response Submitted!</h2>
            <p>Thank you, <strong>{teamName}</strong>.</p>
            <p>Your responses have been recorded. Your team car will be ready for the race.</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="student-page">
      <div className="student-card">
        <h1>{survey?.configName || 'EDI Survey'}</h1>
        {survey?.description && <p className="student-desc">{survey.description}</p>}

        <div className="student-form">
          <input
            type="email"
            placeholder="Your email"
            value={email}
            onChange={e => { setEmail(e.target.value); emailRef.current = e.target.value; }}
            required
          />
          <input
            type="text"
            placeholder="Team name"
            value={teamName}
            onChange={e => { setTeamName(e.target.value); teamNameRef.current = e.target.value; }}
            required
          />
        </div>

        {formError && <p className="error">{formError}</p>}

        {modelRef.current && (
          <div className="student-survey">
            <Survey model={modelRef.current} />
          </div>
        )}

        {status === 'submitting' && <p className="loading">Submitting...</p>}
      </div>
    </div>
  );
}
