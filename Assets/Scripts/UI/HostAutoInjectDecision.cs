/// <summary>
/// Pure decision for whether a professor host launch should auto-inject the selected survey's
/// responses into the game (Phase 3). Kept free of MonoBehaviour/UnityEngine so it is
/// unit-testable in EditMode. Never throws; missing/blank inputs yield <c>false</c>.
///
/// Auto-inject applies only when the page was launched as host (role == "host") AND a non-blank
/// survey id was carried in the launch hash. A student/spectator launch, or a host launch without
/// a survey id (e.g. a plain in-game Host click), must NOT trigger an injection.
/// </summary>
public static class HostAutoInjectDecision
{
    public static bool ShouldAutoInject(string role, string surveyId)
    {
        return role == "host" && !string.IsNullOrWhiteSpace(surveyId);
    }
}
