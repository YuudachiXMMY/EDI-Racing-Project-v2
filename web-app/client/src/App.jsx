import { HashRouter, Routes, Route, Navigate } from 'react-router-dom';
import { isAuthenticated } from './api.js';
import LoginPage from './pages/LoginPage.jsx';
import DashboardPage from './pages/DashboardPage.jsx';
import EditorPage from './pages/EditorPage.jsx';
import StudentSurveyPage from './pages/StudentSurveyPage.jsx';
import LiveRacePage from './pages/LiveRacePage.jsx';
import JoinLandingPage from './pages/JoinLandingPage.jsx';
import HistoryPage from './pages/HistoryPage.jsx';

function ProtectedRoute({ children }) {
  return isAuthenticated() ? children : <Navigate to="/login" replace />;
}

export default function App() {
  return (
    <HashRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
        <Route path="/surveys/:id" element={<ProtectedRoute><EditorPage /></ProtectedRoute>} />
        <Route path="/history" element={<ProtectedRoute><HistoryPage /></ProtectedRoute>} />
        <Route path="/s/:shareCode" element={<StudentSurveyPage />} />
        <Route path="/live/:roomCode" element={<LiveRacePage />} />
        <Route path="/join/:roomCode" element={<JoinLandingPage />} />
        <Route path="*" element={<Navigate to={isAuthenticated() ? "/dashboard" : "/login"} replace />} />
      </Routes>
    </HashRouter>
  );
}
