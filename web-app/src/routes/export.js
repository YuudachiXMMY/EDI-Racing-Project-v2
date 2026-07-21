import { Router } from 'express';
import { randomBytes } from 'crypto';
import WebSocket from 'ws';
import { getDb } from '../db.js';
import { requireAuth } from '../middleware/auth.js';

const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';

const router = Router();

/**
 * Apply a single AttributeMapping to a response value.
 * Mirrors Unity SurveyResponseMapper.ApplyTransform() exactly.
 */
function applyTransform(responseValue, mapping) {
  const transformType = (mapping.TransformType || 'direct').toLowerCase();

  switch (transformType) {
    case 'lookup': {
      const entries = mapping.LookupEntries || [];
      for (const entry of entries) {
        if (entry.Key && entry.Key.toLowerCase() === (responseValue || '').toLowerCase()) {
          return entry.Value;
        }
      }
      return mapping.DefaultValue || responseValue;
    }
    case 'numeric': {
      const num = parseFloat(responseValue);
      if (!isNaN(num)) return responseValue;
      return mapping.DefaultValue || '0';
    }
    case 'direct':
    default:
      return responseValue;
  }
}

/**
 * Map survey responses to CarData attributes using AttributeMappings.
 * Mirrors Unity SurveyResponseMapper.MapResponses() exactly.
 *
 * @param {string} teamName
 * @param {Object} answers - { questionId: answerValue } from responses table
 * @param {Array} mappings - AttributeMapping[] from survey config
 * @returns {{ teamName: string, attributes: Array<{key: string, value: string}> }}
 */
function mapResponsesToCarData(teamName, answers, mappings) {
  if (!mappings || mappings.length === 0) {
    return { teamName, attributes: [] };
  }

  const attributes = [];

  for (const mapping of mappings) {
    // Case-insensitive questionId lookup in answers object
    let responseValue = null;
    if (answers && mapping.QuestionId) {
      const targetId = mapping.QuestionId.toLowerCase();
      for (const key of Object.keys(answers)) {
        if (key.toLowerCase() === targetId) {
          responseValue = String(answers[key]);
          break;
        }
      }
    }

    let attributeValue;
    if (responseValue === null) {
      attributeValue = mapping.DefaultValue || '';
    } else {
      attributeValue = applyTransform(responseValue, mapping);
    }

    attributes.push({
      key: mapping.AttributeName,
      value: attributeValue,
    });
  }

  return { teamName, attributes };
}

// GET /api/surveys/:id/export — export survey data for Unity
router.get('/:id/export', requireAuth, (req, res) => {
  const db = getDb();
  const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }

  const mappings = JSON.parse(survey.mappings_json);
  const eventRules = JSON.parse(survey.rules_json);

  // Fetch all responses for this survey
  const responses = db.prepare(
    'SELECT team_name, answers_json FROM responses WHERE survey_id = ? ORDER BY submitted_at ASC'
  ).all(survey.id);

  // Map each response to CarData using attribute mappings
  const carData = responses.map(r => {
    const answers = JSON.parse(r.answers_json);
    return mapResponsesToCarData(r.team_name, answers, mappings);
  });

  const exportData = {
    configName: survey.config_name,
    carData,
    mappings,
    eventRules,
  };

  res.json({ success: true, data: exportData });
});

// POST /api/surveys/:id/send-to-game — send survey data directly to Unity via WebSocket
router.post('/:id/send-to-game', requireAuth, (req, res) => {
  const { roomCode } = req.body;
  if (!roomCode || !roomCode.trim()) {
    return res.status(400).json({ success: false, error: 'roomCode is required' });
  }

  const db = getDb();
  const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }

  const mappings = JSON.parse(survey.mappings_json);
  const eventRules = JSON.parse(survey.rules_json);

  const responses = db.prepare(
    'SELECT team_name, answers_json FROM responses WHERE survey_id = ? ORDER BY submitted_at ASC'
  ).all(survey.id);

  const carData = responses.map(r => {
    const answers = JSON.parse(r.answers_json);
    return mapResponsesToCarData(r.team_name, answers, mappings);
  });

  if (carData.length === 0) {
    return res.status(400).json({ success: false, error: 'No responses to send. Share the survey with students first.' });
  }

  const exportPayload = {
    configName: survey.config_name,
    carData,
    mappings,
    eventRules,
  };

  const exportJson = JSON.stringify(exportPayload);
  const code = roomCode.trim().toUpperCase();

  const ws = new WebSocket(WS_GAME_URL);
  let responded = false;

  const timeout = setTimeout(() => {
    if (!responded) {
      responded = true;
      ws.close();
      res.status(504).json({ success: false, error: 'Game server did not respond in time' });
    }
  }, 5000);

  ws.on('open', () => {
    ws.send(JSON.stringify({ type: 'web_join_room', roomCode: code }));
  });

  ws.on('message', (data) => {
    if (responded) return;
    let msg;
    try {
      msg = JSON.parse(data.toString());
    } catch {
      return;
    }

    if (msg.type === 'error') {
      responded = true;
      clearTimeout(timeout);
      ws.close();
      return res.status(400).json({ success: false, error: msg.message || 'Room not found' });
    }

    if (msg.type === 'room_joined') {
      ws.send(JSON.stringify({ type: 'survey_import', configName: survey.config_name, exportJson }));
    }

    if (msg.type === 'survey_import_ack') {
      responded = true;
      clearTimeout(timeout);
      ws.close();
      return res.json({ success: true, data: { carsCount: carData.length, rulesCount: eventRules.length } });
    }
  });

  ws.on('error', () => {
    if (!responded) {
      responded = true;
      clearTimeout(timeout);
      res.status(502).json({ success: false, error: 'Cannot connect to game server' });
    }
  });
});

// POST /api/surveys/import-config — import a SurveyConfig from Unity format
router.post('/import-config', requireAuth, (req, res) => {
  const { configName, configJson } = req.body;
  if (!configJson) {
    return res.status(400).json({ success: false, error: 'configJson is required' });
  }

  let config;
  try {
    config = JSON.parse(configJson);
  } catch {
    return res.status(400).json({ success: false, error: 'Invalid config JSON' });
  }

  const name = configName || config.ConfigName || 'Imported Config';
  const description = config.Description || '';
  const questions = config.Questions || [];
  const mappings = config.Mappings || [];
  const rules = config.Rules || [];

  const db = getDb();
  const shareCode = randomBytes(4).toString('hex').toUpperCase();

  const result = db.prepare(
    `INSERT INTO surveys (user_id, config_name, description, questions_json, mappings_json, rules_json, share_code)
     VALUES (?, ?, ?, ?, ?, ?, ?)`
  ).run(
    req.user.userId,
    name,
    description,
    JSON.stringify(questions),
    JSON.stringify(mappings),
    JSON.stringify(rules),
    shareCode
  );

  res.status(201).json({
    success: true,
    data: { id: result.lastInsertRowid, configName: name, shareCode }
  });
});

// POST /api/surveys/:id/send-config-to-game — send raw survey config to Unity
router.post('/:id/send-config-to-game', requireAuth, (req, res) => {
  const { roomCode } = req.body;
  if (!roomCode || !roomCode.trim()) {
    return res.status(400).json({ success: false, error: 'roomCode is required' });
  }

  const db = getDb();
  const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }

  // Build Unity-compatible SurveyConfig (PascalCase fields for JsonUtility)
  const configPayload = {
    ConfigName: survey.config_name,
    Description: survey.description || '',
    CreatedAt: survey.created_at,
    Version: '1.0',
    Questions: JSON.parse(survey.questions_json),
    Mappings: JSON.parse(survey.mappings_json),
    Rules: JSON.parse(survey.rules_json),
  };

  const code = roomCode.trim().toUpperCase();
  const ws = new WebSocket(WS_GAME_URL);
  let responded = false;

  const timeout = setTimeout(() => {
    if (!responded) {
      responded = true;
      ws.close();
      res.status(504).json({ success: false, error: 'Game server did not respond in time' });
    }
  }, 5000);

  ws.on('open', () => {
    ws.send(JSON.stringify({ type: 'web_join_room', roomCode: code }));
  });

  ws.on('message', (data) => {
    if (responded) return;
    let msg;
    try {
      msg = JSON.parse(data.toString());
    } catch {
      return;
    }

    if (msg.type === 'error') {
      responded = true;
      clearTimeout(timeout);
      ws.close();
      return res.status(400).json({ success: false, error: msg.message || 'Room not found' });
    }

    if (msg.type === 'room_joined') {
      ws.send(JSON.stringify({
        type: 'config_import',
        configName: survey.config_name,
        configJson: JSON.stringify(configPayload),
      }));
    }

    if (msg.type === 'config_sync_ack') {
      responded = true;
      clearTimeout(timeout);
      ws.close();
      if (msg.success) {
        return res.json({ success: true, data: { configName: survey.config_name } });
      }
      return res.status(400).json({ success: false, error: msg.error || 'Config sync failed' });
    }
  });

  ws.on('error', () => {
    if (!responded) {
      responded = true;
      clearTimeout(timeout);
      res.status(502).json({ success: false, error: 'Cannot connect to game server' });
    }
  });
});

export default router;
