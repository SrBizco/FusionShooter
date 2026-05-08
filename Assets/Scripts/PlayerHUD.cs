using TMPro;
using UnityEngine;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text ammoText;

    private Health health;
    private PlayerShooter shooter;

    void Awake()
    {
        health = GetComponentInParent<Health>();
        shooter = GetComponentInParent<PlayerShooter>();
    }

    void Update()
    {
        if (health == null || !health.HasInputAuthority)
            return;

        ResolveSceneTexts();

        UpdateHealthText();
        UpdateAmmoText();
    }

    private void ResolveSceneTexts()
    {
        if (PlayerHUDView.Instance == null)
            return;

        if (healthText == null)
            healthText = PlayerHUDView.Instance.HealthText;

        if (ammoText == null)
            ammoText = PlayerHUDView.Instance.AmmoText;
    }

    private void UpdateHealthText()
    {
        if (healthText != null)
            healthText.text = $"Health: {health.CurrentHealth}%";
    }

    private void UpdateAmmoText()
    {
        if (ammoText == null)
            return;

        if (shooter == null)
        {
            ammoText.text = "Ammo: --";
            return;
        }

        ammoText.text = shooter.IsReloading
            ? "Reloading..."
            : $"Ammo: {shooter.CurrentAmmo}/{shooter.MagazineSize}";
    }
}
