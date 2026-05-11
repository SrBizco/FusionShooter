using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private int mainMenuSceneBuildIndex = 0;

    public static ScoreboardUI Instance { get; private set; }

    private bool matchEnded;

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

        if (returnToMenuButton != null)
        {
            returnToMenuButton.gameObject.SetActive(false);
            returnToMenuButton.onClick.AddListener(ReturnToMenu);
        }
    }

    void Update()
    {
        if (matchEnded)
            return;

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
                text.text = $"{p.name} - {p.score}";
        }
    }

    public void ShowFinalScoreboard()
    {
        matchEnded = true;
        scoreboardPanel.SetActive(true);

        if (returnToMenuButton != null)
        {
            returnToMenuButton.gameObject.SetActive(true);
            returnToMenuButton.interactable = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshScoreboard();
    }

    private async void ReturnToMenu()
    {
        if (returnToMenuButton != null)
            returnToMenuButton.interactable = false;

        var runner = NetworkManager.Instance != null ? NetworkManager.Instance.runner : null;
        if (runner != null && runner.IsRunning)
        {
            if (runner.IsServer && GameState.Instance != null && GameState.Instance.IsSpawned)
            {
                GameState.Instance.NotifyHostReturningToMenu();
                await System.Threading.Tasks.Task.Delay(300);
            }

            NetworkManager.MarkIntentionalMenuReturn();
            await runner.Shutdown();
        }

        MatchSettings.SetMode(MatchMode.FreeForAll);
        MatchSettings.ClearTeams();
        SceneManager.LoadScene(mainMenuSceneBuildIndex);
    }
}
