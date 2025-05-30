using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using Fusion.Sockets;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class LobbyUI : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private NetworkRunner runnerPrefab;

    public static string PlayerName { get; private set; }

    private void Start()
    {
        hostButton.onClick.AddListener(() => StartGame(GameMode.Host));
        joinButton.onClick.AddListener(() => StartGame(GameMode.Client));
    }

    private async void StartGame(GameMode mode)
    {
        PlayerName = string.IsNullOrWhiteSpace(nameInput.text) ? "SinNombre" : nameInput.text;

        statusText.text = $"⏳ Conectando como {mode}...";

        var runner = Instantiate(runnerPrefab);
        runner.name = "LobbyRunner";
        DontDestroyOnLoad(runner.gameObject);
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = "SalaTest",
            PlayerCount = 2,
            Scene = SceneRef.FromIndex(1), // Asumimos GameScene está en el index 1
            SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        statusText.text = result.Ok ? "✅ Conectado!" : $"❌ Error: {result.ShutdownReason}";
    }

    // Obligatorio para evitar errores
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    }
