using NUnit.Framework;

[TestFixture]
public class StudentJoinDecisionTests
{
    [Test]
    public void ShouldAutoJoin_PlayRoleWithRoom_ReturnsTrue()
    {
        // Arrange
        string role = "play";
        string room = "ABC123";

        // Act
        bool result = StudentJoinDecision.ShouldAutoJoin(role, room);

        // Assert
        Assert.IsTrue(result);
    }

    [Test]
    public void ShouldAutoJoin_EmptyRoom_ReturnsFalse()
    {
        bool result = StudentJoinDecision.ShouldAutoJoin("play", "");
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldAutoJoin_WhitespaceRoom_ReturnsFalse()
    {
        bool result = StudentJoinDecision.ShouldAutoJoin("play", "   ");
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldAutoJoin_NullRoom_ReturnsFalse()
    {
        bool result = StudentJoinDecision.ShouldAutoJoin("play", null);
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldAutoJoin_HostRole_ReturnsFalse()
    {
        // A host launch must never auto-join as a student, even with a room code.
        bool result = StudentJoinDecision.ShouldAutoJoin("host", "ABC123");
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldAutoJoin_EmptyRole_ReturnsFalse()
    {
        bool result = StudentJoinDecision.ShouldAutoJoin("", "ABC123");
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldAutoJoin_StudentAliasRole_ReturnsFalse()
    {
        // Only "play" is accepted — the web-app emits role=play, not role=student. This locks
        // the contract so a future rename can't silently pass the wrong value.
        bool result = StudentJoinDecision.ShouldAutoJoin("student", "ABC123");
        Assert.IsFalse(result);
    }
}
