using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject createMatchPanel;
    [SerializeField] private GameObject findMatchPanel;
    [SerializeField] private GameObject roomLobbyPanel;

    [Header("Home")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button openCreateMatchButton;
    [SerializeField] private Button openFindMatchButton;
    [SerializeField] private Button quitButton;

    [Header("Create Match")]
    [SerializeField] private TMP_Dropdown matchModeDropdown;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_InputField maxPlayersInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button backFromCreateButton;

    [Header("Find Match")]
    [SerializeField] private Button joinButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button backFromFindButton;
    [SerializeField] private TMP_Text selectedRoomText;
    [SerializeField] private TMP_Text emptySessionsText;
    [SerializeField] private Transform sessionListParent;
    [SerializeField] private LobbySessionEntryUI sessionEntryPrefab;

    [Header("Room Lobby")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startMatchButton;
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private TMP_Text roomTitleText;
    [SerializeField] private Transform playerListParent;
    [SerializeField] private RoomPlayerEntryUI playerEntryPrefab;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    [Header("Network")]
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject roomLobbyStatePrefab;
    [SerializeField] private int defaultMaxPlayers = 2;

    public static LobbyUI Instance { get; private set; }
    public static string PlayerName { get; private set; }
    public static MatchMode SelectedMatchMode { get; private set; } = MatchMode.FreeForAll;
    private static string pendingStatusMessage;

    private NetworkRunner runner;
    private RoomLobbyState roomLobbyState;
    private readonly List<LobbySessionEntryUI> spawnedSessionEntries = new();
    private readonly List<GameObject> spawnedPlayerEntries = new();
    private SessionInfo selectedSession;
    private bool localReady;
    private bool isStartingOrJoining;
    private bool intentionalLobbyShutdown;
    private bool lobbyShutdownHandled;
    private bool lobbyDisconnectRecoveryQueued;
    private bool fusionDisconnectMessageReceived;
    private int lobbyHostPlayerId = -1;
    private GameObject connectionPopupPanel;
    private TMP_Text connectionPopupMessageText;
    private Button connectionPopupCloseButton;
    private float connectionPopupShownAt;
    private bool connectionPopupHideQueued;

    private const float TeamColumnMin = 0.42f;
    private const float TeamColumnMax = 0.58f;
    private const float StateColumnMin = 0.54f;
    private const float PlayerRowHeight = 54f;
    private const float HeaderRowHeight = 34f;
    private const float PlayerRowFontSize = 26f;
    private const float HeaderRowFontSize = 26f;
    private const int MainMenuSceneBuildIndex = 0;
    private const string ConnectionLostWithHostMessage = "Connection lost with host.";

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleUnityLogMessage;
        Application.logMessageReceivedThreaded += HandleUnityLogMessageThreaded;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleUnityLogMessage;
        Application.logMessageReceivedThreaded -= HandleUnityLogMessageThreaded;
    }

    private void Start()
    {
        WireButton(openCreateMatchButton, ShowCreateMatchPanel);
        WireButton(openFindMatchButton, ShowFindMatchPanel);
        WireButton(quitButton, QuitGame);
        WireButton(hostButton, HostGame);
        WireButton(joinButton, JoinSelectedGame);
        WireButton(refreshButton, RefreshSessionList);
        WireButton(backFromCreateButton, ShowHomePanel);
        WireButton(backFromFindButton, BackFromFindPanel);
        WireButton(readyButton, ToggleReady);
        WireButton(startMatchButton, StartMatch);
        WireButton(leaveRoomButton, LeaveRoom);
        ConfigureMatchModeDropdown();

        ShowHomePanel();
        if (!string.IsNullOrWhiteSpace(pendingStatusMessage))
        {
            SetStatusMessage(pendingStatusMessage);
            ShowConnectionPopup(pendingStatusMessage);
            pendingStatusMessage = null;
        }
        else
        {
            SetStatusMessage("Ready.");
        }
    }

    private void Update()
    {
        if (fusionDisconnectMessageReceived)
        {
            fusionDisconnectMessageReceived = false;
            if (runner == null || !runner.IsServer)
                HandleLobbyDisconnect(ConnectionLostWithHostMessage);
        }

        if (ShouldRecoverFromLostLobbyRunner())
            HandleLobbyDisconnect(ConnectionLostWithHostMessage);

        if (connectionPopupPanel == null || !connectionPopupPanel.activeSelf)
            return;

        bool canDismiss = Time.unscaledTime - connectionPopupShownAt > 0.15f;
        if (!canDismiss)
            return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            QueueHideConnectionPopup();
    }

    public void OnRoomLobbyStateReady(RoomLobbyState state)
    {
        roomLobbyState = state;
        roomLobbyState.RegisterLocalPlayer(PlayerName);
        ShowRoomLobbyPanel();
    }

    public void RefreshRoomPlayers(IReadOnlyList<RoomLobbyState.PlayerLobbyData> players)
    {
        ClearPlayerEntries();
        HideMisplacedRoomLobbyTexts();
        UpdateLobbyHostPlayerId(players);
        Transform parent = GetListContent(playerListParent);
        EnsurePlayerTableRoot(parent);

        if (parent != null)
            spawnedPlayerEntries.Add(CreatePlayerTable(parent, players));

        bool canStart = runner != null && runner.IsServer && roomLobbyState != null && roomLobbyState.CanHostStart;
        if (startMatchButton != null)
            startMatchButton.interactable = canStart;

        if (roomTitleText != null && runner != null && runner.SessionInfo != null && !IsInsidePlayerList(roomTitleText.transform))
            roomTitleText.text = runner.SessionInfo.Name;
    }

    public void SetStatusMessage(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    public static void SetPendingStatusMessage(string message)
    {
        pendingStatusMessage = message;
    }

    public static void ReturnToMenuFromHost(string message)
    {
        if (Instance != null)
            Instance.StartCoroutine(Instance.RecoverFromLobbyDisconnect(string.IsNullOrWhiteSpace(message) ? ConnectionLostWithHostMessage : message));
        else
        {
            SetPendingStatusMessage(string.IsNullOrWhiteSpace(message) ? ConnectionLostWithHostMessage : message);
            SceneManager.LoadScene(MainMenuSceneBuildIndex, LoadSceneMode.Single);
        }
    }

    private void ShowConnectionPopup(string message)
    {
        EnsureConnectionPopup();

        if (connectionPopupMessageText != null)
            connectionPopupMessageText.text = string.IsNullOrWhiteSpace(message)
                ? "Connection lost with host."
                : message;

        if (connectionPopupPanel != null)
        {
            connectionPopupPanel.SetActive(true);
            connectionPopupPanel.transform.SetAsLastSibling();
            connectionPopupShownAt = Time.unscaledTime;
            connectionPopupHideQueued = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (connectionPopupCloseButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(connectionPopupCloseButton.gameObject);
    }

    private void HideConnectionPopup()
    {
        connectionPopupHideQueued = false;

        if (connectionPopupPanel != null)
            connectionPopupPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ShowHomePanel();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void QueueHideConnectionPopup()
    {
        if (connectionPopupHideQueued)
            return;

        connectionPopupHideQueued = true;
        StartCoroutine(HideConnectionPopupNextFrame());
    }

    private IEnumerator HideConnectionPopupNextFrame()
    {
        yield return null;
        HideConnectionPopup();
    }

    private void EnsureConnectionPopup()
    {
        if (connectionPopupPanel != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
            return;

        EnsureUiEventSystem();

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        connectionPopupPanel = new GameObject("ConnectionLostPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        connectionPopupPanel.transform.SetParent(canvas.transform, false);

        var overlayRect = connectionPopupPanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var overlayImage = connectionPopupPanel.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.45f);

        var overlayButton = connectionPopupPanel.GetComponent<Button>();
        overlayButton.transition = Selectable.Transition.None;
        overlayButton.onClick.AddListener(HideConnectionPopup);

        var window = CreatePopupWindow(connectionPopupPanel.transform);
        CreatePopupLabel(window, "Connection Lost", 34f, new Vector2(0f, 58f), Color.white);
        connectionPopupMessageText = CreatePopupLabel(window, "Connection lost with host.", 22f, new Vector2(0f, 8f), Color.white);
        connectionPopupCloseButton = CreatePopupButton(window, "Close", new Vector2(0f, -70f), HideConnectionPopup);

        connectionPopupPanel.SetActive(false);
    }

    private void EnsureUiEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystem);
    }

    private Transform CreatePopupWindow(Transform parent)
    {
        var window = new GameObject("Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        window.transform.SetParent(parent, false);

        var rect = window.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(460f, 230f);

        var image = window.GetComponent<Image>();
        image.color = new Color(0.11f, 0.11f, 0.11f, 0.94f);

        return window.transform;
    }

    private TMP_Text CreatePopupLabel(Transform parent, string text, float fontSize, Vector2 anchoredPosition, Color color)
    {
        var labelObject = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(380f, 58f);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = fontSize;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;

        return label;
    }

    private Button CreatePopupButton(Transform parent, string text, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObject = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(180f, 48f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.9f, 0.9f, 0.9f, 0.96f);

        var button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        CreatePopupLabel(buttonObject.transform, text, 24f, Vector2.zero, new Color(0.08f, 0.08f, 0.08f, 1f));

        return button;
    }

    private void ShowHomePanel()
    {
        ShowPanel(homePanel);
        SetHomeButtonsInteractable(true);
        SetButtonsInteractable(true);
        SetJoinInteractable(false);
        SetStartInteractable(false);
    }

    private void ShowCreateMatchPanel()
    {
        if (!TrySetPlayerNameFromInput())
            return;

        ShowPanel(createMatchPanel);
        SetStatusMessage("Configure match.");
    }

    private async void ShowFindMatchPanel()
    {
        if (!TrySetPlayerNameFromInput())
            return;

        ShowPanel(findMatchPanel);
        await EnsureLobbyRunner();
    }

    private void ShowRoomLobbyPanel()
    {
        ShowPanel(roomLobbyPanel);
        localReady = false;
        UpdateReadyButton();
        SetStatusMessage("Room created. Waiting for players.");
    }

    private void ShowPanel(GameObject activePanel)
    {
        SetPanel(homePanel, activePanel == homePanel);
        SetPanel(createMatchPanel, activePanel == createMatchPanel);
        SetPanel(findMatchPanel, activePanel == findMatchPanel);
        SetPanel(roomLobbyPanel, activePanel == roomLobbyPanel);
    }

    private async System.Threading.Tasks.Task EnsureLobbyRunner()
    {
        if (runner != null)
            return;

        if (runnerPrefab == null)
        {
            SetStatusMessage("Missing Runner Prefab in Inspector.");
            return;
        }

        runner = Instantiate(runnerPrefab);
        runner.name = "LobbyRunner";
        DontDestroyOnLoad(runner.gameObject);
        runner.ProvideInput = true;
        runner.AddCallbacks(this);
        lobbyShutdownHandled = false;

        SetStatusMessage("Connecting to lobby...");
        var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        SetStatusMessage(result.Ok ? "Searching matches..." : $"Lobby error: {result.ShutdownReason}");
    }

    private async void RefreshSessionList()
    {
        await EnsureLobbyRunner();
        SetStatusMessage("Refreshing match list...");
    }

    private async void HostGame()
    {
        if (isStartingOrJoining)
            return;

        string roomName = GetRoomName();
        int maxPlayers = GetMaxPlayers();

        await EnsureLobbyRunner();
        await StartSession(GameMode.Host, roomName, maxPlayers, true);
    }

    private async void JoinSelectedGame()
    {
        if (isStartingOrJoining)
            return;

        if (!selectedSession)
        {
            SetStatusMessage("Select a room before joining.");
            return;
        }

        await StartSession(GameMode.Client, selectedSession.Name, selectedSession.MaxPlayers, false);
    }

    private async System.Threading.Tasks.Task StartSession(GameMode mode, string sessionName, int maxPlayers, bool allowCreate)
    {
        if (runner == null)
            return;

        isStartingOrJoining = true;
        SetButtonsInteractable(false);
        SetStatusMessage($"Connecting to {sessionName}...");

        var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            PlayerCount = maxPlayers,
            SceneManager = sceneManager,
            IsOpen = true,
            IsVisible = true,
            EnableClientSessionCreation = allowCreate
        });

        if (!result.Ok)
        {
            SetStatusMessage($"Error: {result.ShutdownReason}");
            SetButtonsInteractable(true);
            isStartingOrJoining = false;
            return;
        }

        SetStatusMessage($"Connected to {sessionName}.");

        if (runner.IsServer)
            SpawnRoomLobbyState();
    }

    private void SpawnRoomLobbyState()
    {
        if (roomLobbyState != null)
            return;

        if (roomLobbyStatePrefab == null)
        {
            SetStatusMessage("Missing Room Lobby State Prefab.");
            return;
        }

        var stateObject = runner.Spawn(roomLobbyStatePrefab, Vector3.zero, Quaternion.identity);
        roomLobbyState = stateObject.GetComponent<RoomLobbyState>();
        roomLobbyState.ConfigureMatchMode(SelectedMatchMode);
    }

    private void SelectSession(SessionInfo session)
    {
        selectedSession = session;
        SetJoinInteractable(session.IsOpen && session.PlayerCount < session.MaxPlayers);

        if (selectedRoomText != null)
            selectedRoomText.text = $"Selected room: {session.Name}";
    }

    private void RebuildSessionList(List<SessionInfo> sessions)
    {
        ClearSessionEntries();
        selectedSession = null;
        SetJoinInteractable(false);

        if (selectedRoomText != null)
            selectedRoomText.text = "Selected room: -";

        foreach (var session in sessions)
        {
            if (!session.IsValid || !session.IsVisible)
                continue;

            if (sessionEntryPrefab == null || sessionListParent == null)
                continue;

            var entry = Instantiate(sessionEntryPrefab, sessionListParent);
            entry.Setup(session, SelectSession);
            spawnedSessionEntries.Add(entry);
        }

        bool hasSessions = spawnedSessionEntries.Count > 0;

        if (emptySessionsText != null)
            emptySessionsText.gameObject.SetActive(!hasSessions);

        SetStatusMessage(hasSessions ? $"{spawnedSessionEntries.Count} room(s) available." : "No rooms available.");
    }

    private void ToggleReady()
    {
        if (roomLobbyState == null)
            return;

        localReady = !localReady;
        roomLobbyState.SetLocalReady(localReady);
        UpdateReadyButton();
    }

    private void StartMatch()
    {
        if (roomLobbyState != null)
            roomLobbyState.TryStartGame();
    }

    private async void LeaveRoom()
    {
        SetStatusMessage("Leaving room...");

        if (runner != null)
        {
            if (runner.IsServer && roomLobbyState != null)
            {
                roomLobbyState.NotifyHostLeavingLobby();
                await System.Threading.Tasks.Task.Delay(300);
            }

            intentionalLobbyShutdown = true;
            await runner.Shutdown();
        }

        ResetNetworkState(true);
        ShowHomePanel();
        SetStatusMessage("Left room.");
    }

    private async void BackFromFindPanel()
    {
        if (runner != null && !isStartingOrJoining)
        {
            intentionalLobbyShutdown = true;
            await runner.Shutdown();
            ResetNetworkState(true);
        }

        ShowHomePanel();
    }

    private void ResetNetworkState(bool destroyRunner = false)
    {
        var runnerToReset = runner;
        if (runnerToReset != null)
        {
            runnerToReset.RemoveCallbacks(this);

            if (destroyRunner)
                Destroy(runnerToReset.gameObject);
        }

        runner = null;
        roomLobbyState = null;
        selectedSession = null;
        localReady = false;
        isStartingOrJoining = false;
        intentionalLobbyShutdown = false;
        lobbyDisconnectRecoveryQueued = false;
        lobbyHostPlayerId = -1;
        ClearSessionEntries();
        ClearPlayerEntries();
        SetHomeButtonsInteractable(true);
        SetButtonsInteractable(true);
        SetStartInteractable(false);
        UpdateReadyButton();
    }

    private void UpdateReadyButton()
    {
        var label = GetReadyButtonLabel();
        if (label != null)
            label.text = localReady ? "Ready" : "Not Ready";
    }

    private GameObject CreatePlayerTable(Transform parent, IReadOnlyList<RoomLobbyState.PlayerLobbyData> players)
    {
        var table = new GameObject("PlayerTable", typeof(RectTransform), typeof(LayoutElement));
        table.transform.SetParent(parent, false);

        var rect = table.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, HeaderRowHeight + PlayerRowHeight * players.Count);

        var layoutElement = table.GetComponent<LayoutElement>();
        layoutElement.minHeight = rect.sizeDelta.y;
        layoutElement.preferredHeight = rect.sizeDelta.y;

        var playersColumn = CreatePlayerColumn(table.transform, "PlayersColumn", 0f, TeamColumnMin);
        var teamColumn = CreatePlayerColumn(table.transform, "TeamColumn", TeamColumnMin, TeamColumnMax);
        var stateColumn = CreatePlayerColumn(table.transform, "StateColumn", StateColumnMin, 1f);

        CreateTableText(playersColumn, "Players", TextAlignmentOptions.Left, Color.white, HeaderRowHeight, HeaderRowFontSize);
        CreateTableText(teamColumn, "Team", TextAlignmentOptions.Center, Color.white, HeaderRowHeight, HeaderRowFontSize);
        CreateTableText(stateColumn, "State", TextAlignmentOptions.Right, Color.white, HeaderRowHeight, HeaderRowFontSize);

        foreach (var player in players)
        {
            CreateTableText(playersColumn, player.IsHost ? $"{player.Name} (Host)" : player.Name, TextAlignmentOptions.Left, Color.white, PlayerRowHeight, PlayerRowFontSize);
            CreateTableText(teamColumn, GetTeamLabel(player.Team), TextAlignmentOptions.Center, GetTeamColor(player.Team), PlayerRowHeight, PlayerRowFontSize);
            CreateTableText(stateColumn, player.IsReady ? "Ready" : "Not Ready", TextAlignmentOptions.Right, player.IsReady ? Color.green : Color.red, PlayerRowHeight, PlayerRowFontSize);
        }

        return table;
    }

    private Transform CreatePlayerColumn(Transform parent, string columnName, float anchorMinX, float anchorMaxX)
    {
        var column = new GameObject(columnName, typeof(RectTransform), typeof(VerticalLayoutGroup));
        column.transform.SetParent(parent, false);

        var rect = column.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchorMinX, 0f);
        rect.anchorMax = new Vector2(anchorMaxX, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var layout = column.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 0f;

        return column.transform;
    }

    private void CreateTableText(Transform parent, string text, TextAlignmentOptions alignment, Color color, float height, float fontSize)
    {
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = alignment;
        label.fontSize = fontSize;
        label.fontSizeMin = 14f;
        label.fontSizeMax = fontSize;
        label.enableAutoSizing = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.raycastTarget = false;

        var layout = textObject.GetComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private Transform GetListContent(Transform assignedParent)
    {
        if (assignedParent == null)
            return null;

        var scrollRect = assignedParent.GetComponent<ScrollRect>();
        if (scrollRect != null && scrollRect.content != null)
            return scrollRect.content;

        return assignedParent;
    }

    private void EnsurePlayerTableRoot(Transform content)
    {
        if (content == null)
            return;

        var vertical = content.GetComponent<VerticalLayoutGroup>();
        if (vertical == null)
            vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();

        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.spacing = 8f;
        vertical.padding = new RectOffset(16, 16, 12, 12);

        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();

        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private TMP_Text GetReadyButtonLabel()
    {
        if (readyButtonText != null)
        {
            bool isButtonChild = readyButton == null || readyButtonText.transform.IsChildOf(readyButton.transform);
            if (isButtonChild)
                return readyButtonText;

            readyButtonText.gameObject.SetActive(false);
        }

        return readyButton != null ? readyButton.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void HideMisplacedRoomLobbyTexts()
    {
        if (readyButtonText != null && readyButton != null && !readyButtonText.transform.IsChildOf(readyButton.transform))
            readyButtonText.gameObject.SetActive(false);

        if (roomTitleText != null && IsInsidePlayerList(roomTitleText.transform))
            roomTitleText.gameObject.SetActive(false);
    }

    private bool IsInsidePlayerList(Transform target)
    {
        return playerListParent != null && target != null && target.IsChildOf(playerListParent);
    }

    private void ClearSessionEntries()
    {
        foreach (var entry in spawnedSessionEntries)
        {
            if (entry != null)
                Destroy(entry);
        }

        spawnedSessionEntries.Clear();
    }

    private void ClearPlayerEntries()
    {
        foreach (var entry in spawnedPlayerEntries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }

        spawnedPlayerEntries.Clear();
    }

    private string GetPlayerName()
    {
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
            return playerNameInput.text.Trim();

        return string.Empty;
    }

    private bool TrySetPlayerNameFromInput()
    {
        string playerName = GetPlayerName();
        if (string.IsNullOrWhiteSpace(playerName))
        {
            SetStatusMessage("Enter a username.");
            return false;
        }

        PlayerName = playerName;
        return true;
    }

    private string GetRoomName()
    {
        return roomNameInput != null && !string.IsNullOrWhiteSpace(roomNameInput.text)
            ? roomNameInput.text.Trim()
            : $"Sala-{Random.Range(1000, 9999)}";
    }

    private void ConfigureMatchModeDropdown()
    {
        if (matchModeDropdown == null)
            return;

        matchModeDropdown.ClearOptions();
        matchModeDropdown.AddOptions(new List<string> { "Free For All", "Team Deathmatch" });
        matchModeDropdown.value = SelectedMatchMode == MatchMode.TeamDeathmatch ? 1 : 0;
        matchModeDropdown.onValueChanged.AddListener(SetSelectedMatchMode);
        SetSelectedMatchMode(matchModeDropdown.value);
    }

    private void SetSelectedMatchMode(int value)
    {
        SelectedMatchMode = value == 1 ? MatchMode.TeamDeathmatch : MatchMode.FreeForAll;
        MatchSettings.SetMode(SelectedMatchMode);
    }

    private string GetTeamLabel(PlayerTeam team)
    {
        return SelectedMatchMode == MatchMode.TeamDeathmatch || team != PlayerTeam.None ? team.ToString() : "-";
    }

    private Color GetTeamColor(PlayerTeam team)
    {
        return team switch
        {
            PlayerTeam.Blue => new Color(0.2f, 0.55f, 1f),
            PlayerTeam.Red => new Color(1f, 0.25f, 0.25f),
            _ => Color.white
        };
    }

    private int GetMaxPlayers()
    {
        if (maxPlayersInput != null && int.TryParse(maxPlayersInput.text, out int value))
            return Mathf.Clamp(value, 2, 8);

        return Mathf.Clamp(defaultMaxPlayers, 2, 8);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (hostButton != null)
            hostButton.interactable = interactable;

        if (refreshButton != null)
            refreshButton.interactable = interactable;

        SetJoinInteractable(interactable && selectedSession);
    }

    private void SetHomeButtonsInteractable(bool interactable)
    {
        if (openCreateMatchButton != null)
            openCreateMatchButton.interactable = interactable;

        if (openFindMatchButton != null)
            openFindMatchButton.interactable = interactable;

        if (quitButton != null)
            quitButton.interactable = interactable;

        if (backFromCreateButton != null)
            backFromCreateButton.interactable = interactable;

        if (backFromFindButton != null)
            backFromFindButton.interactable = interactable;

        if (leaveRoomButton != null)
            leaveRoomButton.interactable = interactable;

        if (readyButton != null)
            readyButton.interactable = interactable;
    }

    private void SetJoinInteractable(bool interactable)
    {
        if (joinButton != null)
            joinButton.interactable = interactable;
    }

    private void SetStartInteractable(bool interactable)
    {
        if (startMatchButton != null)
            startMatchButton.interactable = interactable;
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    private void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.AddListener(action);
    }

    private void QuitGame()
    {
        Application.Quit();
    }

    private void OnDestroy()
    {
        if (runner != null)
            runner.RemoveCallbacks(this);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!intentionalLobbyShutdown && runner != null && !runner.IsServer && IsInRoomLobby() && player.PlayerId == lobbyHostPlayerId)
        {
            HandleLobbyDisconnect(ConnectionLostWithHostMessage);
            return;
        }

        if (roomLobbyState != null)
            roomLobbyState.HandlePlayerLeft(player);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        HandleLobbyDisconnect(ConnectionLostWithHostMessage);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        HandleLobbyDisconnect(ConnectionLostWithHostMessage);
    }

    private void HandleLobbyDisconnect(string message)
    {
        if (intentionalLobbyShutdown)
            return;

        if (lobbyShutdownHandled || lobbyDisconnectRecoveryQueued)
            return;

        StartCoroutine(RecoverFromLobbyDisconnect(message));
    }

    private bool ShouldRecoverFromLostLobbyRunner()
    {
        return !intentionalLobbyShutdown &&
               !lobbyShutdownHandled &&
               !lobbyDisconnectRecoveryQueued &&
               IsInRoomLobby() &&
               runner != null &&
               !runner.IsServer &&
               (runner.IsShutdown || !runner.IsRunning);
    }

    private void HandleUnityLogMessage(string condition, string stackTrace, UnityEngine.LogType type)
    {
        if (type != UnityEngine.LogType.Error && type != UnityEngine.LogType.Exception)
            return;

        if (!IsFusionDisconnectMessage(condition))
            return;

        fusionDisconnectMessageReceived = true;
    }

    private void HandleUnityLogMessageThreaded(string condition, string stackTrace, UnityEngine.LogType type)
    {
        if (type != UnityEngine.LogType.Error && type != UnityEngine.LogType.Exception)
            return;

        if (!IsFusionDisconnectMessage(condition))
            return;

        fusionDisconnectMessageReceived = true;
    }

    private bool IsFusionDisconnectMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;

        return message.IndexOf("DisconnectMessage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               message.IndexOf("Server has disconnected", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsInRoomLobby()
    {
        return (roomLobbyPanel != null && roomLobbyPanel.activeInHierarchy) || roomLobbyState != null;
    }

    private void UpdateLobbyHostPlayerId(IReadOnlyList<RoomLobbyState.PlayerLobbyData> players)
    {
        lobbyHostPlayerId = -1;

        if (players == null)
            return;

        foreach (var player in players)
        {
            if (player.IsHost)
            {
                lobbyHostPlayerId = player.PlayerId;
                return;
            }
        }
    }

    private IEnumerator RecoverFromLobbyDisconnect(string message)
    {
        if (lobbyDisconnectRecoveryQueued)
            yield break;

        lobbyShutdownHandled = true;
        lobbyDisconnectRecoveryQueued = true;
        LobbyUI.SetPendingStatusMessage(message);
        SetStatusMessage(message);

        yield return null;
        yield return new WaitForEndOfFrame();

        ResetNetworkState(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(MainMenuSceneBuildIndex, LoadSceneMode.Single);
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) => RebuildSessionList(sessionList);
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        HandleLobbyDisconnect(ConnectionLostWithHostMessage);
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
}
