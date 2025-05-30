using UnityEngine;
using Fusion;

public class PlayerShooter : NetworkBehaviour
{
    [SerializeField] private Camera fpsCamera;
    [SerializeField] private float shootDistance = 100f;
    [SerializeField] private LayerMask hitLayers;

    public override void Spawned()
    {
        Debug.Log($"🔁 Spawned: {gameObject.name} | InputAuthority: {HasInputAuthority} | StateAuthority: {HasStateAuthority}");

        // Solo desactivo si no soy ni local ni host (para que el host también procese RPCs)
        if (!HasInputAuthority && !Object.HasStateAuthority)
        {
            enabled = false;
            Debug.Log($"❌ Desactivado {gameObject.name}");
        }
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
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
                Debug.Log($"📤 Enviando RPC con target ID: {networkObject.Id}");
                RPC_RequestDamage(networkObject.Id, 10, Object.InputAuthority);
            }
            else
            {
                Debug.LogWarning("❌ No se encontró NetworkObject en el objetivo");
            }
        }
        else
        {
            Debug.Log("❌ Raycast no impactó nada");
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
