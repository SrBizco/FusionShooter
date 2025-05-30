using Fusion;
using Fusion.Sockets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject playerPrefab;

    public NetworkRunner runner;
    private FpsCameraController cameraController;

    public static NetworkManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        StartGame();
    }

    private async void StartGame()
    {
        runner = Instantiate(runnerPrefab);
        runner.name = "NetworkRunner";
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = "SalaTest",
            PlayerCount = 2,
            Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex),
            SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        Debug.Log($"StartGame result: {result.Ok}");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (cameraController == null)
            cameraController = FindAnyObjectByType<FpsCameraController>();

        Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        float yaw = cameraController != null ? cameraController.Yaw : 0f;

        input.Set(new NetworkInputData { MoveDirection = move, Yaw = yaw });
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Vector3 spawnPos = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
            NetworkObject playerInstance = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            runner.SetPlayerObject(player, playerInstance);
        }
    }

    public void RespawnPlayer(PlayerRef player, Health existingHealth)
    {
        StartCoroutine(RespawnCoroutine(player, existingHealth));
    }

    private IEnumerator RespawnCoroutine(PlayerRef player, Health healthToRevive)
    {
        yield return new WaitForSeconds(5f);

        if (runner != null && healthToRevive != null)
        {
            Vector3 respawnPos = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
            healthToRevive.Revive(respawnPos);
        }
    }

    // Callbacks no usados
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
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
}
