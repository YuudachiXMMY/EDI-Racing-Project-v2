/**
 * Default survey templates matching Unity SurveyTemplates.cs.
 * Seeded into the templates table on first startup.
 */

const templates = [
  {
    name: 'ENGG*1100 Survey',
    description: 'Replicates the original ENGG*1100 MS Forms questionnaire. Collects team data, computes average-threshold tags (DataTool.py algorithm), and exports as Excel or game CSV.',
    questions: [
      { Id: 'team_name', Text: 'Name your autonomous vehicle team.\n[Letters and numbers only, NO space] (e.g. Apollo3)', Type: 0, Options: [], MinValue: 0, MaxValue: 0, Required: true },
      { Id: 'color', Text: 'Choose the colour for your autonomous vehicle.', Type: 1, Options: ['Blue', 'Red', 'Black', 'White', 'Green'], MinValue: 0, MaxValue: 0, Required: true },
      { Id: 'member_count', Text: 'How many members in the team?', Type: 2, Options: [], MinValue: 1, MaxValue: 20, Required: true },
      { Id: 'member_names', Text: 'Please enter the first names of the members of your group, separated by commas. (e.g. Steve, Albert, Hussein, Wael). This information will only be used to distribute prizes after', Type: 0, Options: [], MinValue: 0, MaxValue: 0, Required: true },
      { Id: 'facial_count', Text: 'How many members in your team use a facial recognition function on their phones/PCs?', Type: 2, Options: [], MinValue: 0, MaxValue: 20, Required: true },
      { Id: 'glasses_count', Text: 'How many members in your team wear glasses or contact lenses?', Type: 2, Options: [], MinValue: 0, MaxValue: 20, Required: true },
      { Id: 'language_count', Text: 'How many different languages overall are spoken in your team?', Type: 2, Options: [], MinValue: 0, MaxValue: 50, Required: true },
      { Id: 'male_count', Text: 'How many members in the group identify themselves as male?', Type: 2, Options: [], MinValue: 0, MaxValue: 20, Required: true },
      { Id: 'pwd_count', Text: 'How many members in the team has their PPW* 5 characters or more?', Type: 2, Options: [], MinValue: 0, MaxValue: 20, Required: true },
      { Id: 'distance_km', Text: 'Consider all members, whose hometown (attended their high school) is the furthest from the University of Guelph? Enter the distance in kilometers below.', Type: 2, Options: [], MinValue: 0, MaxValue: 99999, Required: true },
      { Id: 'vehicle_type', Text: 'What type of vehicle would your team prefer to ride in?', Type: 1, Options: ['Convertible', 'Hatchback', 'Pickup truck', 'Sedan', 'SUV', 'Van'], MinValue: 0, MaxValue: 0, Required: true },
      { Id: 'entertainment', Text: 'What type of entertainment system do you utilize the most?', Type: 1, Options: ['Bluetooth', 'CD player', 'AUX connected devices', 'Apple CarPlay', 'Android AutoCar'], MinValue: 0, MaxValue: 0, Required: true },
      { Id: 'driving_experience', Text: 'What is the cumulative driving experience of your team (in years)?', Type: 2, Options: [], MinValue: 0, MaxValue: 200, Required: false },
      { Id: 'car_features', Text: 'Rank the following advanced in-car features in terms of importance to your team. (Select up to 3)', Type: 3, Options: ['Heads-up display', 'Automatic High Beams', 'Electronic Door Handles', 'Do-It-All Touchscreens', 'Camera Vision', 'Lane-Keep Assist', 'Full-self driving'], MinValue: 0, MaxValue: 3, Required: false },
    ],
    mappings: [
      { QuestionId: 'color', AttributeName: 'colorIndex', DefaultValue: '0', TransformType: 'lookup', LookupEntries: [{ Key: 'Green', Value: '0' }, { Key: 'Black', Value: '1' }, { Key: 'Red', Value: '2' }, { Key: 'Blue', Value: '3' }, { Key: 'White', Value: '4' }] },
      { QuestionId: 'facial_count', AttributeName: 'facial_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
      { QuestionId: 'glasses_count', AttributeName: 'glasses_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
      { QuestionId: 'language_count', AttributeName: 'language_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
      { QuestionId: 'male_count', AttributeName: 'male_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
      { QuestionId: 'pwd_count', AttributeName: 'pwd_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
      { QuestionId: 'distance_km', AttributeName: 'distance_km', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
    ],
    postProcessing: [
      { type: 'average_threshold', sourceAttribute: 'facial_count', direction: 'gte', tagName: 'facerecog', targetAttribute: 'functions' },
      { type: 'average_threshold', sourceAttribute: 'glasses_count', direction: 'gte', tagName: 'glasses', targetAttribute: 'functions' },
      { type: 'average_threshold', sourceAttribute: 'language_count', direction: 'gte', tagName: 'language', targetAttribute: 'functions' },
      { type: 'average_threshold', sourceAttribute: 'pwd_count', direction: 'lte', tagName: 'password', targetAttribute: 'functions' },
      { type: 'average_threshold', sourceAttribute: 'distance_km', direction: 'gte', tagName: 'distance', targetAttribute: 'functions' },
      { type: 'fixed_threshold', sourceAttribute: 'male_count', threshold: 2, direction: 'gt', tagName: 'male', targetAttribute: 'functions' },
    ],
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
];

export function seedTemplates(db) {
  const insert = db.prepare(
    'INSERT OR IGNORE INTO templates (name, description, questions_json, mappings_json, rules_json, post_processing_json) VALUES (?, ?, ?, ?, ?, ?)'
  );

  const insertMany = db.transaction((items) => {
    for (const t of items) {
      insert.run(
        t.name,
        t.description,
        JSON.stringify(t.questions),
        JSON.stringify(t.mappings),
        JSON.stringify(t.rules),
        JSON.stringify(t.postProcessing || [])
      );
    }
  });

  insertMany(templates);
}
