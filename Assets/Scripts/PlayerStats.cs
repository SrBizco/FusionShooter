using Fusion;
using UnityEngine;

public class PlayerStats : NetworkBehaviour
{
    [Networked] public int Score { get; set; }
    [Networked] public string PlayerName { get; set; }
    [Networked] public PlayerTeam Team { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            Team = ResolveTeam();

        if (HasInputAuthority)
        {
            RPC_SetPlayerName(LobbyUI.PlayerName);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerName(string name)
    {
        PlayerName = name;
        Debug.Log($"📛 Nombre asignado por cliente: {name}");
    }

    private PlayerTeam AssignTeam()
    {
        int blueCount = 0;
        int redCount = 0;

        foreach (var player in Runner.ActivePlayers)
        {
            var playerObject = Runner.GetPlayerObject(player);
            if (playerObject == null || !playerObject.TryGetComponent(out PlayerStats stats))
                continue;

            if (stats == this)
                continue;

            if (stats.Team == PlayerTeam.Blue)
                blueCount++;
            else if (stats.Team == PlayerTeam.Red)
                redCount++;
        }

        return blueCount <= redCount ? PlayerTeam.Blue : PlayerTeam.Red;
    }

    private PlayerTeam ResolveTeam()
    {
        if (MatchSettings.CurrentMode != MatchMode.TeamDeathmatch)
            return PlayerTeam.None;

        if (MatchSettings.TryGetPlayerTeam(Object.InputAuthority, out PlayerTeam team) && team != PlayerTeam.None)
            return team;

        return AssignTeam();
    }
}
