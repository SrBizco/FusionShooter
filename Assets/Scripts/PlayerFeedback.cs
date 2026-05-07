using UnityEngine;

public class PlayerFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioFeedbackPlayer audioPlayer;

    [Header("Damage")]
    [SerializeField] private GameObject damageVfxPrefab;
    [SerializeField] private AudioClip damageSound;

    [Header("Death")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private AudioClip deathSound;

    [Header("Respawn")]
    [SerializeField] private GameObject respawnVfxPrefab;
    [SerializeField] private AudioClip respawnSound;

    private void Awake()
    {
        if (audioPlayer == null)
            audioPlayer = GetComponent<AudioFeedbackPlayer>();
    }

    public void PlayDamage()
    {
        SpawnVfx(damageVfxPrefab);
        PlaySound(damageSound);
    }

    public void PlayDeath()
    {
        SpawnVfx(deathVfxPrefab);
        PlaySound(deathSound);
    }

    public void PlayRespawn()
    {
        SpawnVfx(respawnVfxPrefab);
        PlaySound(respawnSound);
    }

    private void SpawnVfx(GameObject prefab)
    {
        if (prefab == null)
            return;

        var vfx = Instantiate(prefab, transform.position, Quaternion.identity);
        Destroy(vfx, 3f);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioPlayer != null)
            audioPlayer.PlayOneShot(clip);
        else
            AudioFeedbackPlayer.PlayClipAtPoint(clip, transform.position);
    }
}
