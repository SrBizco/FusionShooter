using UnityEngine;
using Fusion;

public class PlayerShooter : NetworkBehaviour
{
    [SerializeField] private Camera fpsCamera;
    [SerializeField] private float shootDistance = 100f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private WeaponFeedback weaponFeedback;
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private int magazineSize = 7;
    [SerializeField] private float reloadDuration = 2f;

    private Health health;

    [Networked] public int CurrentAmmo { get; private set; }
    [Networked] public bool IsReloading { get; private set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    public int MagazineSize => magazineSize;

    public override void Spawned()
    {
        health = GetComponent<Health>();
        ResolveFpsCamera();

        if (weaponFeedback == null)
            weaponFeedback = GetComponentInChildren<WeaponFeedback>();

        if (animationController == null)
            animationController = GetComponent<PlayerAnimationController>();

        if (Object.HasStateAuthority)
            CurrentAmmo = magazineSize;
    }

    void Update()
    {
        if (PauseMenuUI.IsPaused)
            return;

        if (HasInputAuthority && Input.GetKeyDown(KeyCode.R) && CanRequestReload())
        {
            weaponFeedback?.PlayReload();
            RPC_RequestReload();
        }

        // Solo permitir disparar si tiene autoridad de entrada Y el jugador está vivo (según red)
        if (HasInputAuthority && Input.GetButtonDown("Fire1") && health != null && health.IsAlive)
        {
            Shoot();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && IsReloading && ReloadTimer.Expired(Runner))
            FinishReload();
    }

    private void Shoot()
    {
        if (CurrentAmmo <= 0 || IsReloading)
            return;

        ResolveFpsCamera();
        if (fpsCamera == null)
        {
            Debug.LogWarning("No FPS camera assigned for shooting.");
            return;
        }

        weaponFeedback?.PlayShot();
        animationController?.PlayFire();
        RPC_ConsumeAmmo();

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

            surfaceType = ResolveSurfaceType(hit.collider);

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

    private bool CanRequestReload()
    {
        return !IsReloading && CurrentAmmo < magazineSize;
    }

    private SurfaceType ResolveSurfaceType(Collider hitCollider)
    {
        if (hitCollider == null)
            return SurfaceType.Default;

        if (hitCollider.GetComponentInParent<SurfaceFeedback>() is SurfaceFeedback surfaceFeedback)
            return surfaceFeedback.SurfaceType;

        string layerName = LayerMask.LayerToName(hitCollider.gameObject.layer);
        if (System.Enum.TryParse(layerName, true, out SurfaceType layerSurfaceType))
            return layerSurfaceType;

        return SurfaceType.Default;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ConsumeAmmo()
    {
        if (IsReloading || CurrentAmmo <= 0)
            return;

        CurrentAmmo--;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestReload()
    {
        if (IsReloading || CurrentAmmo >= magazineSize)
            return;

        IsReloading = true;
        ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadDuration);
        RPC_PlayReloadFeedback();
    }

    private void FinishReload()
    {
        CurrentAmmo = magazineSize;
        IsReloading = false;
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayReloadFeedback()
    {
        if (HasInputAuthority)
            return;

        weaponFeedback?.PlayReload();
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
