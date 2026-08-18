import { QuestionType } from './constants.js';

/**
 * Convert Unity SurveyQuestion[] to SurveyJS JSON schema.
 * SurveyJS uses pages/elements structure; we put all questions on one page.
 */
export function unityQuestionsToSurveyJS(questions) {
  if (!questions || questions.length === 0) {
    return { pages: [{ name: 'page1', elements: [] }] };
  }

  const elements = questions.map(q => {
    const base = {
      name: q.Id,
      title: q.Text || '',
      isRequired: !!q.Required
    };

    switch (q.Type) {
      case QuestionType.MultipleChoice:
        return { ...base, type: 'radiogroup', choices: q.Options || [] };
      case QuestionType.MultiSelect:
        return { ...base, type: 'checkbox', choices: q.Options || [], maxSelectedChoices: q.MaxValue || undefined };
      case QuestionType.Numeric:
        return { ...base, type: 'text', inputType: 'number', min: q.MinValue ?? 0, max: q.MaxValue ?? 10 };
      case QuestionType.Text:
      default:
        return { ...base, type: 'text' };
    }
  });

  return { pages: [{ name: 'page1', elements }] };
}

/**
 * Convert SurveyJS JSON schema back to Unity SurveyQuestion[].
 * Flattens all pages/elements into a single array.
 */
export function surveyJSToUnityQuestions(surveyJSON) {
  if (!surveyJSON) return [];

  const pages = surveyJSON.pages || [];
  const elements = pages.flatMap(p => p.elements || []);

  return elements.map(el => {
    const question = {
      Id: el.name || '',
      Text: el.title || '',
      Type: QuestionType.Text,
      Options: [],
      MinValue: 0,
      MaxValue: 10,
      Required: !!el.isRequired
    };

    if (el.type === 'checkbox') {
      question.Type = QuestionType.MultiSelect;
      question.Options = (el.choices || []).map(c =>
        typeof c === 'string' ? c : (c.text || c.value || '')
      );
      if (el.maxSelectedChoices) question.MaxValue = el.maxSelectedChoices;
    } else if (el.type === 'radiogroup' || el.type === 'dropdown') {
      question.Type = QuestionType.MultipleChoice;
      question.Options = (el.choices || []).map(c =>
        typeof c === 'string' ? c : (c.text || c.value || '')
      );
    } else if (el.type === 'text' && el.inputType === 'number') {
      question.Type = QuestionType.Numeric;
      question.MinValue = el.min ?? 0;
      question.MaxValue = el.max ?? 10;
    }
    // else: default Text

    return question;
  });
}
