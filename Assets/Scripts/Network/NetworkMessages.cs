using System;
using UnityEngine;

/// <summary>
/// Serializable message types for WebSocket communication.
/// JsonUtility requires concrete types — parse "type" field first, then deserialize.
/// </summary>

[Serializable]
public class NetworkMessage
{
    public string type;
}

// --- Client → Server ---

[Serializable]
public class CreateRoomMessage
{
    public string type = "create_room";
}

[Serializable]
public class JoinRoomMessage
{
    public string type = "join_room";
    public string roomCode;
}

// --- Server → Client ---

[Serializable]
public class RoomCreatedMessage
{
    public string type = "room_created";
    public string roomCode;
}

[Serializable]
public class RoomJoinedMessage
{
    public string type = "room_joined";
    public string roomCode;
}

[Serializable]
public class StudentCountMessage
{
    public string type = "student_count";
    public int count;
}

[Serializable]
public class ErrorMessage
{
    public string type = "error";
    public string message;
}

[Serializable]
public class RoomClosedMessage
{
    public string type = "room_closed";
}

// --- Professor → Students (via server relay) ---

[Serializable]
public class RaceStartMessage
{
    public string type = "race_start";
    public NetCarData[] cars = Array.Empty<NetCarData>();
}

[Serializable]
public struct NetCarData
{
    public string teamName;
    public int colorIndex;
    public string functions; // slash-separated, e.g. "facerecog/glasses/male"

    public static NetCarData FromCarData(CarData cd)
    {
        return new NetCarData
        {
            teamName = cd.TeamName,
            colorIndex = cd.ColorIndex,
            functions = cd.Functions != null ? string.Join("/", cd.Functions) : ""
        };
    }

    public CarData ToCarData()
    {
        string[] funcs = string.IsNullOrEmpty(functions)
            ? Array.Empty<string>()
            : functions.Split('/');
        return new CarData(teamName, colorIndex, funcs);
    }
}

[Serializable]
public class GameStateMessage
{
    public string type = "game_state";
    public string state; // "Setup", "Racing", "Paused", "Finished"
}

[Serializable]
public class StateUpdateMessage
{
    public string type = "state_update";
    public float t; // race time
    public CarNetState[] cars = Array.Empty<CarNetState>();
}

[Serializable]
public struct CarNetState
{
    public int i;   // index in spawn order
    public float px, py, pz; // position
    public float ry; // y-axis rotation
    public int l;   // current lap
    public int c;   // total checkpoints passed
}

[Serializable]
public class EventTriggeredMessage
{
    public string type = "event_triggered";
    public int index;
    public string name;
    public int affected;
    public int total;
}

[Serializable]
public class LeaderboardMessage
{
    public string type = "leaderboard";
    public LeaderboardEntry[] rankings = Array.Empty<LeaderboardEntry>();
}

[Serializable]
public struct LeaderboardEntry
{
    public int rank;
    public string name;
    public int lap;
    public int cp;
}

[Serializable]
public class RaceEndMessage
{
    public string type = "race_end";
}
