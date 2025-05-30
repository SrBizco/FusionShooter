using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeathUIController : MonoBehaviour
{
    public static DeathUIController Instance;

    [SerializeField] private GameObject deathPanel;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Button respawnButton;

    private float respawnTime = 5f;
    private float timer;
    private bool isDead = false;
    private bool isWaiting = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        deathPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isDead || !isWaiting) return;

        timer -= Time.deltaTime;
        int remaining = Mathf.Max(0, Mathf.CeilToInt(timer));
        timerText.text = $"Time to next respawn: {remaining}";

        if (timer <= 0f)
        {
            respawnButton.interactable = true;
            isWaiting = false;
        }
    }

    public void ShowDeathPanel()
    {
        deathPanel.SetActive(true);
        timer = respawnTime;
        isDead = true;
        isWaiting = true;

        respawnButton.interactable = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void HideDeathPanel()
    {
        deathPanel.SetActive(false);
        isDead = false;
        isWaiting = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnRespawnButtonPressed()
    {
        if (!respawnButton.interactable) return;

        Debug.Log("🔁 Botón de respawn presionado");

        // Buscar solo el objeto del jugador local con autoridad
        var all = FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var h in all)
        {
            if (h.HasInputAuthority)
            {
                h.RPC_RequestRespawn();
                break;
            }
        }
    }
}
