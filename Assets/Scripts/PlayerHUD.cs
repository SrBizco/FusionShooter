using TMPro;
using UnityEngine;

public class PlayerHUD : MonoBehaviour
{
    private TMP_Text healthText;
    private Health health;

    void Start()
    {
        // Buscar el texto por tag en la escena
        var healthObj = GameObject.FindWithTag("HealthText");
        if (healthObj != null)
            healthText = healthObj.GetComponent<TMP_Text>();

        health = GetComponentInParent<Health>(); // Asume que está en el hijo de un player
    }

    void Update()
    {
        if (health == null || healthText == null) return;
        if (!health.HasInputAuthority) return;

        healthText.text = $"Health: {health.CurrentHealth}%";
    }
}
