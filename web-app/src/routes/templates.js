import { Router } from 'express';

const router = Router();

// Mirrors Unity SurveyTemplates.cs exactly
const templates = [
  {
    name: 'V1 Parity',
    description: 'Original ENGG*1100 configuration. Import data via CSV with columns: teamName, colorIndex, functions.',
    config: {
      questions: [],
      mappings: [],
      rules: [
        { DisplayName: 'Name Length Penalty', AttributeName: 'teamName', Operator: 6, CompareValue: '10', SpeedDelta: -10, Duration: 8, Weather: 0, AllowRepeat: false },
        { DisplayName: 'Color Boost (Blue)', AttributeName: 'colorIndex', Operator: 0, CompareValue: '3', SpeedDelta: 15, Duration: 6, Weather: 0, AllowRepeat: false },
        { DisplayName: 'Color Penalty (Red)', AttributeName: 'colorIndex', Operator: 0, CompareValue: '2', SpeedDelta: -12, Duration: 8, Weather: 0, AllowRepeat: false },
        { DisplayName: 'Function Boost (Password)', AttributeName: 'functions', Operator: 2, CompareValue: 'password', SpeedDelta: 10, Duration: 6, Weather: 0, AllowRepeat: false },
        { DisplayName: 'Function Penalty (Face Recog)', AttributeName: 'functions', Operator: 2, CompareValue: 'facerecog', SpeedDelta: -10, Duration: 8, Weather: 0, AllowRepeat: false },
        { DisplayName: 'Snow Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -8, Duration: 12, Weather: 1, AllowRepeat: true },
        { DisplayName: 'Night Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -5, Duration: 15, Weather: 2, AllowRepeat: true },
      ]
    }
  },
  {
    name: 'Accessibility',
    description: 'Demonstrates how disability status and assistive technology access affect outcomes.',
    config: {
      questions: [
        { Id: 'disability', Text: 'Do you have a disability that affects your daily activities?', Type: 1, Options: ['No', 'Yes - Physical', 'Yes - Cognitive', 'Yes - Sensory', 'Prefer not to say'], MinValue: 0, MaxValue: 0, Required: true },
        { Id: 'assistive_tech', Text: 'Do you use assistive technology (screen reader, hearing aid, mobility device, etc.)?', Type: 1, Options: ['No', 'Yes'], MinValue: 0, MaxValue: 0, Required: true },
        { Id: 'accommodation_ease', Text: 'How easy is it for you to get accommodations when needed? (1=very difficult, 10=very easy)', Type: 2, Options: [], MinValue: 1, MaxValue: 10, Required: true },
      ],
      mappings: [
        { QuestionId: 'disability', AttributeName: 'disability', DefaultValue: 'none', TransformType: 'lookup', LookupEntries: [{ Key: 'No', Value: 'none' }, { Key: 'Yes - Physical', Value: 'physical' }, { Key: 'Yes - Cognitive', Value: 'cognitive' }, { Key: 'Yes - Sensory', Value: 'sensory' }, { Key: 'Prefer not to say', Value: 'undisclosed' }] },
        { QuestionId: 'assistive_tech', AttributeName: 'assistive_tech', DefaultValue: 'no', TransformType: 'lookup', LookupEntries: [{ Key: 'No', Value: 'no' }, { Key: 'Yes', Value: 'yes' }] },
        { QuestionId: 'accommodation_ease', AttributeName: 'accommodation_ease', DefaultValue: '5', TransformType: 'numeric', LookupEntries: [] },
      ],
      rules: [
        { DisplayName: 'Inaccessible Building', AttributeName: 'disability', Operator: 1, CompareValue: 'none', SpeedDelta: -12, Duration: 8, Weather: 0, AllowRepeat: false },
        { DisplayName: 'Tech Upgrade', AttributeName: 'assistive_tech', Operator: 0, CompareValue: 'yes', SpeedDelta: 10, Duration: 6, Weather: 0, AllowRepeat: false },
        { DisplayName: 'Accommodation Barrier', AttributeName: 'accommodation_ease', Operator: 5, CompareValue: '5', SpeedDelta: -8, Duration: 10, Weather: 1, AllowRepeat: false },
      ]
    }
  },
  {
    name: 'Diversity',
    description: 'Demonstrates how identity factors (language, first-generation status) create systemic barriers.',
    config: {
      questions: [
        { Id: 'primary_language', Text: 'What is your primary language?', Type: 0, Options: [], MinValue: 0, MaxValue: 0, Required: true },
        { Id: 'first_gen', Text: 'Are you a first-generation university student?', Type: 1, Options: ['No', 'Yes'], MinValue: 0, MaxValue: 0, Required: true },
        { Id: 'work_hours', Text: 'How many hours per week do you work outside of school? (0-40)', Type: 2, Options: [], MinValue: 0, MaxValue: 40, Required: false },
      ],
      mappings: [
        { QuestionId: 'primary_language', AttributeName: 'language', DefaultValue: 'English', TransformType: 'direct', LookupEntries: [] },
        { QuestionId: 'first_gen', AttributeName: 'first_gen', DefaultValue: 'no', TransformType: 'lookup', LookupEntries: [{ Key: 'No', Value: 'no' }, { Key: 'Yes', Value: 'yes' }] },
        { QuestionId: 'work_hours', AttributeName: 'work_hours', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
      ],
      rules: [
        { DisplayName: 'Language Barrier', AttributeName: 'language', Operator: 1, CompareValue: 'English', SpeedDelta: -10, Duration: 8, Weather: 0, AllowRepeat: false },
        { DisplayName: 'First-Gen Headwind', AttributeName: 'first_gen', Operator: 0, CompareValue: 'yes', SpeedDelta: -8, Duration: 10, Weather: 1, AllowRepeat: false },
        { DisplayName: 'Work Fatigue', AttributeName: 'work_hours', Operator: 4, CompareValue: '20', SpeedDelta: -6, Duration: 12, Weather: 2, AllowRepeat: false },
        { DisplayName: 'Mentorship Program', AttributeName: 'first_gen', Operator: 0, CompareValue: 'yes', SpeedDelta: 12, Duration: 6, Weather: 0, AllowRepeat: false },
      ]
    }
  }
];

// GET /api/templates
router.get('/', (req, res) => {
  res.json({ success: true, data: templates });
});

export default router;
