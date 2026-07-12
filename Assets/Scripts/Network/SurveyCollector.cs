using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Professor-side component that distributes survey questions to students
/// and aggregates their responses into CarData for the race.
/// Attach alongside NetworkManager and NetworkSync.
/// </summary>
public class SurveyCollector : MonoBehaviour
{
    [Header("References")]
    public NetworkManager NetworkManager;
    public SurveyConfigManager ConfigManager;

    public int ResponseCount => responses.Count;
    public bool HasResponses => responses.Count > 0;
    public bool SurveyDistributed { get; private set; }

    public event Action<int> OnResponseReceived;

    private readonly List<SurveyResponseData> responses = new List<SurveyResponseData>();
    private SurveyConfig distributedConfig;

    private void Start()
    {
        if (NetworkManager != null)
            NetworkManager.OnMessageReceived += HandleMessage;
    }

    private void OnDestroy()
    {
        if (NetworkManager != null)
            NetworkManager.OnMessageReceived -= HandleMessage;
    }

    /// <summary>
    /// Sends survey questions to all connected students via WebSocket.
    /// </summary>
    public void DistributeSurvey(SurveyConfig config)
    {
        if (NetworkManager == null || !NetworkManager.IsConnected || !NetworkManager.IsHost)
        {
            Debug.LogWarning("[SurveyCollector] Cannot distribute: not hosting a room");
            return;
        }

        if (config == null || config.Questions == null || config.Questions.Length == 0)
        {
            Debug.LogWarning("[SurveyCollector] Cannot distribute: no questions in config");
            return;
        }

        distributedConfig = config;
        SurveyDistributed = true;

        var msg = new SurveyQuestionsMessage
        {
            configJson = JsonUtility.ToJson(config)
        };
        NetworkManager.Send(JsonUtility.ToJson(msg));

        Debug.Log($"[SurveyCollector] Survey distributed ({config.Questions.Length} questions)");
    }

    /// <summary>
    /// Closes the survey and notifies students.
    /// </summary>
    public void CloseSurvey()
    {
        if (NetworkManager == null || !NetworkManager.IsConnected) return;

        var msg = new SurveyClosedMessage();
        NetworkManager.Send(JsonUtility.ToJson(msg));
        Debug.Log("[SurveyCollector] Survey closed");
    }

    /// <summary>
    /// Converts all collected responses to CarData using the active config's mappings.
    /// </summary>
    public List<CarData> GetAllCarData()
    {
        var carDataList = new List<CarData>();
        if (distributedConfig == null) return carDataList;

        var mappings = distributedConfig.Mappings ?? Array.Empty<AttributeMapping>();

        foreach (var resp in responses)
        {
            CarData car = SurveyResponseMapper.MapResponses(resp.TeamName, resp.Responses, mappings);
            carDataList.Add(car);
        }

        return carDataList;
    }

    /// <summary>
    /// Resets for a new survey session.
    /// </summary>
    public void Clear()
    {
        responses.Clear();
        distributedConfig = null;
        SurveyDistributed = false;
    }

    /// <summary>
    /// Called by NetworkSync when a survey_response message arrives.
    /// </summary>
    public void ProcessResponse(string json)
    {
        var msg = JsonUtility.FromJson<SurveyResponseMessage>(json);
        if (string.IsNullOrEmpty(msg.teamName))
        {
            Debug.LogWarning("[SurveyCollector] Received response with empty team name, ignoring");
            return;
        }

        var responseEntries = new AttributeEntry[msg.responses != null ? msg.responses.Length : 0];
        for (int i = 0; i < responseEntries.Length; i++)
        {
            responseEntries[i] = new AttributeEntry
            {
                Key = msg.responses[i].k,
                Value = msg.responses[i].v
            };
        }

        responses.Add(new SurveyResponseData
        {
            TeamName = msg.teamName,
            Responses = responseEntries
        });

        // Send acknowledgement back to students
        if (NetworkManager != null && NetworkManager.IsConnected)
        {
            var ack = new SurveyAckMessage { teamName = msg.teamName };
            NetworkManager.Send(JsonUtility.ToJson(ack));
        }

        Debug.Log($"[SurveyCollector] Response received from '{msg.teamName}' ({responses.Count} total)");
        OnResponseReceived?.Invoke(responses.Count);
    }

    private void HandleMessage(string json)
    {
        // Only process on professor side
        if (NetworkManager == null || !NetworkManager.IsHost) return;

        var baseMsg = JsonUtility.FromJson<NetworkMessage>(json);
        if (baseMsg.type == "survey_response")
        {
            ProcessResponse(json);
        }
    }

    private struct SurveyResponseData
    {
        public string TeamName;
        public AttributeEntry[] Responses;
    }
}
