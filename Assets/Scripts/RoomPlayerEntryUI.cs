using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomPlayerEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text readyStateText;
    [SerializeField] private Image readyStateImage;
    [SerializeField] private Color waitingColor = Color.red;
    [SerializeField] private Color readyColor = Color.green;

    public void Setup(string playerName, bool isReady, bool isHost)
    {
        if (playerNameText != null)
            playerNameText.text = isHost ? $"{playerName} (Host)" : playerName;

        if (readyStateText != null)
            readyStateText.text = isReady ? "Listo" : "En espera";

        if (readyStateImage != null)
            readyStateImage.color = isReady ? readyColor : waitingColor;
    }
}
