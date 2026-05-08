using TMPro;
using UnityEngine;

public class PlayerHUDView : MonoBehaviour
{
    public static PlayerHUDView Instance { get; private set; }

    [field: SerializeField] public TMP_Text HealthText { get; private set; }
    [field: SerializeField] public TMP_Text AmmoText { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
