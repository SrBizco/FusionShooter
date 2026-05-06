using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbySessionEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button selectButton;

    private SessionInfo session;
    private Action<SessionInfo> onSelected;

    public void Setup(SessionInfo sessionInfo, Action<SessionInfo> selectedCallback)
    {
        session = sessionInfo;
        onSelected = selectedCallback;

        if (roomNameText != null)
            roomNameText.text = session.Name;

        if (playerCountText != null)
            playerCountText.text = $"{session.PlayerCount}/{session.MaxPlayers}";

        if (statusText != null)
            statusText.text = session.IsOpen ? "Open" : "Closed";

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(SelectSession);
            selectButton.onClick.AddListener(SelectSession);
            selectButton.interactable = session.IsOpen && session.PlayerCount < session.MaxPlayers;
        }
    }

    private void SelectSession()
    {
        onSelected?.Invoke(session);
    }
}
