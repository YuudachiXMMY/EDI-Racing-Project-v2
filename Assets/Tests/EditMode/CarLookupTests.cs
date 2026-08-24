using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Covers <see cref="CarLookup.FindByTeamName"/> — the row → spawned-car resolver used by RaceUI's
/// leaderboard click-to-follow. The rule is a simple case-sensitive TeamName match, but it must be
/// null-safe (bad/blank inputs, null list entries) and deterministic on duplicates, so those paths
/// are pinned here. Uses throwaway GameObjects with CarIdentity since the lookup calls GetComponent.
/// </summary>
[TestFixture]
public class CarLookupTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    private GameObject MakeCar(string teamName)
    {
        // Object name is deliberately DIFFERENT from teamName so a test can prove the lookup matches
        // on CarIdentity.TeamName, not GameObject.name.
        var go = new GameObject("obj:" + teamName);
        go.AddComponent<CarIdentity>().TeamName = teamName;
        created.Add(go);
        return go;
    }

    // A live GameObject with NO CarIdentity — exercises the `identity != null` guard.
    private GameObject MakeBare(string objectName)
    {
        var go = new GameObject(objectName);
        created.Add(go);
        return go;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in created)
            if (go != null) Object.DestroyImmediate(go);
        created.Clear();
    }

    [Test]
    public void FindByTeamName_Match_ReturnsThatCar()
    {
        // Arrange
        var red = MakeCar("Red Rockets");
        var blue = MakeCar("Blue Bolts");
        var cars = new List<GameObject> { red, blue };

        // Act
        var result = CarLookup.FindByTeamName(cars, "Blue Bolts");

        // Assert
        Assert.AreSame(blue, result);
    }

    [Test]
    public void FindByTeamName_UnknownName_ReturnsNull()
    {
        // Arrange
        var cars = new List<GameObject> { MakeCar("Red Rockets") };

        // Act
        var result = CarLookup.FindByTeamName(cars, "Ghost");

        // Assert
        Assert.IsNull(result);
    }

    [Test]
    public void FindByTeamName_NullList_ReturnsNull()
    {
        // Act
        var result = CarLookup.FindByTeamName(null, "Red Rockets");

        // Assert
        Assert.IsNull(result);
    }

    [Test]
    public void FindByTeamName_BlankName_ReturnsNull()
    {
        // Arrange
        var cars = new List<GameObject> { MakeCar("Red Rockets") };

        // Act
        var result = CarLookup.FindByTeamName(cars, "");

        // Assert
        Assert.IsNull(result);
    }

    [Test]
    public void FindByTeamName_NullEntryInList_IsSkipped()
    {
        // Arrange — a null slot must not throw and must not stop the scan.
        var red = MakeCar("Red Rockets");
        var cars = new List<GameObject> { null, red };

        // Act
        var result = CarLookup.FindByTeamName(cars, "Red Rockets");

        // Assert
        Assert.AreSame(red, result);
    }

    [Test]
    public void FindByTeamName_DuplicateNames_ReturnsFirst()
    {
        // Arrange
        var first = MakeCar("Red Rockets");
        var second = MakeCar("Red Rockets");
        var cars = new List<GameObject> { first, second };

        // Act
        var result = CarLookup.FindByTeamName(cars, "Red Rockets");

        // Assert
        Assert.AreSame(first, result);
    }

    [Test]
    public void FindByTeamName_MatchesOnTeamNameNotObjectName()
    {
        // Arrange — the GameObject's name ("obj:Red Rockets") differs from the team key.
        var red = MakeCar("Red Rockets");
        var cars = new List<GameObject> { red };

        // Act + Assert — the team key resolves; the GameObject's own name does not.
        Assert.AreSame(red, CarLookup.FindByTeamName(cars, "Red Rockets"));
        Assert.IsNull(CarLookup.FindByTeamName(cars, red.name));
    }

    [Test]
    public void FindByTeamName_CaseMismatch_ReturnsNull()
    {
        // Arrange — the match is case-sensitive (ordinal ==).
        var cars = new List<GameObject> { MakeCar("Blue Bolts") };

        // Act
        var result = CarLookup.FindByTeamName(cars, "blue bolts");

        // Assert
        Assert.IsNull(result);
    }

    [Test]
    public void FindByTeamName_EntryWithoutCarIdentity_IsSkipped()
    {
        // Arrange — a live GameObject lacking CarIdentity must not throw or stop the scan.
        var bare = MakeBare("no_identity");
        var red = MakeCar("Red Rockets");
        var cars = new List<GameObject> { bare, red };

        // Act
        var result = CarLookup.FindByTeamName(cars, "Red Rockets");

        // Assert
        Assert.AreSame(red, result);
    }
}
