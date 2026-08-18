import { useState, useRef, useEffect } from 'react';
import { SurveyCreatorComponent, SurveyCreator } from 'survey-creator-react';
import 'survey-core/survey-core.css';
import 'survey-creator-core/survey-creator-core.css';
import { unityQuestionsToSurveyJS, surveyJSToUnityQuestions } from '../surveyjs-config.js';

const creatorOptions = {
  questionTypes: ['text', 'radiogroup', 'checkbox'],
  showThemeTab: false,
  showLogicTab: false,
  showTranslationTab: false,
  showJSONEditorTab: false,
  showPreviewTab: true,
  autoSaveEnabled: true,
};

export default function QuestionsTab({ questions, onChange }) {
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  const [creator] = useState(() => {
    const c = new SurveyCreator(creatorOptions);

    // Add a custom "Numeric" toolbox item
    c.toolbox.addItem({
      name: 'numeric-range',
      title: 'Numeric Range',
      json: {
        type: 'text',
        inputType: 'number',
        title: 'Numeric question',
        min: 0,
        max: 10,
        isRequired: true,
      },
    });

    c.saveSurveyFunc = (saveNo, callback) => {
      const unityQuestions = surveyJSToUnityQuestions(c.JSON);
      onChangeRef.current(unityQuestions);
      callback(saveNo, true);
    };

    return c;
  });

  // Load initial questions into creator (once)
  const initializedRef = useRef(false);
  useEffect(() => {
    if (!initializedRef.current && questions) {
      creator.JSON = unityQuestionsToSurveyJS(questions);
      initializedRef.current = true;
    }
  }, [questions, creator]);

  return (
    <div className="questions-tab" style={{ height: 'calc(100vh - 140px)' }}>
      <SurveyCreatorComponent creator={creator} />
    </div>
  );
}
