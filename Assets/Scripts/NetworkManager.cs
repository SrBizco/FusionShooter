using Fusion;
using Fusion.Sockets;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private NetworkObject gameStatePrefab;
    [SerializeField] private TMP_Text gameTimerText;
    [SerializeField, Min(1f)] private float matchDurationSeconds = 60f;

    public NetworkRunner runner;
    private FpsCameraController cameraController;
    private NetworkObject gameStateInstance;
    private bool gameplayInitializationQueued;
    private bool isShuttingDown;
    private bool menuReturnQueued;
    private bool fusionDisconnectMessageReceived;
    private static bool intentionalMenuReturn;

    public static NetworkManager Instance { get; private set; }
    private Dictionary<PlayerRef, Health> deadPlayers = new();

    private const int MainMenuSceneBuildIndex = 0;
    private const string ConnectionLostMessage = "Connection lost with host.";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            intentionalMenuReturn = false;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null)
        {
            runner.AddCallbacks(this);
            QueueGameplayInitialization();
        }
        else
        {
            Debug.LogError("❌ No se encontró un NetworkRunner en la escena.");
        }

        EnsurePauseMenu();
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

    private void Update()
    {
        if (fusionDisconnectMessageReceived)
        {
            fusionDisconnectMessageReceived = false;
            if (runner == null || !runner.IsServer)
                HandleNetworkShutdown(true);
        }

        if (ShouldRecoverFromLostGameplayRunner())
            HandleNetworkShutdown(true);

        if (isShuttingDown)
            return;

        var gameState = GameState.Instance;
        if (gameState == null || !gameState.IsSpawned || !gameState.IsGameActive)
            return;

        int remaining = gameState.ReplicatedSeconds;
        int minutes = remaining / 60;
        int seconds = remaining % 60;

        if (gameTimerText != null)
            gameTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        QueueGameplayInitialization();
    }

    private void TryInitializeGameplayForExistingPlayers()
    {
        if (runner == null || !runner.IsServer)
            return;

        EnsureGameState();

        foreach (var player in runner.ActivePlayers)
            SpawnPlayerIfNeeded(player);

        StartTimerIfNeeded();
    }

    private void QueueGameplayInitialization()
    {
        if (isShuttingDown || gameplayInitializationQueued)
            return;

        gameplayInitializationQueued = true;
        StartCoroutine(InitializeGameplayWhenRunnerReady());
    }

    private IEnumerator InitializeGameplayWhenRunnerReady()
    {
        yield return null;

        yield return new WaitUntil(() =>
            runner != null &&
            runner.IsRunning &&
            !isShuttingDown &&
            !runner.IsSceneManagerBusy);

        yield return null;

        gameplayInitializationQueued = false;
        TryInitializeGameplayForExistingPlayers();
    }

    private void EnsureGameState()
    {
        if (gameStateInstance == null)
        {
            gameStateInstance = runner.Spawn(gameStatePrefab, Vector3.zero, Quaternion.identity);
            Debug.Log("🧩 GameState instanciado por el host");
        }
    }

    private void SpawnPlayerIfNeeded(PlayerRef player)
    {
        if (runner.GetPlayerObject(player) != null)
            return;

        Vector3 spawnPos = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
        var playerInstance = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
        runner.SetPlayerObject(player, playerInstance);
    }

    private void StartTimerIfNeeded()
    {
        var gameState = gameStateInstance != null ? gameStateInstance.GetComponent<GameState>() : GameState.Instance;
        if (gameState != null && !gameState.GameTimer.IsRunning)
        {
            gameState.StartGameTimer(matchDurationSeconds);
        }
    }

    public void RegisterDeadPlayer(PlayerRef player, Health health)
    {
        if (!deadPlayers.ContainsKey(player))
        {
            deadPlayers[player] = health;
            Debug.Log($"☠️ Jugador {player} registrado como muerto");
        }
    }

    public void TryRespawn(PlayerRef player)
    {
        if (!runner.IsServer)
        {
            Debug.LogWarning("❌ TryRespawn solo debe ser ejecutado por el host");
            return;
        }

        if (deadPlayers.TryGetValue(player, out var health))
        {
            Vector3 respawnPos = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
            health.Revive(respawnPos);
            deadPlayers.Remove(player);

            Debug.Log($"✅ Jugador {player} respawneado");
        }
        else
        {
            Debug.LogWarning($"⚠️ No se encontró jugador {player} para respawn");
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        cameraController = ResolveLocalCameraController(runner);

        Vector2 move = PauseMenuUI.IsPaused
            ? Vector2.zero
            : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        float yaw = cameraController != null ? cameraController.Yaw : 0f;

        input.Set(new NetworkInputData { MoveDirection = move, Yaw = yaw });
    }

    private void EnsurePauseMenu()
    {
        if (FindAnyObjectByType<PauseMenuUI>() == null)
            gameObject.AddComponent<PauseMenuUI>();
    }

    private FpsCameraController ResolveLocalCameraController(NetworkRunner runner)
    {
        if (cameraController != null && cameraController.HasInputAuthority)
            return cameraController;

        if (runner != null)
        {
            var playerObject = runner.GetPlayerObject(runner.LocalPlayer);
            if (playerObject != null && playerObject.TryGetComponent(out FpsCameraController localCameraController))
                return localCameraController;
        }

        return null;
    }

    public static void MarkIntentionalMenuReturn()
    {
        intentionalMenuReturn = true;
    }

    public static void ReturnToMenuFromHost(string message)
    {
        if (Instance != null)
            Instance.StartCoroutine(Instance.ReturnToMenuAfterHostRequest(message));
    }

    private IEnumerator ReturnToMenuAfterHostRequest(string message)
    {
        if (menuReturnQueued)
            yield break;

        menuReturnQueued = true;
        intentionalMenuReturn = true;
        isShuttingDown = true;
        gameplayInitializationQueued = false;
        gameStateInstance = null;
        cameraController = null;

        MatchSettings.SetMode(MatchMode.FreeForAll);
        MatchSettings.ClearTeams();
        LobbyUI.SetPendingStatusMessage(string.IsNullOrWhiteSpace(message) ? ConnectionLostMessage : message);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var runnerToShutdown = runner;
        if (runnerToShutdown != null)
        {
            runnerToShutdown.RemoveCallbacks(this);

            if (runnerToShutdown.IsRunning)
            {
                var shutdownTask = runnerToShutdown.Shutdown();
                while (!shutdownTask.IsCompleted)
                    yield return null;
            }

            runner = null;
            Destroy(runnerToShutdown.gameObject);
        }

        yield return null;
        SceneManager.LoadScene(MainMenuSceneBuildIndex, LoadSceneMode.Single);
    }

    private void HandleNetworkShutdown(bool connectionLost)
    {
        isShuttingDown = true;
        gameplayInitializationQueued = false;
        gameStateInstance = null;
        cameraController = null;

        if (menuReturnQueued)
            return;

        StopAllCoroutines();

        if (intentionalMenuReturn || !connectionLost)
            return;

        StartCoroutine(ReturnToMainMenuAfterConnectionLoss());
    }

    private bool ShouldRecoverFromLostGameplayRunner()
    {
        return !intentionalMenuReturn &&
               !menuReturnQueued &&
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

    private IEnumerator ReturnToMainMenuAfterConnectionLoss()
    {
        if (menuReturnQueued)
            yield break;

        menuReturnQueued = true;
        MatchSettings.SetMode(MatchMode.FreeForAll);
        MatchSettings.ClearTeams();
        LobbyUI.SetPendingStatusMessage(ConnectionLostMessage);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return null;
        yield return new WaitForEndOfFrame();

        var runnerToDestroy = runner;
        runner = null;

        if (runnerToDestroy != null)
        {
            runnerToDestroy.RemoveCallbacks(this);
            Destroy(runnerToDestroy.gameObject);
        }

        SceneManager.LoadScene(MainMenuSceneBuildIndex, LoadSceneMode.Single);
    }

    // Callbacks requeridos
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        HandleNetworkShutdown(true);
    }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        HandleNetworkShutdown(true);
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        HandleNetworkShutdown(true);
    }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { QueueGameplayInitialization(); }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    private void OnDestroy()
    {
        isShuttingDown = true;
        StopAllCoroutines();

        if (runner != null)
            runner.RemoveCallbacks(this);

        if (Instance == this)
            Instance = null;
    }
}

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private int mainMenuSceneBuildIndex = 0;

    public static bool IsPaused { get; private set; }

    private GameObject pausePanel;
    private Button continueButton;
    private Button leaveMatchButton;
    private bool isLeavingMatch;

    private void Awake()
    {
        IsPaused = false;
        CreatePauseMenu();
    }

    private void Update()
    {
        if (isLeavingMatch)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            SetPaused(!IsPaused);
    }

    public void Continue()
    {
        SetPaused(false);
    }

    public async void LeaveMatch()
    {
        if (isLeavingMatch)
            return;

        isLeavingMatch = true;
        if (leaveMatchButton != null)
            leaveMatchButton.interactable = false;
        if (continueButton != null)
            continueButton.interactable = false;

        SetPaused(false, keepCursorUnlocked: true);

        var runner = NetworkManager.Instance != null ? NetworkManager.Instance.runner : null;
        if (runner != null && runner.IsRunning)
        {
            if (runner.IsServer && GameState.Instance != null && GameState.Instance.IsSpawned)
            {
                GameState.Instance.NotifyHostReturningToMenu();
                await System.Threading.Tasks.Task.Delay(300);
            }

            NetworkManager.MarkIntentionalMenuReturn();
            await runner.Shutdown();
        }

        MatchSettings.SetMode(MatchMode.FreeForAll);
        MatchSettings.ClearTeams();
        SceneManager.LoadScene(mainMenuSceneBuildIndex);
    }

    private void SetPaused(bool paused, bool keepCursorUnlocked = false)
    {
        IsPaused = paused;

        if (pausePanel != null)
            pausePanel.SetActive(paused);

        bool shouldUnlockCursor = paused || keepCursorUnlocked || !IsLocalPlayerAlive();
        Cursor.lockState = shouldUnlockCursor
            ? CursorLockMode.None
            : CursorLockMode.Locked;
        Cursor.visible = shouldUnlockCursor;
    }

    private void CreatePauseMenu()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            canvas = CreateCanvas();

        pausePanel = new GameObject("PauseMenuPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pausePanel.transform.SetParent(canvas.transform, false);

        var panelRect = pausePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var background = pausePanel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);

        var window = CreateWindow(pausePanel.transform);
        CreateLabel(window, "Paused", 42, new Vector2(0f, 90f), Color.white);
        continueButton = CreateButton(window, "Continue", new Vector2(0f, 10f), Continue);
        leaveMatchButton = CreateButton(window, "Leave Match", new Vector2(0f, -60f), LeaveMatch);

        pausePanel.SetActive(false);
    }

    private Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("PauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        return canvas;
    }

    private Transform CreateWindow(Transform parent)
    {
        var window = new GameObject("Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        window.transform.SetParent(parent, false);

        var rect = window.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 280f);

        var image = window.GetComponent<Image>();
        image.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

        return window.transform;
    }

    private bool IsLocalPlayerAlive()
    {
        var runner = NetworkManager.Instance != null ? NetworkManager.Instance.runner : null;
        var playerObject = runner != null ? runner.GetPlayerObject(runner.LocalPlayer) : null;

        return playerObject == null ||
               !playerObject.TryGetComponent(out Health health) ||
               health.IsAlive;
    }

    private void CreateLabel(Transform parent, string text, float fontSize, Vector2 anchoredPosition, Color color)
    {
        var label = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(parent, false);

        var rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(320f, 60f);

        var tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
    }

    private Button CreateButton(Transform parent, string text, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObject = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(260f, 52f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);

        var button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        CreateLabel(buttonObject.transform, text, 26f, Vector2.zero, new Color(0.08f, 0.08f, 0.08f, 1f));

        return button;
    }

    private void OnDestroy()
    {
        IsPaused = false;
    }
}
