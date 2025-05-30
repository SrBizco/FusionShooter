using Fusion;
using UnityEngine;

public class PlayerStats : NetworkBehaviour
{
    [Networked] public int Score { get; set; }
    [Networked] public string PlayerName { get; set; }

    public override void Spawned()
    {
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
}
