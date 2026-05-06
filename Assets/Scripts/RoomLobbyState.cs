using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomLobbyState : NetworkBehaviour
{
    public struct PlayerLobbyData
    {
        public PlayerRef Player;
        public string Name;
        public bool IsReady;
        public bool IsHost;
    }

    public static RoomLobbyState Instance { get; private set; }

    [SerializeField] private int gameplaySceneBuildIndex = 1;

    private readonly List<PlayerLobbyData> players = new();
    private readonly List<PlayerLobbyData> snapshotPlayers = new();

    public bool LocalPlayerReady { get; private set; }
    public bool CanHostStart => HasStateAuthority && players.Count > 0 && AllPlayersReady();

    public override void Spawned()
    {
        Instance = this;

        if (LobbyUI.Instance != null)
            LobbyUI.Instance.OnRoomLobbyStateReady(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterLocalPlayer(string playerName)
    {
        RPC_RequestRegisterPlayer(Runner.LocalPlayer, playerName);
    }

    public void SetLocalReady(bool isReady)
    {
        LocalPlayerReady = isReady;
        RPC_RequestReadyState(Runner.LocalPlayer, isReady);
    }

    public void TryStartGame()
    {
        if (!HasStateAuthority)
            return;

        if (!AllPlayersReady())
        {
            RPC_SetStatus("Todos los jugadores deben estar listos.");
            return;
        }

        Runner.SessionInfo.IsOpen = false;
        Runner.SessionInfo.IsVisible = false;
        Runner.LoadScene(SceneRef.FromIndex(gameplaySceneBuildIndex), LoadSceneMode.Single);
    }

    public void HandlePlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        int index = players.FindIndex(p => p.Player == player);
        if (index >= 0)
        {
            players.RemoveAt(index);
            BroadcastPlayers();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRegisterPlayer(PlayerRef player, string playerName)
    {
        if (player == PlayerRef.None)
            return;

        int index = players.FindIndex(p => p.Player == player);
        bool isHost = player == Runner.LocalPlayer;

        var data = new PlayerLobbyData
        {
            Player = player,
            Name = string.IsNullOrWhiteSpace(playerName) ? "SinNombre" : playerName,
            IsReady = false,
            IsHost = isHost
        };

        if (index >= 0)
            players[index] = data;
        else
            players.Add(data);

        BroadcastPlayers();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestReadyState(PlayerRef player, bool isReady)
    {
        if (player == PlayerRef.None)
            return;

        int index = players.FindIndex(p => p.Player == player);
        if (index < 0)
            return;

        var data = players[index];
        data.IsReady = isReady;
        players[index] = data;
        BroadcastPlayers();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetStatus(string message)
    {
        if (LobbyUI.Instance != null)
            LobbyUI.Instance.SetStatusMessage(message);
    }

    private void BroadcastPlayers()
    {
        RPC_ReceivePlayerSnapshot(BuildPlayerSnapshot());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReceivePlayerSnapshot(string snapshot)
    {
        snapshotPlayers.Clear();

        if (!string.IsNullOrWhiteSpace(snapshot))
        {
            string[] rows = snapshot.Split('\n');
            foreach (string row in rows)
            {
                if (string.IsNullOrWhiteSpace(row))
                    continue;

                string[] columns = row.Split('|');
                if (columns.Length < 3)
                    continue;

                snapshotPlayers.Add(new PlayerLobbyData
                {
                    Player = PlayerRef.None,
                    Name = columns[0],
                    IsReady = columns[1] == "1",
                    IsHost = columns[2] == "1"
                });
            }
        }

        if (LobbyUI.Instance != null)
            LobbyUI.Instance.RefreshRoomPlayers(snapshotPlayers);
    }

    private string BuildPlayerSnapshot()
    {
        var rows = new List<string>(players.Count);
        foreach (var player in players)
        {
            string safeName = Sanitize(player.Name);
            string ready = player.IsReady ? "1" : "0";
            string host = player.IsHost ? "1" : "0";
            rows.Add($"{safeName}|{ready}|{host}");
        }

        return string.Join("\n", rows);
    }

    private string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "SinNombre";

        return value.Replace("|", "/").Replace("\n", " ").Replace("\r", " ");
    }

    private bool AllPlayersReady()
    {
        if (players.Count == 0)
            return false;

        foreach (var player in players)
        {
            if (!player.IsReady)
                return false;
        }

        return true;
    }
}
