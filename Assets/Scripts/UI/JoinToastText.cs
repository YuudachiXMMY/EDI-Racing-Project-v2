/// <summary>
/// Pure formatter for the bottom-left "student joined" toast. Kept free of MonoBehaviour/
/// UnityEngine so the wording is unit-testable in EditMode (mirrors <see cref="StudentJoinDecision"/>).
/// Never throws; a blank/whitespace team name (anonymous spectator) falls back to "A student".
/// </summary>
public static class JoinToastText
{
    public static string Format(string teamName, int count)
    {
        string who = string.IsNullOrWhiteSpace(teamName) ? "A student" : $"'{teamName.Trim()}'";
        // count is the cumulative room total the server sends with each join. A non-positive value
        // (unknown/first frame) drops the suffix rather than showing a misleading "(0 total)".
        return count > 0 ? $"{who} joined  ({count} total)" : $"{who} joined";
    }
}
