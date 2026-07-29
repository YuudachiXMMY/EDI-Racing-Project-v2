// Single source of truth for the Unity WebGL game root. Same-origin root by default
// (single-origin deploy: game at /, survey app at /survey/). Override with VITE_GAME_URL.
const GAME_ROOT = import.meta.env?.VITE_GAME_URL || '/';

// Build the professor host-launch URL. Token + survey ride in the hash fragment so they
// are never sent to the server, never logged by nginx/Traefik/Caddy, and never CDN-cached.
export function buildHostLaunchUrl(token, surveyId) {
  const params = new URLSearchParams({ role: 'host', token, survey: String(surveyId) });
  return `${GAME_ROOT}#${params.toString()}`;
}

// Build the student 3D-join URL. Carries the room code and role=play only — NEVER a host
// token — so opening it can join/watch but cannot create a room or trigger events. The hash
// (client-only) keeps it out of server/CDN logs, consistent with the host-launch URL.
export function buildStudentPlayUrl(roomCode) {
  const params = new URLSearchParams({ room: String(roomCode), role: 'play' });
  return `${GAME_ROOT}#${params.toString()}`;
}
