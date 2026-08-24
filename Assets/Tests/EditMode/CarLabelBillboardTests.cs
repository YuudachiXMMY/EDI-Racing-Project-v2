using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pins <see cref="CarLabel.ComputeFacingRotation"/>: a world-space UGUI label reads correctly
/// when its forward (+Z) points AWAY from the camera (same way the camera looks) — pointing +Z
/// toward the camera mirrors the text. Must hold from any angle, including straight overhead
/// where the old Y-locked billboard turned edge-on. Deterministic — no live camera or play mode.
/// </summary>
[TestFixture]
public class CarLabelBillboardTests
{
    private const float Tol = 0.01f;

    [Test]
    public void ComputeFacingRotation_HorizontalCamera_ForwardPointsAwayFromCamera()
    {
        // Camera on +Z looking back at the label at origin → label forward must be -Z (away).
        var rot = CarLabel.ComputeFacingRotation(Vector3.zero, new Vector3(0f, 0f, 10f), Vector3.up);
        Assert.Less(Vector3.Angle(rot * Vector3.forward, Vector3.back), 1f);
    }

    [Test]
    public void ComputeFacingRotation_OverheadCamera_LabelForwardPointsDown()
    {
        // Camera straight above — the exact case the Y-locked billboard turned edge-on. The label
        // reads correctly with its +Z pointing down (away from the overhead camera).
        var rot = CarLabel.ComputeFacingRotation(Vector3.zero, new Vector3(0f, 10f, 0f), Vector3.forward);
        Assert.Less(Vector3.Angle(rot * Vector3.forward, Vector3.down), 1f);
    }

    [Test]
    public void ComputeFacingRotation_DegenerateSamePosition_ReturnsIdentity()
    {
        var rot = CarLabel.ComputeFacingRotation(Vector3.one, Vector3.one, Vector3.up);
        Assert.That(Quaternion.Angle(rot, Quaternion.identity), Is.LessThan(Tol));
    }

    [Test]
    public void ComputeFacingRotation_FacePointsAwayFromCamera()
    {
        Vector3 labelPos = new Vector3(3f, 1f, -2f);
        Vector3 camPos = new Vector3(-4f, 6f, 5f);
        var rot = CarLabel.ComputeFacingRotation(labelPos, camPos, Vector3.up);
        Vector3 expected = (labelPos - camPos).normalized;
        Assert.Less(Vector3.Angle(rot * Vector3.forward, expected), 1f);
    }

    [Test]
    public void ComputeFacingRotation_LabelRightMatchesCameraRight_NotMirrored()
    {
        // The readability guarantee: the label's local +X (text flow direction) must agree with
        // the camera's right, so glyphs are not mirrored. Compare against a camera looking at the
        // label — when the label is directly in front, its basis equals the camera's.
        Vector3 camPos = new Vector3(0f, 0f, 10f);
        var labelRot = CarLabel.ComputeFacingRotation(Vector3.zero, camPos, Vector3.up);
        var camRot = Quaternion.LookRotation(Vector3.zero - camPos, Vector3.up); // camera aimed at label
        Assert.Less(Vector3.Angle(labelRot * Vector3.right, camRot * Vector3.right), 1f);
    }
}
