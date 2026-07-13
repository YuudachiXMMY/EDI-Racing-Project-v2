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

  const res = await fetch(`/api${path}`, { ...options, headers });
  const json = await res.json();

  if (res.status === 401) {
    clearToken();
    window.location.hash = '#/login';
  }

  return json;
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

export async function getTemplates() {
  return request('/templates');
}

export async function getResponseCount(id) {
  return request(`/surveys/${id}/responses/count`);
}

export async function getResponses(id) {
  return request(`/surveys/${id}/responses`);
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
