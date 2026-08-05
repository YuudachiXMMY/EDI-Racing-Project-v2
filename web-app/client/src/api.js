const TOKEN_KEY = 'edi-survey-token';

function getToken() {
  return localStorage.getItem(TOKEN_KEY);
}

function setToken(token) {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY);
}

export function isAuthenticated() {
  return !!getToken();
}

async function request(path, options = {}) {
  const token = getToken();
  const headers = { 'Content-Type': 'application/json', ...options.headers };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  let res;
  try {
    res = await fetch(`/api${path}`, { ...options, headers });
  } catch {
    // Network failure / server unreachable — return the standard result contract so callers
    // (e.g. handleHostGame) can surface an error instead of an unhandled promise rejection.
    return { success: false, error: 'Network error — could not reach the server.' };
  }

  if (res.status === 401) {
    clearToken();
    window.location.hash = '#/login';
  }

  // A proxy 502/504 or other non-JSON body would make res.json() throw; degrade gracefully
  // rather than reject, keeping the { success, error } shape the rest of the app expects.
  try {
    return await res.json();
  } catch {
    return { success: false, error: `Server returned an unexpected response (HTTP ${res.status}).` };
  }
}

export async function login(email, password) {
  const result = await request('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password })
  });
  if (result.success) setToken(result.data.token);
  return result;
}

export async function register(email, password, displayName) {
  const result = await request('/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password, displayName })
  });
  if (result.success) setToken(result.data.token);
  return result;
}

export async function logout() {
  await request('/auth/logout', { method: 'POST' });
  clearToken();
}

export async function getSurveys() {
  return request('/surveys');
}

export async function getSurvey(id) {
  return request(`/surveys/${id}`);
}

export async function createSurvey(data) {
  return request('/surveys', {
    method: 'POST',
    body: JSON.stringify(data)
  });
}

export async function updateSurvey(id, data) {
  return request(`/surveys/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
}

export async function deleteSurvey(id) {
  return request(`/surveys/${id}`, { method: 'DELETE' });
}

export async function duplicateSurvey(id) {
  return request(`/surveys/${id}/duplicate`, { method: 'POST' });
}

export async function getTemplates() {
  return request('/templates');
}

export async function getResponseCount(id) {
  return request(`/surveys/${id}/responses/count`);
}

export async function getResponses(id) {
  return request(`/surveys/${id}/responses`);
}

export async function exportExcel(id) {
  const token = getToken();
  const res = await fetch(`/api/surveys/${id}/export-excel`, {
    headers: token ? { 'Authorization': `Bearer ${token}` } : {},
  });
  if (!res.ok) return { success: false, error: 'Export failed' };
  return { success: true, blob: await res.blob(), filename: res.headers.get('Content-Disposition')?.split('filename=')[1]?.replace(/"/g, '') || 'export.xlsx' };
}

export async function exportCsv(id) {
  const token = getToken();
  const res = await fetch(`/api/surveys/${id}/export-csv`, {
    headers: token ? { 'Authorization': `Bearer ${token}` } : {},
  });
  if (!res.ok) return { success: false, error: 'Export failed' };
  return { success: true, blob: await res.blob(), filename: 'vehicleGroupData.csv' };
}

// Mint a short-lived host token for launching the game in professor host mode (Phase 2).
// Backend: POST /api/game/host-token (requireAuth) -> { success, data: { token, expiresAt } }.
export async function requestHostToken(surveyId) {
  return request('/game/host-token', {
    method: 'POST',
    body: JSON.stringify({ surveyId }),
  });
}

// Race results read path — populates the Results tab. The write path (saveRaceResults) was
// coupled to the removed Send-to-Game modal; a host-mode results hook can be added later.
export async function getRaceResults(surveyId) {
  return request(`/surveys/${surveyId}/results`);
}

export async function getSessionHistory() {
  return request('/sessions');
}

export async function toggleSurveyActive(id, isActive) {
  return request(`/surveys/${id}/active`, {
    method: 'PATCH',
    body: JSON.stringify({ isActive })
  });
}

// Public endpoints (no auth) — use fetch directly to avoid 401 redirect
export async function getPublicSurvey(shareCode) {
  const res = await fetch(`/api/s/${shareCode}`);
  return res.json();
}

export async function submitResponse(shareCode, { email, teamName, answers }) {
  const res = await fetch(`/api/s/${shareCode}/respond`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, teamName, answers }),
  });
  return res.json();
}
