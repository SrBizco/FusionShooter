using Fusion;
using UnityEngine;

public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }

    [Networked] public TickTimer GameTimer { get; private set; }
    [Networked] public int ReplicatedSeconds { get; private set; }
    [Networked] public bool IsGameActive { get; private set; }

    private bool localEnded = false;
    public bool IsSpawned { get; private set; }

    public override void Spawned()
    {
        IsSpawned = true;
        Instance = this;
        Debug.Log($"🧬 GameState Spawned. HasStateAuthority: {HasStateAuthority}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        IsSpawned = false;

        if (Instance == this)
            Instance = null;
    }

    private void OnDestroy()
    {
        IsSpawned = false;

        if (Instance == this)
            Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !IsGameActive) return;

        if (GameTimer.IsRunning)
        {
            float timeLeft = GameTimer.RemainingTime(Runner) ?? 0f;
            ReplicatedSeconds = Mathf.CeilToInt(timeLeft);

            if (timeLeft <= 0f)
            {
                Debug.Log("🏁 Timer expirado. Fin del juego.");
                IsGameActive = false;
            }
        }
    }

    public override void Render()
    {
        if (!IsGameActive && !localEnded)
        {
            localEnded = true;
            Debug.Log("🧱 Ejecutando fin de juego local");
            EndGameLocal();
        }
    }

    private void EndGameLocal()
    {
        foreach (var player in Runner.ActivePlayers)
        {
            var obj = Runner.GetPlayerObject(player);
            if (obj != null && obj.TryGetComponent(out Health health))
            {
                health.ForceStopGameplay();
            }
        }

        if (ScoreboardUI.Instance != null)
        {
            ScoreboardUI.Instance.ShowFinalScoreboard();
        }
    }

    public void StartGameTimer(float durationSeconds)
    {
        if (!HasStateAuthority || GameTimer.IsRunning || IsGameActive)
            return;

        GameTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
        IsGameActive = true;
        Debug.Log("🕒 Timer iniciado manualmente por NetworkManager");
    }

    public void NotifyHostReturningToMenu()
    {
        if (!HasStateAuthority)
            return;

        RPC_ReturnClientsToMenu("Connection lost.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReturnClientsToMenu(string message)
    {
        if (HasStateAuthority)
            return;

        NetworkManager.ReturnToMenuFromHost(message);
    }
}
