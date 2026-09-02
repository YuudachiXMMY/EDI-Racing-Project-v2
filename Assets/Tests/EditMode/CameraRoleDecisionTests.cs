using NUnit.Framework;

/// <summary>
/// Pins the role → initial camera-mode mapping exposed by <see cref="RaceUI.CameraModeForRole"/>:
/// the professor loads parked at the first fixed camera (Fixed → FixedCam_F1), the student starts
/// in the "Auto: All Cam" broadcast (AutoAllCams). The mapping is pure, so a future refactor can't
/// silently send students back to the top-N chase, or the professor back to the free-fly default.
/// </summary>
[TestFixture]
public class CameraRoleDecisionTests
{
    [Test]
    public void CameraModeForRole_Professor_ReturnsFixed()
    {
        Assert.AreEqual(CameraManager.CameraMode.Fixed, RaceUI.CameraModeForRole(true));
    }

    [Test]
    public void CameraModeForRole_Student_ReturnsAutoAllCams()
    {
        Assert.AreEqual(CameraManager.CameraMode.AutoAllCams, RaceUI.CameraModeForRole(false));
    }

    // The Escape key shares this rule: free control (professor) drops to free cam, otherwise
    // (student) back to the auto broadcast camera — a student's one way out of click-to-follow.
    [Test]
    public void ModeForEscape_AllowFreeControl_ReturnsFree()
    {
        Assert.AreEqual(CameraManager.CameraMode.Free, CameraManager.ModeForEscape(true));
    }

    [Test]
    public void ModeForEscape_NoFreeControl_ReturnsAutoTopCars()
    {
        Assert.AreEqual(CameraManager.CameraMode.AutoTopCars, CameraManager.ModeForEscape(false));
    }
}
