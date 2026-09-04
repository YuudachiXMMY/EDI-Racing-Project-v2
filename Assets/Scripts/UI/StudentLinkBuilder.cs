using System.Globalization;

/// <summary>
/// Pure builder for the shareable student join link surfaced on the professor host screen.
/// Composes "{origin}/#/join/{ROOMCODE}" — the survey app's no-auth landing route (JoinLandingPage,
/// App.jsx route /join/:roomCode). The survey app is the site root, so there is NO "/survey/" path
/// prefix; the room code is upper-cased to match the web-app's buildJoinLandingUrl and the server's
/// room lookup (both upper-case). The link carries only the room code — NEVER the host token — so a
/// student who opens it cannot create a room or trigger events. Kept free of UnityEngine so it is
/// EditMode-testable. Returns "" for empty origin (e.g. Editor) or empty room code, so callers can
/// hide the UI.
/// </summary>
public static class StudentLinkBuilder
{
    public static string BuildJoinLink(string origin, string roomCode)
    {
        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(roomCode)) return "";
        string trimmed = origin.TrimEnd('/');
        return $"{trimmed}/#/join/{roomCode.ToUpper(CultureInfo.InvariantCulture)}";
    }
}
