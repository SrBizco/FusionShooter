using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
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

    private NetworkRunner runner;
    private RoomLobbyState roomLobbyState;
    private readonly List<LobbySessionEntryUI> spawnedSessionEntries = new();
    private readonly List<GameObject> spawnedPlayerEntries = new();
    private SessionInfo selectedSession;
    private bool localReady;
    private bool isStartingOrJoining;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
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

        ShowHomePanel();
        SetStatusMessage("Ready.");
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
        Transform parent = GetListContent(playerListParent);
        EnsureVerticalList(parent);

        foreach (var player in players)
        {
            if (parent == null)
                continue;

            spawnedPlayerEntries.Add(CreatePlayerRow(parent, player));
        }

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

    private void ShowHomePanel()
    {
        ShowPanel(homePanel);
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
            await runner.Shutdown();

        ResetNetworkState();
        ShowHomePanel();
        SetStatusMessage("Left room.");
    }

    private async void BackFromFindPanel()
    {
        if (runner != null && !isStartingOrJoining)
        {
            await runner.Shutdown();
            ResetNetworkState();
        }

        ShowHomePanel();
    }

    private void ResetNetworkState()
    {
        if (runner != null)
            runner.RemoveCallbacks(this);

        runner = null;
        roomLobbyState = null;
        selectedSession = null;
        localReady = false;
        isStartingOrJoining = false;
        ClearSessionEntries();
        ClearPlayerEntries();
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

    private GameObject CreatePlayerRow(Transform parent, RoomLobbyState.PlayerLobbyData player)
    {
        var row = new GameObject($"PlayerRow_{player.Name}", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 42f);

        var rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.minHeight = 42f;
        rowLayout.preferredHeight = 42f;

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 24f;

        CreateRowText(row.transform, player.IsHost ? $"{player.Name} (Host)" : player.Name, TextAlignmentOptions.Left, Color.white);
        CreateRowText(row.transform, player.IsReady ? "Ready" : "Not Ready", TextAlignmentOptions.Right, player.IsReady ? Color.green : Color.red);

        return row;
    }

    private void CreateRowText(Transform parent, string text, TextAlignmentOptions alignment, Color color)
    {
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = alignment;
        label.fontSize = 28f;
        label.color = color;
        label.raycastTarget = false;

        var layout = textObject.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.preferredHeight = 42f;
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

    private void EnsureVerticalList(Transform content)
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
        if (roomLobbyState != null)
            roomLobbyState.HandlePlayerLeft(player);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        ResetNetworkState();
        ShowHomePanel();
        SetStatusMessage($"Connection closed: {reason}");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        ResetNetworkState();
        ShowHomePanel();
        SetStatusMessage($"Disconnected: {reason}");
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) => RebuildSessionList(sessionList);
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
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
