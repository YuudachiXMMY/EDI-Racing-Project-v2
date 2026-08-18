/// <summary>
/// Pure builder for the shareable student join link surfaced on the professor host screen.
/// Composes "{origin}/survey/#/join/{roomCode}" — the survey app's landing route (Phase 4).
/// The link carries only the room code — NEVER the host token — so a student who opens it
/// cannot create a room or trigger events. Kept free of UnityEngine so it is EditMode-testable.
/// Returns "" for empty origin (e.g. Editor) or empty room code, so callers can hide the UI.
/// </summary>
public static class StudentLinkBuilder
{
    public static string BuildJoinLink(string origin, string roomCode)
    {
        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(roomCode)) return "";
        string trimmed = origin.TrimEnd('/');
        return $"{trimmed}/survey/#/join/{roomCode}";
    }
}
