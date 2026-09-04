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
    public string sessionId;
    public string hostToken;   // set when launched from the professor Dashboard (Phase 2)
}

[Serializable]
public class JoinRoomMessage
{
    public string type = "join_room";
    public string roomCode;
    public string sessionId;
    public string teamName;
}

[Serializable]
public class RejoinRoomMessage
{
    public string type = "rejoin_room";
    public string roomCode;
    public string sessionId;
    public string teamName;
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
    public int yourCarIndex = -1;
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
    public float s; // current speed (NavMeshAgent.velocity.magnitude)
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
public class WeatherStateMessage
{
    public string type = "weather_state";
    public int weather;    // WeatherType: 0=None/Day, 1=Snow, 2=Night, 3=Sunset
    public float duration; // informational only; host is authoritative on end
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

/// <summary>
/// Professor → Students/Web App: sent once at race start with the precise start point and
/// track bounds (and optional waypoint polyline) so viewers can render a stable, non-jittering
/// minimap. JsonUtility cannot serialize Vector3[] directly, so the waypoint polyline is
/// flattened into parallel float[] arrays.
/// </summary>
[Serializable]
public class TrackGeometryMessage
{
    public string type = "track_geometry";
    public float startX, startZ;
    public float minX, maxX, minZ, maxZ;
    public float[] wpx = Array.Empty<float>();
    public float[] wpz = Array.Empty<float>();
}

// --- Survey Messages ---

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

/// <summary>
/// Server → Professor: a new survey response was submitted on the web app.
/// </summary>
[Serializable]
public class NewWebResponseMessage
{
    public string type = "new_web_response";
    public int responseCount;
    public string teamName;
    public int surveyId;
}

// --- Reconnection Messages ---

/// <summary>
/// Server → Students: professor connection dropped, room suspended during grace period.
/// </summary>
[Serializable]
public class HostReconnectingMessage
{
    public string type = "host_reconnecting";
}

/// <summary>
/// Server → Students: professor reconnected, room resumed.
/// </summary>
[Serializable]
public class HostReconnectedMessage
{
    public string type = "host_reconnected";
}

/// <summary>
/// Server → Client: sent after successful rejoin with cached room state.
/// </summary>
[Serializable]
public class ReconnectStateMessage
{
    public string type = "reconnect_state";
    public string gamePhase;
    public int studentCount;
    public bool raceStarted;
}

// --- Config Sync Messages ---

/// <summary>
/// Professor -> Server -> Web App: exports raw SurveyConfig for import into web app.
/// configJson is a serialized SurveyConfig (questions, mappings, rules).
/// </summary>
[Serializable]
public class ConfigExportMessage
{
    public string type = "config_export";
    public string configName;
    public string configJson;
}

/// <summary>
/// Web App -> Server -> Professor: sends raw SurveyConfig for loading into Unity.
/// configJson is a serialized SurveyConfig (questions, mappings, rules).
/// </summary>
[Serializable]
public class ConfigImportMessage
{
    public string type = "config_import";
    public string configName;
    public string configJson;
}

/// <summary>
/// Server -> Client: acknowledges config export/import was processed.
/// </summary>
[Serializable]
public class ConfigSyncAckMessage
{
    public string type = "config_sync_ack";
    public bool success;
    public string error;
    public string direction; // "export" or "import"
}

// --- Student Identity Messages ---

/// <summary>
/// Server → Professor: notifies that a named student joined the room.
/// </summary>
[Serializable]
public class StudentJoinedMessage
{
    public string type = "student_joined";
    public string teamName;
    public int count;
}

/// <summary>
/// Server → Professor: full list of connected student team names.
/// </summary>
[Serializable]
public class StudentListMessage
{
    public string type = "student_list";
    public string[] teamNames;
    public int count;
}
