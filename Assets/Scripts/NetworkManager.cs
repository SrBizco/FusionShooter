using Fusion;
using Fusion.Sockets;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private NetworkObject gameStatePrefab;
    [SerializeField] private TMP_Text gameTimerText;

    public NetworkRunner runner;
    private FpsCameraController cameraController;
    private NetworkObject gameStateInstance;
    private bool gameplayInitializationQueued;

    public static NetworkManager Instance { get; private set; }
    private Dictionary<PlayerRef, Health> deadPlayers = new();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
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
    }

    private void Update()
    {
        if (GameState.Instance == null || !GameState.Instance.IsGameActive) return;

        int remaining = GameState.Instance.ReplicatedSeconds;
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
        if (gameplayInitializationQueued)
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
            gameState.StartGameTimer(60f); // Cambiar a 600f para 10 minutos reales
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

        Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        float yaw = cameraController != null ? cameraController.Yaw : 0f;

        input.Set(new NetworkInputData { MoveDirection = move, Yaw = yaw });
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

    // Callbacks requeridos
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
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
}
