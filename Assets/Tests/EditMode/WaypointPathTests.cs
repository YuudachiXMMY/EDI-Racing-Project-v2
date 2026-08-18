using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class WaypointPathTests
{
    private GameObject pathObj;
    private WaypointPath waypointPath;
    private List<GameObject> childObjects;

    [SetUp]
    public void SetUp()
    {
        pathObj = new GameObject("WaypointPath");
        waypointPath = pathObj.AddComponent<WaypointPath>();
        childObjects = new List<GameObject>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in childObjects)
            Object.DestroyImmediate(obj);
        Object.DestroyImmediate(pathObj);
    }

    private Transform CreateWaypoint(string name, Vector3 position)
    {
        var obj = new GameObject(name);
        childObjects.Add(obj);
        obj.transform.position = position;
        return obj.transform;
    }

    private void SetupWaypoints(params Vector3[] positions)
    {
        var waypoints = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
            waypoints[i] = CreateWaypoint($"WP{i}", positions[i]);
        waypointPath.Waypoints = waypoints;
    }

    [Test]
    public void Count_ReturnsNumberOfWaypoints()
    {
        SetupWaypoints(Vector3.zero, Vector3.one, Vector3.forward);
        Assert.AreEqual(3, waypointPath.Count);
    }

    [Test]
    public void GetWaypoint_ReturnsCorrectWaypoint()
    {
        SetupWaypoints(Vector3.zero, Vector3.right, Vector3.forward);

        Assert.AreEqual(Vector3.zero, waypointPath.GetWaypoint(0).position);
        Assert.AreEqual(Vector3.right, waypointPath.GetWaypoint(1).position);
        Assert.AreEqual(Vector3.forward, waypointPath.GetWaypoint(2).position);
    }

    [Test]
    public void GetWaypoint_WrapsAround_WhenIndexExceedsLength()
    {
        SetupWaypoints(Vector3.zero, Vector3.right, Vector3.forward);

        // Index 3 should wrap to 0, index 4 to 1, etc.
        Assert.AreEqual(waypointPath.GetWaypoint(0).position,
            waypointPath.GetWaypoint(3).position);
        Assert.AreEqual(waypointPath.GetWaypoint(1).position,
            waypointPath.GetWaypoint(4).position);
    }

    [Test]
    public void GetWaypoint_SingleWaypoint_AlwaysReturnsSame()
    {
        SetupWaypoints(new Vector3(5, 0, 5));

        Assert.AreEqual(new Vector3(5, 0, 5), waypointPath.GetWaypoint(0).position);
        Assert.AreEqual(new Vector3(5, 0, 5), waypointPath.GetWaypoint(1).position);
        Assert.AreEqual(new Vector3(5, 0, 5), waypointPath.GetWaypoint(99).position);
    }

    [Test]
    public void Count_WithEmptyArray_ReturnsZero()
    {
        waypointPath.Waypoints = new Transform[0];
        Assert.AreEqual(0, waypointPath.Count);
    }
}
