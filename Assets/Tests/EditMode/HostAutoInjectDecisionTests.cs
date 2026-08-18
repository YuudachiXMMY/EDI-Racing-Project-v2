using NUnit.Framework;

[TestFixture]
public class HostAutoInjectDecisionTests
{
    [Test]
    public void ShouldAutoInject_HostWithSurvey_ReturnsTrue()
    {
        // Arrange
        string role = "host";
        string surveyId = "42";

        // Act
        bool result = HostAutoInjectDecision.ShouldAutoInject(role, surveyId);

        // Assert
        Assert.IsTrue(result);
    }

    [Test]
    public void ShouldAutoInject_HostWithEmptySurvey_ReturnsFalse()
    {
        bool result = HostAutoInjectDecision.ShouldAutoInject("host", "");
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldAutoInject_HostWithNullSurvey_ReturnsFalse()
    {
        bool result = HostAutoInjectDecision.ShouldAutoInject("host", null);
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldAutoInject_HostWithWhitespaceSurvey_ReturnsFalse()
    {
        bool result = HostAutoInjectDecision.ShouldAutoInject("host", "   ");
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldAutoInject_StudentRole_ReturnsFalse()
    {
        // A student/spectator launch must never trigger an injection, even with a survey id.
        bool result = HostAutoInjectDecision.ShouldAutoInject("play", "42");
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldAutoInject_EmptyRole_ReturnsFalse()
    {
        bool result = HostAutoInjectDecision.ShouldAutoInject("", "42");
        Assert.IsFalse(result);
    }
}
