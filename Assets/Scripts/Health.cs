using Fusion;
using UnityEngine;

public class Health : NetworkBehaviour
{
    private const int MaxHealth = 100;

    [Networked] public int CurrentHealth { get; private set; }
    [Networked] public bool IsAlive { get; private set; } = true;
    [Networked] private TickTimer RespawnTimer { get; set; }

    private CharacterController characterController;
    private NetworkPlayerController movement;
    private PlayerShooter shooter;
    private Camera playerCamera;

    private bool visualsApplied = false;

    public override void Spawned()
    {
        characterController = GetComponent<CharacterController>();
        movement = GetComponent<NetworkPlayerController>();
        shooter = GetComponent<PlayerShooter>();
        playerCamera = GetComponentInChildren<Camera>(true);

        if (Object.HasStateAuthority)
        {
            Debug.Log($"🧬 Asignando salud máxima a {gameObject.name}");
            CurrentHealth = MaxHealth;
            IsAlive = true;
        }
    }

    public override void Render()
    {
        if (IsAlive && !visualsApplied)
        {
            ApplyVisualState(true);
            visualsApplied = true;
        }
        else if (!IsAlive && visualsApplied)
        {
            ApplyVisualState(false);
            visualsApplied = false;
        }
    }

    public void TakeDamage(int amount, PlayerRef attacker)
    {
        if (!Object.HasStateAuthority || !IsAlive) return;

        CurrentHealth -= amount;
        Debug.Log($"💥 {gameObject.name} recibió {amount} de daño. Vida restante: {CurrentHealth}");

        if (CurrentHealth <= 0)
        {
            Kill(attacker);
        }
    }

    private void Kill(PlayerRef killer)
    {
        Debug.Log($"💀 {gameObject.name} murió. Killer: {killer}");
        IsAlive = false;
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, 5f);

        if (Runner.GetPlayerObject(killer)?.GetComponent<PlayerStats>() is PlayerStats stats)
        {
            stats.Score += 1;
            Debug.Log($"🏅 {killer} suma 1 punto. Total: {stats.Score}");
        }
    }

    public void Revive(Vector3 position)
    {
        Debug.Log($"🟢 Reviviendo a {gameObject.name} en {position}");

        transform.position = position;
        CurrentHealth = MaxHealth;
        IsAlive = true;
    }

    private void ApplyVisualState(bool alive)
    {
        // Movimiento, cámara y disparo
        if (characterController != null) characterController.enabled = alive;
        if (movement != null) movement.enabled = alive;
        if (shooter != null) shooter.enabled = alive;

        // Render
        foreach (var rend in GetComponentsInChildren<Renderer>())
            rend.enabled = alive;

        // Cámara y UI
        if (HasInputAuthority)
        {
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(alive);

            Cursor.lockState = alive ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !alive;

            if (alive)
                DeathUIController.Instance.HideDeathPanel();
            else
                DeathUIController.Instance.ShowDeathPanel();
        }

        // Animación de muerte
        transform.rotation = alive
            ? Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0)
            : Quaternion.Euler(90, transform.rotation.eulerAngles.y, 0);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestRespawn()
    {
        Debug.Log("📩 Cliente solicitó respawn al host");

        if (!Object.HasStateAuthority)
            return;

        if (IsAlive)
        {
            Debug.LogWarning("⚠️ El jugador ya está vivo, no se necesita respawn");
            return;
        }

        Vector3 respawnPos = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
        Revive(respawnPos);
    }
    public void ForceStopGameplay()
    {
        if (characterController != null) characterController.enabled = false;
        if (movement != null) movement.enabled = false;
        if (shooter != null) shooter.enabled = false;

        foreach (var rend in GetComponentsInChildren<Renderer>())
            rend.enabled = true;

        if (HasInputAuthority)
        {
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Debug.Log($"⛔ Gameplay desactivado para {gameObject.name}");
    }
}
