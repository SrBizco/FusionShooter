using System.Collections.Generic;
using Fusion;

public enum MatchMode
{
    FreeForAll,
    TeamDeathmatch
}

public enum PlayerTeam
{
    None,
    Blue,
    Red
}

public static class MatchSettings
{
    public static MatchMode CurrentMode { get; private set; } = MatchMode.FreeForAll;
    private static readonly Dictionary<PlayerRef, PlayerTeam> playerTeams = new();

    public static void SetMode(MatchMode mode)
    {
        CurrentMode = mode;
    }

    public static void ClearTeams()
    {
        playerTeams.Clear();
    }

    public static void SetPlayerTeam(PlayerRef player, PlayerTeam team)
    {
        if (player == PlayerRef.None)
            return;

        playerTeams[player] = team;
    }

    public static bool TryGetPlayerTeam(PlayerRef player, out PlayerTeam team)
    {
        return playerTeams.TryGetValue(player, out team);
    }

    public static bool FriendlyFireBlocked(PlayerTeam attackerTeam, PlayerTeam targetTeam)
    {
        return CurrentMode == MatchMode.TeamDeathmatch &&
               attackerTeam != PlayerTeam.None &&
               attackerTeam == targetTeam;
    }
}
