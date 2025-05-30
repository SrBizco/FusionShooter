using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject entryPrefab;

    private Dictionary<PlayerRef, GameObject> playerEntries = new();

    void Start()
    {
        scoreboardPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            UpdateScoreboard(true);
        else if (Input.GetKeyUp(KeyCode.Tab))
            UpdateScoreboard(false);
    }

    void UpdateScoreboard(bool show)
    {
        scoreboardPanel.SetActive(show);
        if (!show) return;

        // Limpiar entries anteriores
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        List<(string name, int score)> players = new();

        var runner = NetworkManager.Instance.runner;
        foreach (var player in runner.ActivePlayers)
        {
            var obj = runner.GetPlayerObject(player); // ✅ CORREGIDO

            if (obj == null) continue;

            var stats = obj.GetComponent<PlayerStats>();
            if (stats == null) continue;

            string playerName = obj.name;
            int score = stats.Score;

            players.Add((playerName, score));
        }

        // Ordenar de mayor a menor
        players.Sort((a, b) => b.score.CompareTo(a.score));

        // Crear nuevas filas
        foreach (var p in players)
        {
            var entry = Instantiate(entryPrefab, contentParent);
            entry.GetComponentInChildren<TMP_Text>().text = $"{p.name} — {p.score}";
        }
    }
}
