using UnityEngine;
using Fusion;

public class PlayerShooter : NetworkBehaviour
{
    [SerializeField] private Camera fpsCamera;
    [SerializeField] private float shootDistance = 100f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private WeaponFeedback weaponFeedback;
    [SerializeField] private PlayerAnimationController animationController;

    private Health health;

    public override void Spawned()
    {
        health = GetComponent<Health>();
        ResolveFpsCamera();

        if (weaponFeedback == null)
            weaponFeedback = GetComponentInChildren<WeaponFeedback>();

        if (animationController == null)
            animationController = GetComponent<PlayerAnimationController>();
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
        ResolveFpsCamera();
        if (fpsCamera == null)
        {
            Debug.LogWarning("No FPS camera assigned for shooting.");
            return;
        }

        weaponFeedback?.PlayShot();
        animationController?.PlayFire();

        Ray ray = new Ray(fpsCamera.transform.position, fpsCamera.transform.forward);
        Vector3 impactPoint = ray.origin + ray.direction * shootDistance;
        Vector3 impactNormal = -ray.direction;
        SurfaceType surfaceType = SurfaceType.Default;
        bool hasImpact = false;
        bool hitPlayer = false;

        if (Physics.Raycast(ray, out RaycastHit hit, shootDistance, hitLayers))
        {
            Debug.Log($"✅ Hit registrado en: {hit.collider.name}");
            hasImpact = true;
            impactPoint = hit.point;
            impactNormal = hit.normal;

            if (hit.collider.GetComponentInParent<SurfaceFeedback>() is SurfaceFeedback surfaceFeedback)
                surfaceType = surfaceFeedback.SurfaceType;

            var networkObject = hit.collider.GetComponentInParent<NetworkObject>();
            if (networkObject != null)
            {
                hitPlayer = networkObject.GetComponent<Health>() != null;
                RPC_RequestDamage(networkObject.Id, 10, Object.InputAuthority);
            }
        }

        if (hasImpact)
            weaponFeedback?.PlayImpact(impactPoint, impactNormal, surfaceType);

        if (hitPlayer)
            weaponFeedback?.PlayHitMarker();

        RPC_PlayShotFeedback(impactPoint, impactNormal, hasImpact, (int)surfaceType);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayShotFeedback(Vector3 impactPoint, Vector3 impactNormal, bool hasImpact, int surfaceType)
    {
        if (HasInputAuthority)
            return;

        weaponFeedback?.PlayShot();
        animationController?.PlayFire();

        if (hasImpact)
            weaponFeedback?.PlayImpact(impactPoint, impactNormal, (SurfaceType)surfaceType);
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

    private void ResolveFpsCamera()
    {
        if (fpsCamera != null && fpsCamera.enabled)
            return;

        foreach (var camera in GetComponentsInChildren<Camera>(true))
        {
            if (camera.enabled)
            {
                fpsCamera = camera;
                return;
            }
        }
    }
}
