using System;
using NUnit.Framework;

[TestFixture]
public class GameStateTests
{
    [Test]
    public void GameState_AllValuesExist()
    {
        Assert.IsTrue(Enum.IsDefined(typeof(GameState), GameState.Setup));
        Assert.IsTrue(Enum.IsDefined(typeof(GameState), GameState.Racing));
        Assert.IsTrue(Enum.IsDefined(typeof(GameState), GameState.Paused));
        Assert.IsTrue(Enum.IsDefined(typeof(GameState), GameState.Finished));
    }

    [Test]
    public void GameState_AllValuesDistinct()
    {
        var values = Enum.GetValues(typeof(GameState));
        var unique = new System.Collections.Generic.HashSet<int>();
        foreach (var v in values)
            Assert.IsTrue(unique.Add((int)v), $"Duplicate GameState value: {v}");
    }

    [Test]
    public void GameState_StringRoundTrip_ParseWorks()
    {
        // NetworkSync uses Enum.TryParse<GameState>(msg.state, ...)
        foreach (GameState state in Enum.GetValues(typeof(GameState)))
        {
            string name = state.ToString();
            Assert.IsTrue(Enum.TryParse<GameState>(name, out var parsed),
                $"Failed to parse '{name}' back to GameState");
            Assert.AreEqual(state, parsed);
        }
    }
}
