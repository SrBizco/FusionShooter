﻿using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject entryPrefab;
    public static ScoreboardUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    void Start()
    {
        scoreboardPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            scoreboardPanel.SetActive(true);
            RefreshScoreboard();
        }
        else if (Input.GetKeyUp(KeyCode.Tab))
        {
            scoreboardPanel.SetActive(false);
        }
    }

    private void RefreshScoreboard()
    {
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        var runner = NetworkManager.Instance.runner;
        List<(string name, int score)> players = new();

        foreach (var player in runner.ActivePlayers)
        {
            var obj = runner.GetPlayerObject(player);
            if (obj == null) continue;

            var stats = obj.GetComponent<PlayerStats>();
            if (stats == null) continue;

            players.Add((stats.PlayerName, stats.Score));
        }

        players.Sort((a, b) => b.score.CompareTo(a.score));

        foreach (var p in players)
        {
            var entry = Instantiate(entryPrefab, contentParent);
            var text = entry.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = $"{p.name} — {p.score}";
        }
    }
    public void ShowFinalScoreboard()
    {
        scoreboardPanel.SetActive(true);
        RefreshScoreboard();
    }
}
