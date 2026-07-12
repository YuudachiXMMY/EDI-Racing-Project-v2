using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Student-facing survey panel. Renders questions from SurveyConfig received via WebSocket.
/// Shows after joining a room when professor distributes the survey.
/// Collects responses and sends them back to the professor.
/// </summary>
public class StudentSurveyPanel : MonoBehaviour
{
    [Header("References")]
    public NetworkManager NetworkManager;

    private ScrollRect scrollRect;
    private InputField teamNameField;
    private Button submitButton;
    private Text statusText;
    private Text progressText;
    private GameObject confirmationPanel;

    private SurveyConfig receivedConfig;
    private readonly List<QuestionWidget> questionWidgets = new List<QuestionWidget>();
    private bool submitted;

    private void Awake()
    {
        BuildUI();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Called when survey_questions message arrives. Shows panel with rendered questions.
    /// </summary>
    public void ShowSurvey(string json)
    {
        var msg = JsonUtility.FromJson<SurveyQuestionsMessage>(json);
        if (string.IsNullOrEmpty(msg.configJson))
        {
            Debug.LogWarning("[StudentSurveyPanel] Received empty configJson");
            return;
        }

        receivedConfig = JsonUtility.FromJson<SurveyConfig>(msg.configJson);
        if (receivedConfig == null || receivedConfig.Questions == null)
        {
            Debug.LogWarning("[StudentSurveyPanel] Failed to parse survey config");
            return;
        }

        submitted = false;
        gameObject.SetActive(true);
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (scrollRect != null) scrollRect.gameObject.SetActive(true);
        if (submitButton != null) submitButton.gameObject.SetActive(true);

        RenderQuestions(receivedConfig.Questions);
        UpdateProgress();
        SetStatus("");

        Debug.Log($"[StudentSurveyPanel] Survey shown ({receivedConfig.Questions.Length} questions)");
    }

    /// <summary>
    /// Called when survey_closed message arrives.
    /// </summary>
    public void OnSurveyClosed()
    {
        if (!submitted)
        {
            SetStatus("Survey closed by professor.");
            if (submitButton != null) submitButton.interactable = false;
        }
    }

    /// <summary>
    /// Called when survey_ack message arrives confirming professor received the response.
    /// </summary>
    public void OnAckReceived(string json)
    {
        // Ack confirms receipt — confirmation panel already shown on submit
        Debug.Log("[StudentSurveyPanel] Response acknowledged by professor");
    }

    private void BuildUI()
    {
        RectTransform rt = gameObject.GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(60, 40);
        rt.offsetMax = new Vector2(-60, -40);

        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.1f, 0.96f);

        VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(15, 15, 15, 15);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        // Header row: title + progress
        GameObject headerRow = BuilderUIFactory.CreateRow(transform, "Header", 34f);

        BuilderUIFactory.CreateText(headerRow.transform, "Title", "SURVEY",
            20, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(headerRow.transform.GetChild(0).gameObject, preferredWidth: 120f);

        progressText = BuilderUIFactory.CreateText(headerRow.transform, "Progress", "",
            14, TextAnchor.MiddleRight, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(progressText.gameObject, flexibleWidth: 1f);
        progressText.color = new Color(0.7f, 0.7f, 0.7f);

        // Team name row
        GameObject teamRow = BuilderUIFactory.CreateRow(transform, "TeamRow", 34f);

        BuilderUIFactory.CreateText(teamRow.transform, "TeamLabel", "Team Name:",
            14, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(teamRow.transform.GetChild(0).gameObject, preferredWidth: 90f);

        teamNameField = BuilderUIFactory.CreateInputField(teamRow.transform, "TeamName",
            "Enter your team name", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(teamNameField.gameObject, flexibleWidth: 1f);

        // Scroll area for questions
        scrollRect = BuilderUIFactory.CreateScrollView(transform, "QuestionsScroll",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement scrollLe = scrollRect.gameObject.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1f;
        scrollLe.flexibleWidth = 1f;

        // Status text
        GameObject statusRow = BuilderUIFactory.CreateRow(transform, "StatusRow", 22f);
        statusText = BuilderUIFactory.CreateText(statusRow.transform, "Status", "",
            12, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuilderUIFactory.AddLayoutElement(statusText.gameObject, flexibleWidth: 1f);
        statusText.color = new Color(1f, 0.5f, 0.3f);

        // Submit button
        GameObject submitRow = BuilderUIFactory.CreateRow(transform, "SubmitRow", 40f);
        submitButton = BuilderUIFactory.CreateButton(submitRow.transform, "Submit", "Submit",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.1f, 0.4f, 0.6f, 0.95f));
        BuilderUIFactory.AddLayoutElement(submitButton.gameObject, flexibleWidth: 1f);
        submitButton.onClick.AddListener(OnSubmit);

        // Confirmation panel (hidden initially, replaces form on submit)
        confirmationPanel = new GameObject("Confirmation");
        confirmationPanel.transform.SetParent(transform, false);
        RectTransform confRt = confirmationPanel.AddComponent<RectTransform>();
        confRt.anchorMin = Vector2.zero;
        confRt.anchorMax = Vector2.one;
        confRt.offsetMin = Vector2.zero;
        confRt.offsetMax = Vector2.zero;

        Image confBg = confirmationPanel.AddComponent<Image>();
        confBg.color = new Color(0.06f, 0.06f, 0.1f, 0.96f);

        VerticalLayoutGroup confVlg = confirmationPanel.AddComponent<VerticalLayoutGroup>();
        confVlg.childAlignment = TextAnchor.MiddleCenter;
        confVlg.spacing = 10;
        confVlg.childForceExpandWidth = true;
        confVlg.childForceExpandHeight = false;
        confVlg.childControlWidth = true;
        confVlg.childControlHeight = true;

        Text confTitle = BuilderUIFactory.CreateText(confirmationPanel.transform, "ConfTitle",
            "Your responses have been submitted!",
            18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        confTitle.color = new Color(0.4f, 0.9f, 0.4f);
        LayoutElement confTitleLe = confTitle.gameObject.AddComponent<LayoutElement>();
        confTitleLe.preferredHeight = 40f;

        Text confSub = BuilderUIFactory.CreateText(confirmationPanel.transform, "ConfSub",
            "Your team car is ready.\nWaiting for the race to start...",
            14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        confSub.color = new Color(0.8f, 0.8f, 0.8f);
        LayoutElement confSubLe = confSub.gameObject.AddComponent<LayoutElement>();
        confSubLe.preferredHeight = 50f;

        confirmationPanel.SetActive(false);
    }

    private void RenderQuestions(SurveyQuestion[] questions)
    {
        // Clear existing question widgets
        foreach (var w in questionWidgets)
            if (w.Root != null) Destroy(w.Root);
        questionWidgets.Clear();

        Transform content = scrollRect.content;

        for (int i = 0; i < questions.Length; i++)
        {
            var q = questions[i];
            var widget = new QuestionWidget { Question = q };

            GameObject row = new GameObject($"Q_{i}");
            row.transform.SetParent(content, false);
            RectTransform rowRt = row.AddComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0, 60);

            VerticalLayoutGroup rowVlg = row.AddComponent<VerticalLayoutGroup>();
            rowVlg.spacing = 2;
            rowVlg.padding = new RectOffset(5, 5, 5, 5);
            rowVlg.childForceExpandWidth = true;
            rowVlg.childForceExpandHeight = false;
            rowVlg.childControlWidth = true;
            rowVlg.childControlHeight = true;

            LayoutElement rowLe = row.AddComponent<LayoutElement>();

            // Question label
            string requiredMark = q.Required ? " *" : "";
            Text label = BuilderUIFactory.CreateText(row.transform, "Label",
                $"{i + 1}. {q.Text}{requiredMark}",
                14, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            LayoutElement labelLe = label.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredHeight = 22f;

            // Input widget based on type
            QuestionType qType = (QuestionType)q.Type;
            switch (qType)
            {
                case QuestionType.Text:
                    InputField textInput = BuilderUIFactory.CreateInputField(row.transform, "Input",
                        "Type your answer...", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    LayoutElement inputLe = textInput.gameObject.AddComponent<LayoutElement>();
                    inputLe.preferredHeight = 30f;
                    widget.TextInput = textInput;
                    rowLe.preferredHeight = 60f;
                    break;

                case QuestionType.MultipleChoice:
                    GameObject optionsRow = new GameObject("Options");
                    optionsRow.transform.SetParent(row.transform, false);
                    optionsRow.AddComponent<RectTransform>();
                    HorizontalLayoutGroup optHlg = optionsRow.AddComponent<HorizontalLayoutGroup>();
                    optHlg.spacing = 10;
                    optHlg.childForceExpandWidth = false;
                    optHlg.childForceExpandHeight = true;
                    optHlg.childControlWidth = true;
                    optHlg.childControlHeight = true;
                    LayoutElement optLe = optionsRow.AddComponent<LayoutElement>();
                    optLe.preferredHeight = 28f;

                    ToggleGroup group = optionsRow.AddComponent<ToggleGroup>();
                    group.allowSwitchOff = true;

                    widget.Toggles = new List<Toggle>();
                    widget.ToggleLabels = new List<string>();

                    if (q.Options != null)
                    {
                        for (int o = 0; o < q.Options.Length; o++)
                        {
                            Toggle toggle = BuilderUIFactory.CreateToggle(optionsRow.transform,
                                $"Opt_{o}", q.Options[o],
                                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                            toggle.group = group;
                            BuilderUIFactory.AddLayoutElement(toggle.gameObject, preferredWidth: 120f);
                            widget.Toggles.Add(toggle);
                            widget.ToggleLabels.Add(q.Options[o]);
                        }
                    }

                    int optionCount = q.Options != null ? q.Options.Length : 0;
                    rowLe.preferredHeight = optionCount > 4 ? 85f : 60f;
                    break;

                case QuestionType.Numeric:
                    GameObject sliderRow = new GameObject("SliderRow");
                    sliderRow.transform.SetParent(row.transform, false);
                    sliderRow.AddComponent<RectTransform>();
                    HorizontalLayoutGroup sliderHlg = sliderRow.AddComponent<HorizontalLayoutGroup>();
                    sliderHlg.spacing = 8;
                    sliderHlg.childForceExpandWidth = false;
                    sliderHlg.childForceExpandHeight = true;
                    sliderHlg.childControlWidth = true;
                    sliderHlg.childControlHeight = true;
                    LayoutElement sliderRowLe = sliderRow.AddComponent<LayoutElement>();
                    sliderRowLe.preferredHeight = 30f;

                    // Min label
                    Text minLabel = BuilderUIFactory.CreateText(sliderRow.transform, "Min",
                        q.MinValue.ToString("0"), 12, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    BuilderUIFactory.AddLayoutElement(minLabel.gameObject, preferredWidth: 30f);

                    // Slider
                    GameObject sliderObj = new GameObject("Slider");
                    sliderObj.transform.SetParent(sliderRow.transform, false);
                    RectTransform sliderRt = sliderObj.AddComponent<RectTransform>();
                    sliderRt.sizeDelta = new Vector2(200, 20);

                    // Background
                    GameObject sliderBg = new GameObject("Background");
                    sliderBg.transform.SetParent(sliderObj.transform, false);
                    Image bgImg = sliderBg.AddComponent<Image>();
                    bgImg.color = new Color(0.2f, 0.2f, 0.2f);
                    RectTransform bgRt = sliderBg.GetComponent<RectTransform>();
                    bgRt.anchorMin = new Vector2(0, 0.25f);
                    bgRt.anchorMax = new Vector2(1, 0.75f);
                    bgRt.offsetMin = Vector2.zero;
                    bgRt.offsetMax = Vector2.zero;

                    // Fill area
                    GameObject fillArea = new GameObject("Fill Area");
                    fillArea.transform.SetParent(sliderObj.transform, false);
                    RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
                    fillAreaRt.anchorMin = new Vector2(0, 0.25f);
                    fillAreaRt.anchorMax = new Vector2(1, 0.75f);
                    fillAreaRt.offsetMin = Vector2.zero;
                    fillAreaRt.offsetMax = Vector2.zero;

                    GameObject fill = new GameObject("Fill");
                    fill.transform.SetParent(fillArea.transform, false);
                    Image fillImg = fill.AddComponent<Image>();
                    fillImg.color = new Color(0.2f, 0.5f, 0.8f);
                    RectTransform fillRt = fill.GetComponent<RectTransform>();
                    fillRt.anchorMin = Vector2.zero;
                    fillRt.anchorMax = Vector2.one;
                    fillRt.offsetMin = Vector2.zero;
                    fillRt.offsetMax = Vector2.zero;

                    // Handle slide area
                    GameObject handleArea = new GameObject("Handle Slide Area");
                    handleArea.transform.SetParent(sliderObj.transform, false);
                    RectTransform handleAreaRt = handleArea.AddComponent<RectTransform>();
                    handleAreaRt.anchorMin = Vector2.zero;
                    handleAreaRt.anchorMax = Vector2.one;
                    handleAreaRt.offsetMin = Vector2.zero;
                    handleAreaRt.offsetMax = Vector2.zero;

                    GameObject handle = new GameObject("Handle");
                    handle.transform.SetParent(handleArea.transform, false);
                    Image handleImg = handle.AddComponent<Image>();
                    handleImg.color = Color.white;
                    RectTransform handleRt = handle.GetComponent<RectTransform>();
                    handleRt.sizeDelta = new Vector2(16, 16);

                    Slider slider = sliderObj.AddComponent<Slider>();
                    slider.fillRect = fillRt;
                    slider.handleRect = handleRt;
                    slider.targetGraphic = handleImg;
                    slider.minValue = q.MinValue;
                    slider.maxValue = q.MaxValue;
                    slider.wholeNumbers = true;
                    slider.value = Mathf.Round((q.MinValue + q.MaxValue) / 2f);

                    BuilderUIFactory.AddLayoutElement(sliderObj, flexibleWidth: 1f);

                    // Max label
                    Text maxLabel = BuilderUIFactory.CreateText(sliderRow.transform, "Max",
                        q.MaxValue.ToString("0"), 12, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    BuilderUIFactory.AddLayoutElement(maxLabel.gameObject, preferredWidth: 30f);

                    // Value label
                    Text valueLabel = BuilderUIFactory.CreateText(sliderRow.transform, "Value",
                        slider.value.ToString("0"), 14, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    valueLabel.color = new Color(0.4f, 0.8f, 1f);
                    BuilderUIFactory.AddLayoutElement(valueLabel.gameObject, preferredWidth: 35f);

                    // Update value label on change
                    Text valRef = valueLabel;
                    slider.onValueChanged.AddListener(v => valRef.text = v.ToString("0"));

                    widget.Slider = slider;
                    rowLe.preferredHeight = 60f;
                    break;
            }

            widget.Root = row;
            questionWidgets.Add(widget);
        }
    }

    private void OnSubmit()
    {
        if (submitted) return;

        // Validate team name
        string teamName = teamNameField != null ? teamNameField.text.Trim() : "";
        if (string.IsNullOrEmpty(teamName))
        {
            SetStatus("Please enter a team name.");
            return;
        }

        // Validate required questions
        for (int i = 0; i < questionWidgets.Count; i++)
        {
            var w = questionWidgets[i];
            if (w.Question.Required && string.IsNullOrEmpty(GetWidgetValue(w)))
            {
                SetStatus($"Please answer question {i + 1} (required).");
                return;
            }
        }

        // Collect responses
        var responses = new List<NetAttribute>();
        for (int i = 0; i < questionWidgets.Count; i++)
        {
            var w = questionWidgets[i];
            responses.Add(new NetAttribute
            {
                k = w.Question.Id,
                v = GetWidgetValue(w) ?? ""
            });
        }

        // Send
        var msg = new SurveyResponseMessage
        {
            teamName = teamName,
            responses = responses.ToArray()
        };

        if (NetworkManager != null && NetworkManager.IsConnected)
        {
            NetworkManager.Send(JsonUtility.ToJson(msg));
            submitted = true;
            ShowConfirmation();
            Debug.Log($"[StudentSurveyPanel] Response submitted for team '{teamName}'");
        }
        else
        {
            SetStatus("Not connected to server.");
        }
    }

    private string GetWidgetValue(QuestionWidget w)
    {
        QuestionType qType = (QuestionType)w.Question.Type;
        switch (qType)
        {
            case QuestionType.Text:
                return w.TextInput != null ? w.TextInput.text.Trim() : "";

            case QuestionType.MultipleChoice:
                if (w.Toggles != null)
                {
                    for (int i = 0; i < w.Toggles.Count; i++)
                    {
                        if (w.Toggles[i] != null && w.Toggles[i].isOn)
                            return w.ToggleLabels[i];
                    }
                }
                return "";

            case QuestionType.Numeric:
                return w.Slider != null ? w.Slider.value.ToString("0") : "";

            default:
                return "";
        }
    }

    private void ShowConfirmation()
    {
        if (scrollRect != null) scrollRect.gameObject.SetActive(false);
        if (submitButton != null) submitButton.gameObject.SetActive(false);
        if (confirmationPanel != null) confirmationPanel.SetActive(true);
        SetStatus("");
    }

    private void UpdateProgress()
    {
        if (progressText != null && receivedConfig != null && receivedConfig.Questions != null)
            progressText.text = $"{receivedConfig.Questions.Length} question(s)";
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private class QuestionWidget
    {
        public GameObject Root;
        public SurveyQuestion Question;
        public InputField TextInput;
        public List<Toggle> Toggles;
        public List<string> ToggleLabels;
        public Slider Slider;
    }
}
