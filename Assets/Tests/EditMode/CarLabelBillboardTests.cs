using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pins <see cref="CarLabel.ComputeFacingRotation"/>: a world-space label's +Z (readable
/// face) must point at the camera from any angle, including straight overhead where the old
/// Y-locked billboard failed. Deterministic — no live camera or play mode.
/// </summary>
[TestFixture]
public class CarLabelBillboardTests
{
    private const float Tol = 0.01f;

    [Test]
    public void ComputeFacingRotation_HorizontalCamera_FacesCamera()
    {
        var rot = CarLabel.ComputeFacingRotation(Vector3.zero, new Vector3(0f, 0f, 10f), Vector3.up);
        Assert.Less(Vector3.Angle(rot * Vector3.forward, Vector3.forward), 1f);
    }

    [Test]
    public void ComputeFacingRotation_OverheadCamera_LabelForwardPointsUp()
    {
        // Camera straight above — the exact case the Y-locked billboard turned edge-on.
        var rot = CarLabel.ComputeFacingRotation(Vector3.zero, new Vector3(0f, 10f, 0f), Vector3.forward);
        Assert.Less(Vector3.Angle(rot * Vector3.forward, Vector3.up), 1f);
    }

    [Test]
    public void ComputeFacingRotation_DegenerateSamePosition_ReturnsIdentity()
    {
        var rot = CarLabel.ComputeFacingRotation(Vector3.one, Vector3.one, Vector3.up);
        Assert.That(Quaternion.Angle(rot, Quaternion.identity), Is.LessThan(Tol));
    }

    [Test]
    public void ComputeFacingRotation_FacePointsFromLabelTowardCamera()
    {
        Vector3 labelPos = new Vector3(3f, 1f, -2f);
        Vector3 camPos = new Vector3(-4f, 6f, 5f);
        var rot = CarLabel.ComputeFacingRotation(labelPos, camPos, Vector3.up);
        Vector3 expected = (camPos - labelPos).normalized;
        Assert.Less(Vector3.Angle(rot * Vector3.forward, expected), 1f);
    }
}
