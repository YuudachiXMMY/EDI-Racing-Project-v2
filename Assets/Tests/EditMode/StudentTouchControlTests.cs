using NUnit.Framework;

/// <summary>
/// Pins the student Auto Cam button caption rule (<see cref="RaceUI.AutoCamButtonLabel"/>): "Auto: All
/// Cam" only in AutoAllCams, "Auto: Top 3" for every other mode. Pure, so a refactor can't silently
/// mislabel the touch button relative to the actual camera mode. The button's click side-effects need a
/// live scene and are exercised in play mode instead.
/// </summary>
[TestFixture]
public class StudentTouchControlTests
{
    [Test]
    public void AutoCamButtonLabel_AllCams_ReturnsAllCam()
    {
        Assert.AreEqual("Auto: All Cam",
            RaceUI.AutoCamButtonLabel(CameraManager.CameraMode.AutoAllCams));
    }

    [Test]
    public void AutoCamButtonLabel_TopCars_ReturnsTop3()
    {
        Assert.AreEqual("Auto: Top 3",
            RaceUI.AutoCamButtonLabel(CameraManager.CameraMode.AutoTopCars));
    }

    [Test]
    public void AutoCamButtonLabel_FollowCar_DefaultsToTop3()
    {
        // A student in click-to-follow: the label shows the auto mode the button will enter next.
        Assert.AreEqual("Auto: Top 3",
            RaceUI.AutoCamButtonLabel(CameraManager.CameraMode.FollowCar));
    }
}
