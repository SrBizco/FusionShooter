using UnityEngine;

public class AudioFeedbackPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float minPitch = 0.95f;
    [SerializeField] private float maxPitch = 1.05f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return;

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(clip, volume);
    }

    public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float minPitch = 0.95f, float maxPitch = 1.05f)
    {
        if (clip == null)
            return;

        var audioObject = new GameObject($"OneShotAudio_{clip.name}");
        audioObject.transform.position = position;

        var source = audioObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.PlayOneShot(clip, volume);

        Destroy(audioObject, clip.length / Mathf.Max(0.01f, source.pitch) + 0.1f);
    }
}
