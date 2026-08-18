using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Professor-only panel displaying event buttons with trigger status.
/// Each button calls EventManager.TriggerEvent(index).
/// </summary>
public class EventPanel : MonoBehaviour
{
    [Header("References")]
    public EventManager EventManager;

    [Header("UI Elements")]
    [Tooltip("Parent transform for event button rows")]
    public Transform ContentParent;

    [Tooltip("Prefab for a single event row (Button + Text required)")]
    public GameObject EventRowPrefab;

    private Button[] eventButtons;
    private Text[] eventLabels;
    private bool initialized;

    private void Awake()
    {
        // Defensive auto-wire: SceneWiring/TrackSetupEditor do not re-assign this on an
        // already-existing panel, so a scene can ship with EventManager unset — which leaves the
        // panel visible but empty (BuildEventRows early-returns on null).
        //
        // This MUST run in Awake, not Start: OnEnable fires between Awake and Start, and it is
        // where we subscribe to OnEventTriggered. If the wire happened in Start, the first
        // OnEnable would see EventManager == null, skip the subscription, and never re-subscribe —
        // leaving button states frozen. Wiring in Awake guarantees OnEnable has a live reference.
        if (EventManager == null)
            EventManager = FindFirstObjectByType<EventManager>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        BuildEventRows();
    }

    private void OnEnable()
    {
        if (EventManager != null)
            EventManager.OnEventTriggered += OnEventTriggered;
    }

    private void OnDisable()
    {
        if (EventManager != null)
            EventManager.OnEventTriggered -= OnEventTriggered;
    }

    private void BuildEventRows()
    {
        if (EventManager == null || EventManager.Schedule == null) return;

        var events = EventManager.Schedule.Events;
        eventButtons = new Button[events.Length];
        eventLabels = new Text[events.Length];

        for (int i = 0; i < events.Length; i++)
        {
            GameObject row = Instantiate(EventRowPrefab, ContentParent);
            var button = row.GetComponentInChildren<Button>();
            var text = row.GetComponentInChildren<Text>();

            if (text != null)
                text.text = $"[{i + 1}] {events[i].DisplayName}";

            if (button != null)
            {
                int index = i; // capture for closure
                button.onClick.AddListener(() => TriggerEvent(index));
            }

            eventButtons[i] = button;
            eventLabels[i] = text;
        }

        initialized = true;
    }

    private void TriggerEvent(int index)
    {
        if (EventManager != null)
            EventManager.TriggerEvent(index);
    }

    private void OnEventTriggered(EventRule rule, int affectedCount)
    {
        if (!initialized) return;

        // Update button states
        var events = EventManager.Schedule.Events;
        for (int i = 0; i < events.Length; i++)
        {
            if (eventButtons[i] == null) continue;

            if (events[i].HasBeenTriggered && !events[i].AllowRepeat)
            {
                eventButtons[i].interactable = false;
                if (eventLabels[i] != null)
                    eventLabels[i].color = Color.gray;
            }
        }
    }
}
