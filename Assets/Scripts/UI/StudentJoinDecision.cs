/// <summary>
/// Pure decision for whether a page load should auto-join a room as a student spectator
/// (Phase 5). Kept free of MonoBehaviour/UnityEngine so it is unit-testable in EditMode.
/// Never throws; missing/blank inputs yield <c>false</c>.
///
/// Auto-join applies only when the student 3D link declared the play role (role == "play",
/// as emitted by the web-app's <c>buildStudentPlayUrl</c>) AND carried a non-blank room code.
/// A host launch, or a play launch without a room code, must NOT auto-join. Note the role
/// value is "play" (not "student") to match the URL the web-app actually emits.
/// </summary>
public static class StudentJoinDecision
{
    public static bool ShouldAutoJoin(string role, string room)
    {
        return role == "play" && !string.IsNullOrWhiteSpace(room);
    }
}
