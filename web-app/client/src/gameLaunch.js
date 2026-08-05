// Single source of truth for the web-app -> Unity build launch links. The build is served
// gated at /game/; you never link there directly. Every launch goes through the web-app
// access gateway /api/game/enter, which verifies the caller, sets the HttpOnly game-access
// cookie nginx checks, then 302-redirects into /game/#… (where Unity reads role/token/room).

// Build the professor host-launch URL: the gateway with role + host token + survey. The
// gateway re-verifies the token before minting the cookie, and forwards the token into the
// final /game/ hash so Unity can create the room. The token rides a query to the trusted
// web-app only (over TLS), never to nginx static / a CDN.
export function buildHostLaunchUrl(token, surveyId) {
  const params = new URLSearchParams({ role: 'host', token, survey: String(surveyId) });
  return `/api/game/enter?${params.toString()}`;
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

// Build the shareable survey-response URL for a survey's share code. The survey app is now the
// site root, so this points at its HashRouter route (/#/s/CODE) on the current origin. Single
// source of truth for the share link used by SharePanel and the dashboard card (both direct
// copy and readonly input).
export function buildShareUrl(shareCode) {
  return `${window.location.origin}/#/s/${shareCode}`;
}
