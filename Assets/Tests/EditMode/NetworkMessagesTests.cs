using System;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class NetworkMessagesTests
{
    // --- Message type fields ---

    [Test]
    public void CreateRoomMessage_TypeField_IsCorrect()
    {
        var msg = new CreateRoomMessage();
        Assert.AreEqual("create_room", msg.type);
    }

    [Test]
    public void JoinRoomMessage_TypeField_IsCorrect()
    {
        var msg = new JoinRoomMessage();
        Assert.AreEqual("join_room", msg.type);
    }

    [Test]
    public void RejoinRoomMessage_TypeField_IsCorrect()
    {
        var msg = new RejoinRoomMessage();
        Assert.AreEqual("rejoin_room", msg.type);
    }

    [Test]
    public void RoomCreatedMessage_TypeField_IsCorrect()
    {
        var msg = new RoomCreatedMessage();
        Assert.AreEqual("room_created", msg.type);
    }

    [Test]
    public void RoomJoinedMessage_TypeField_IsCorrect()
    {
        var msg = new RoomJoinedMessage();
        Assert.AreEqual("room_joined", msg.type);
    }

    [Test]
    public void ErrorMessage_TypeField_IsCorrect()
    {
        var msg = new ErrorMessage();
        Assert.AreEqual("error", msg.type);
    }

    [Test]
    public void GameStateMessage_TypeField_IsCorrect()
    {
        var msg = new GameStateMessage();
        Assert.AreEqual("game_state", msg.type);
    }

    [Test]
    public void StateUpdateMessage_TypeField_IsCorrect()
    {
        var msg = new StateUpdateMessage();
        Assert.AreEqual("state_update", msg.type);
    }

    [Test]
    public void EventTriggeredMessage_TypeField_IsCorrect()
    {
        var msg = new EventTriggeredMessage();
        Assert.AreEqual("event_triggered", msg.type);
    }

    [Test]
    public void LeaderboardMessage_TypeField_IsCorrect()
    {
        var msg = new LeaderboardMessage();
        Assert.AreEqual("leaderboard", msg.type);
    }

    [Test]
    public void RaceEndMessage_TypeField_IsCorrect()
    {
        var msg = new RaceEndMessage();
        Assert.AreEqual("race_end", msg.type);
    }

    [Test]
    public void RaceResultsMessage_TypeField_IsCorrect()
    {
        var msg = new RaceResultsMessage();
        Assert.AreEqual("race_results", msg.type);
    }

    [Test]
    public void SurveyImportMessage_TypeField_IsCorrect()
    {
        var msg = new SurveyImportMessage();
        Assert.AreEqual("survey_import", msg.type);
    }

    [Test]
    public void ConfigExportMessage_TypeField_IsCorrect()
    {
        var msg = new ConfigExportMessage();
        Assert.AreEqual("config_export", msg.type);
    }

    [Test]
    public void ConfigImportMessage_TypeField_IsCorrect()
    {
        var msg = new ConfigImportMessage();
        Assert.AreEqual("config_import", msg.type);
    }

    [Test]
    public void ConfigSyncAckMessage_TypeField_IsCorrect()
    {
        var msg = new ConfigSyncAckMessage();
        Assert.AreEqual("config_sync_ack", msg.type);
    }

    [Test]
    public void StudentJoinedMessage_TypeField_IsCorrect()
    {
        var msg = new StudentJoinedMessage();
        Assert.AreEqual("student_joined", msg.type);
    }

    [Test]
    public void StudentListMessage_TypeField_IsCorrect()
    {
        var msg = new StudentListMessage();
        Assert.AreEqual("student_list", msg.type);
    }

    [Test]
    public void HostReconnectingMessage_TypeField_IsCorrect()
    {
        var msg = new HostReconnectingMessage();
        Assert.AreEqual("host_reconnecting", msg.type);
    }

    [Test]
    public void HostReconnectedMessage_TypeField_IsCorrect()
    {
        var msg = new HostReconnectedMessage();
        Assert.AreEqual("host_reconnected", msg.type);
    }

    [Test]
    public void ReconnectStateMessage_TypeField_IsCorrect()
    {
        var msg = new ReconnectStateMessage();
        Assert.AreEqual("reconnect_state", msg.type);
    }

    // --- JSON round-trip tests ---

    [Test]
    public void RaceStartMessage_JsonRoundTrip_PreservesData()
    {
        var original = new RaceStartMessage();
        original.yourCarIndex = 2;
        original.cars = new[]
        {
            new NetCarData
            {
                teamName = "Alpha",
                attrs = new[] { new NetAttribute { k = "color", v = "blue" } }
            }
        };

        string json = JsonUtility.ToJson(original);
        var restored = JsonUtility.FromJson<RaceStartMessage>(json);

        Assert.AreEqual(2, restored.yourCarIndex);
        Assert.AreEqual(1, restored.cars.Length);
        Assert.AreEqual("Alpha", restored.cars[0].teamName);
        Assert.AreEqual("color", restored.cars[0].attrs[0].k);
    }

    [Test]
    public void StateUpdateMessage_JsonRoundTrip_PreservesCarStates()
    {
        var original = new StateUpdateMessage();
        original.t = 45.5f;
        original.cars = new[]
        {
            new CarNetState { i = 0, px = 1f, py = 2f, pz = 3f, ry = 90f, l = 2, c = 5 }
        };

        string json = JsonUtility.ToJson(original);
        var restored = JsonUtility.FromJson<StateUpdateMessage>(json);

        Assert.AreEqual(45.5f, restored.t);
        Assert.AreEqual(1, restored.cars.Length);
        Assert.AreEqual(0, restored.cars[0].i);
        Assert.AreEqual(1f, restored.cars[0].px);
        Assert.AreEqual(90f, restored.cars[0].ry);
        Assert.AreEqual(2, restored.cars[0].l);
    }

    [Test]
    public void LeaderboardMessage_JsonRoundTrip_PreservesRankings()
    {
        var original = new LeaderboardMessage();
        original.rankings = new[]
        {
            new LeaderboardEntry { rank = 1, name = "First", lap = 3, cp = 12 },
            new LeaderboardEntry { rank = 2, name = "Second", lap = 2, cp = 8 }
        };

        string json = JsonUtility.ToJson(original);
        var restored = JsonUtility.FromJson<LeaderboardMessage>(json);

        Assert.AreEqual(2, restored.rankings.Length);
        Assert.AreEqual("First", restored.rankings[0].name);
        Assert.AreEqual(3, restored.rankings[0].lap);
        Assert.AreEqual(2, restored.rankings[1].rank);
    }

    [Test]
    public void RaceStartMessage_EmptyCars_SerializesAsEmptyArray()
    {
        var msg = new RaceStartMessage();
        Assert.IsNotNull(msg.cars);
        Assert.AreEqual(0, msg.cars.Length);

        string json = JsonUtility.ToJson(msg);
        var restored = JsonUtility.FromJson<RaceStartMessage>(json);

        Assert.IsNotNull(restored.cars);
        Assert.AreEqual(0, restored.cars.Length);
    }

    [Test]
    public void ConfigSyncAckMessage_JsonRoundTrip_PreservesFields()
    {
        var original = new ConfigSyncAckMessage
        {
            success = true,
            error = "",
            direction = "export"
        };

        string json = JsonUtility.ToJson(original);
        var restored = JsonUtility.FromJson<ConfigSyncAckMessage>(json);

        Assert.IsTrue(restored.success);
        Assert.AreEqual("export", restored.direction);
    }

    [Test]
    public void ReconnectStateMessage_JsonRoundTrip_PreservesFields()
    {
        var original = new ReconnectStateMessage
        {
            gamePhase = "Racing",
            studentCount = 15,
            raceStarted = true
        };

        string json = JsonUtility.ToJson(original);
        var restored = JsonUtility.FromJson<ReconnectStateMessage>(json);

        Assert.AreEqual("Racing", restored.gamePhase);
        Assert.AreEqual(15, restored.studentCount);
        Assert.IsTrue(restored.raceStarted);
    }

    [Test]
    public void EventTriggeredMessage_JsonRoundTrip_PreservesFields()
    {
        var original = new EventTriggeredMessage
        {
            index = 3,
            name = "Snow Storm",
            affected = 5,
            total = 8
        };

        string json = JsonUtility.ToJson(original);
        var restored = JsonUtility.FromJson<EventTriggeredMessage>(json);

        Assert.AreEqual(3, restored.index);
        Assert.AreEqual("Snow Storm", restored.name);
        Assert.AreEqual(5, restored.affected);
        Assert.AreEqual(8, restored.total);
    }

    [Test]
    public void NewWebResponseMessage_TypeField_IsCorrect()
    {
        var msg = new NewWebResponseMessage();
        Assert.AreEqual("new_web_response", msg.type);
    }

    [Test]
    public void RoomClosedMessage_TypeField_IsCorrect()
    {
        var msg = new RoomClosedMessage();
        Assert.AreEqual("room_closed", msg.type);
    }

    [Test]
    public void StudentCountMessage_TypeField_IsCorrect()
    {
        var msg = new StudentCountMessage();
        Assert.AreEqual("student_count", msg.type);
    }

    [Test]
    public void RaceStartMessage_TypeField_IsCorrect()
    {
        var msg = new RaceStartMessage();
        Assert.AreEqual("race_start", msg.type);
    }
}
