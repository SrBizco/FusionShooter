using Fusion;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [Networked] public int CurrentHealth { get; private set; }

    private const int MaxHealth = 100;
    private bool isDead = false;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Debug.Log($"🧬 Asignando salud máxima a {gameObject.name}");
            CurrentHealth = MaxHealth;
        }
    }

    public void TakeDamage(int amount, PlayerRef attacker)
    {
        Debug.Log($"💥 TakeDamage llamado en {gameObject.name} con {amount} de daño por {attacker}");

        if (!Object.HasStateAuthority || isDead)
        {
            Debug.Log("⛔ TakeDamage ignorado (sin autoridad o ya muerto)");
            return;
        }

        CurrentHealth -= amount;
        Debug.Log($"❤️ Vida restante de {gameObject.name}: {CurrentHealth}");

        if (CurrentHealth <= 0)
        {
            Die(attacker);
        }
    }

    private void Die(PlayerRef killer)
    {
        isDead = true;
        Debug.Log($"💀 {gameObject.name} murió. Killer: {killer}");

        foreach (var player in Runner.ActivePlayers)
        {
            if (player == killer)
            {
                var obj = Runner.GetPlayerObject(player);
                if (obj == null)
                {
                    Debug.LogWarning($"❌ No se encontró PlayerObject para {player}");
                    continue;
                }

                var stats = obj.GetComponent<PlayerStats>();
                if (stats == null)
                {
                    Debug.LogWarning("❌ PlayerStats no encontrado en PlayerObject");
                    continue;
                }

                stats.Score += 1;
                Debug.Log($"🏅 {killer} sumó 1 punto. Total: {stats.Score}");
            }
        }

        // Ocultar jugador muerto
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;

        if (HasInputAuthority)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.gameObject.SetActive(false);
        }

        GetComponent<CharacterController>().enabled = false;
        GetComponent<NetworkPlayerController>().enabled = false;
        GetComponent<PlayerShooter>().enabled = false;

        // No despawn: permitimos mantener referencia en Scoreboard
        if (Object.HasStateAuthority)
        {
            NetworkManager.Instance.RespawnPlayer(Object.InputAuthority, this);
        }
    }

    public void Revive(Vector3 position)
    {
        isDead = false;
        CurrentHealth = MaxHealth;
        transform.position = position;

        foreach (var renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = true;

        if (HasInputAuthority)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.gameObject.SetActive(true);
        }

        GetComponent<CharacterController>().enabled = true;
        GetComponent<NetworkPlayerController>().enabled = true;
        GetComponent<PlayerShooter>().enabled = true;

        Debug.Log($"🟢 {gameObject.name} revivido en {position}");
    }
}
