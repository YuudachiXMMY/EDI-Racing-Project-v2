// Single source of truth for the web-app -> Unity build launch links. The build is served
// gated at /game/; you never link there directly. Every launch goes through the web-app
// access gateway /api/game/enter, which verifies the caller, sets the HttpOnly game-access
// cookie nginx checks, then 302-redirects into /game/#… (where Unity reads role/token/room).

// Describe the professor host-launch as a form POST (pure — no DOM). The host token is a
// create_room credential, so it must travel in a POST body, NEVER a query string: a GET query
// would be captured by nginx/edge access logs and browser history. The gateway re-verifies the
// token, mints the game-access cookie, then 302-redirects into /game/#… (token in the hash,
// which no server sees). See POST /api/game/enter.
export function hostLaunchFormSpec(token, surveyId) {
  return {
    action: '/api/game/enter',
    method: 'POST',
    fields: { role: 'host', token, survey: String(surveyId) },
  };
}

// Submit the host launch as a real form POST into an already-opened tab (or a new one when
// `gameTab` is null after a popup block). Kept thin so the URL-shape logic above stays pure
// and unit-testable; this is the only DOM glue.
//
// We write the form INTO the game tab's own document and self-submit it, rather than submitting
// from the opener with form.target=<window name>. Cross-window name retargeting is fragile: a
// popup whose opener link is severed (or one isolated by COOP) is no longer "familiar" to the
// opener, so the name lookup misses, the POST spawns a THIRD tab, and the opened tab is left
// stranded on about:blank — the "Host Game opens an empty tab" bug. Self-submitting inside the
// tab navigates that exact tab with no name/opener matching at all. The about:blank tab is
// same-origin with the opener, so its document is writable and the absolute same-origin action
// resolves correctly (a relative action would resolve against about:blank and break).
export function submitHostLaunch(token, surveyId, gameTab) {
  const spec = hostLaunchFormSpec(token, surveyId);
  // Absolute same-origin action: required when the form lives in the about:blank tab (its base
  // URI is about:blank); harmless in the opener-document fallback below.
  const action = `${window.location.origin}${spec.action}`;

  const buildForm = (doc, target) => {
    const form = doc.createElement('form');
    form.method = spec.method;
    form.action = action;
    if (target) form.target = target;
    for (const [name, value] of Object.entries(spec.fields)) {
      const input = doc.createElement('input');
      input.type = 'hidden';
      input.name = name;
      input.value = value;
      form.appendChild(input);
    }
    doc.body.appendChild(form);
    return form;
  };

  // Preferred path: submit from within the already-open game tab (self-navigation).
  const tabDoc = gameTab && gameTab.document;
  if (tabDoc && tabDoc.body) {
    buildForm(tabDoc, null).submit();
    return;
  }

  // Fallback (popup blocked → gameTab null, or its document is unreachable): submit from the
  // current document into a fresh _blank tab.
  const form = buildForm(document, '_blank');
  form.submit();
  form.remove();
}

// Build the student/audience 3D-join URL: the gateway with role=play + room only — NEVER a
// host token — so it can join/watch but cannot create a room or trigger events. The gateway
// admits only live rooms before minting the cookie.
export function buildStudentPlayUrl(roomCode) {
  const params = new URLSearchParams({ role: 'play', room: String(roomCode) });
  return `/api/game/enter?${params.toString()}`;
}

// Build the in-app path to the 2D spectator dashboard for a room. Returns a HashRouter-relative
// path ("/live/CODE") — NOT a game-root hash URL — because the 2D view lives inside the survey
// app (see App.jsx route /live/:roomCode). Upper-cases the code so the URL bar, the on-page
// "Room …" label, and the server's web_join_room (which upper-cases too) all agree. Carries no
// token and grants no host authority: a spectator can watch but never create a room or trigger.
export function buildSpectatorPath(roomCode) {
  return `/live/${String(roomCode).toUpperCase()}`;
}

// Build the public student join link for a live room: an absolute URL to the survey app's
// no-auth landing route (/#/join/CODE, JoinLandingPage), where students pick 3D game or 2D
// spectate. This is what the professor shares and what the QR code encodes after Host Game.
// Absolute (origin-qualified) and upper-cased so it scans/pastes cleanly off any device.
export function buildJoinLandingUrl(roomCode) {
  return `${window.location.origin}/#/join/${String(roomCode).toUpperCase()}`;
}

// Build the shareable survey-response URL for a survey's share code. The survey app is now the
// site root, so this points at its HashRouter route (/#/s/CODE) on the current origin. Single
// source of truth for the share link used by SharePanel and the dashboard card (both direct
// copy and readonly input).
export function buildShareUrl(shareCode) {
  return `${window.location.origin}/#/s/${shareCode}`;
}
