import { useParams, Link } from 'react-router-dom';
import { buildStudentPlayUrl, buildSpectatorPath } from '../gameLaunch.js';

// Public landing page (no auth) reached via the professor-shared student link
// /survey/#/join/:roomCode. Offers the two audience paths; neither exposes host controls.
export default function JoinLandingPage() {
  const { roomCode } = useParams();
  return (
    <div className="join-landing">
      <h1>Join Live Race</h1>
      <p className="join-room-code">Room Code {roomCode?.toUpperCase()}</p>
      <div className="join-choices">
        {/* 3D: leaves the survey app for the Unity game root. Phase 5 makes Unity
            auto-join from this hash and hide Host UI; for now it opens the game. */}
        <a className="btn-primary btn-choice" href={buildStudentPlayUrl(roomCode)}>
          <span className="join-choice-title">Enter 3D Game</span>
          <span className="join-choice-sub">Watch your team's car in the browser</span>
        </a>
        {/* 2D: stays inside the survey app (HashRouter) — existing spectator view. */}
        <Link className="btn-primary btn-choice" to={buildSpectatorPath(roomCode)}>
          <span className="join-choice-title">2D Spectate</span>
          <span className="join-choice-sub">Leaderboard · Minimap · Event Feed</span>
        </Link>
      </div>
    </div>
  );
}
