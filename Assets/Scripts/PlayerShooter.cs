using UnityEngine;
using Fusion;

public class PlayerShooter : NetworkBehaviour
{
    [SerializeField] private Camera fpsCamera;
    [SerializeField] private float shootDistance = 100f;
    [SerializeField] private LayerMask hitLayers;

    private Health health;

    public override void Spawned()
    {
        health = GetComponent<Health>();
    }

    void Update()
    {
        // Solo permitir disparar si tiene autoridad de entrada Y el jugador está vivo (según red)
        if (HasInputAuthority && Input.GetButtonDown("Fire1") && health != null && health.IsAlive)
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        Ray ray = new Ray(fpsCamera.transform.position, fpsCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, shootDistance, hitLayers))
        {
            Debug.Log($"✅ Hit registrado en: {hit.collider.name}");

            var networkObject = hit.collider.GetComponentInParent<NetworkObject>();
            if (networkObject != null)
            {
                RPC_RequestDamage(networkObject.Id, 10, Object.InputAuthority);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDamage(NetworkId targetId, int amount, PlayerRef attacker)
    {
        Debug.Log($"📡 RPC_RequestDamage recibido. Atacante: {attacker} | Objetivo ID: {targetId}");

        var target = Runner.FindObject(targetId);
        if (target != null && target.TryGetComponent(out Health health))
        {
            Debug.Log($"🧠 Llamando a TakeDamage de {target.name}");
            health.TakeDamage(amount, attacker);
        }
        else
        {
            Debug.LogWarning("⚠️ No se encontró Health en el objetivo o target nulo");
        }
    }
}
