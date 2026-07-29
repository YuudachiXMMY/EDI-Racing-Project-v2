import { useParams, Link } from 'react-router-dom';
import { buildStudentPlayUrl, buildSpectatorPath } from '../gameLaunch.js';

// Public landing page (no auth) reached via the professor-shared student link
// /survey/#/join/:roomCode. Offers the two audience paths; neither exposes host controls.
export default function JoinLandingPage() {
  const { roomCode } = useParams();
  return (
    <div className="join-landing">
      <h1>加入直播赛事</h1>
      <p className="join-room-code">房间号 {roomCode?.toUpperCase()}</p>
      <div className="join-choices">
        {/* 3D: leaves the survey app for the Unity game root. Phase 5 makes Unity
            auto-join from this hash and hide Host UI; for now it opens the game. */}
        <a className="btn-primary btn-choice" href={buildStudentPlayUrl(roomCode)}>
          <span className="join-choice-title">进入 3D 游戏</span>
          <span className="join-choice-sub">在浏览器中观看你队伍的赛车</span>
        </a>
        {/* 2D: stays inside the survey app (HashRouter) — existing spectator view. */}
        <Link className="btn-primary btn-choice" to={buildSpectatorPath(roomCode)}>
          <span className="join-choice-title">2D 观战</span>
          <span className="join-choice-sub">排行榜 · 小地图 · 事件流</span>
        </Link>
      </div>
    </div>
  );
}
