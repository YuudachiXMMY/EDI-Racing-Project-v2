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
public struct NetAttribute
{
    public string k;
    public string v;
}

[Serializable]
public struct NetCarData
{
    public string teamName;
    public NetAttribute[] attrs;

    public static NetCarData FromCarData(CarData cd)
    {
        NetAttribute[] netAttrs;
        if (cd.Attributes != null && cd.Attributes.Length > 0)
        {
            netAttrs = new NetAttribute[cd.Attributes.Length];
            for (int i = 0; i < cd.Attributes.Length; i++)
            {
                netAttrs[i] = new NetAttribute
                {
                    k = cd.Attributes[i].Key,
                    v = cd.Attributes[i].Value
                };
            }
        }
        else
        {
            netAttrs = Array.Empty<NetAttribute>();
        }
        return new NetCarData { teamName = cd.TeamName, attrs = netAttrs };
    }

    public CarData ToCarData()
    {
        AttributeEntry[] entries;
        if (attrs != null && attrs.Length > 0)
        {
            entries = new AttributeEntry[attrs.Length];
            for (int i = 0; i < attrs.Length; i++)
            {
                entries[i] = new AttributeEntry
                {
                    Key = attrs[i].k,
                    Value = attrs[i].v
                };
            }
        }
        else
        {
            entries = Array.Empty<AttributeEntry>();
        }
        return new CarData(teamName, entries);
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

// --- Survey Messages ---

/// <summary>
/// Professor → Students: distributes survey questions to all connected students.
/// configJson is a double-serialized SurveyConfig (JsonUtility cannot nest complex types).
/// </summary>
[Serializable]
public class SurveyQuestionsMessage
{
    public string type = "survey_questions";
    public string configJson;
}

/// <summary>
/// Student → Professor: submits survey responses for one team.
/// </summary>
[Serializable]
public class SurveyResponseMessage
{
    public string type = "survey_response";
    public string teamName;
    public NetAttribute[] responses = Array.Empty<NetAttribute>();
}

/// <summary>
/// Professor → Students: survey is closed, race is about to start.
/// </summary>
[Serializable]
public class SurveyClosedMessage
{
    public string type = "survey_closed";
}

/// <summary>
/// Professor → Student: acknowledges a response was received.
/// </summary>
[Serializable]
public class SurveyAckMessage
{
    public string type = "survey_ack";
    public string teamName;
}

// --- Professor → Web App (via server relay) ---

/// <summary>
/// Professor → Server → Web App: sends race results after race completion.
/// resultsJson is double-serialized RaceResults (JsonUtility limitation).
/// </summary>
[Serializable]
public class RaceResultsMessage
{
    public string type = "race_results";
    public string configName;
    public string resultsJson;
}

// --- Web App → Professor (via server relay) ---

/// <summary>
/// Web App → Professor: sends pre-mapped survey data directly into the Unity game.
/// exportJson is a double-serialized WebAppExport (same format as manual JSON import).
/// </summary>
[Serializable]
public class SurveyImportMessage
{
    public string type = "survey_import";
    public string configName;
    public string exportJson;
}
