using System.Collections;
using UnityEngine;

public class WeaponFeedback : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceImpactFeedback
    {
        public SurfaceType surfaceType = SurfaceType.Default;
        public GameObject impactPrefab;
        public AudioClip impactSound;
    }

    [Header("References")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private AudioFeedbackPlayer audioPlayer;

    [Header("Shot")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private float shootVolume = 1f;

    [Header("Reload")]
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private float reloadVolume = 1f;

    [Header("Impact")]
    [SerializeField] private GameObject defaultImpactPrefab;
    [SerializeField] private AudioClip defaultImpactSound;
    [SerializeField] private SurfaceImpactFeedback[] surfaceImpacts;
    [SerializeField] private float impactVolume = 1f;
    [SerializeField] private float impactLifetime = 3f;

    [Header("Local UI")]
    [SerializeField] private GameObject hitMarker;
    [SerializeField] private float hitMarkerDuration = 0.08f;

    private Coroutine hitMarkerRoutine;

    private void Awake()
    {
        if (audioPlayer == null)
            audioPlayer = GetComponent<AudioFeedbackPlayer>();

        if (hitMarker != null)
            hitMarker.SetActive(false);
    }

    public void PlayShot()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play(true);

        if (muzzleFlashPrefab != null && muzzlePoint != null)
        {
            var flash = Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation);
            Destroy(flash, 2f);
        }

        if (audioPlayer != null)
            audioPlayer.PlayOneShot(shootSound, shootVolume);
        else if (muzzlePoint != null)
            AudioFeedbackPlayer.PlayClipAtPoint(shootSound, muzzlePoint.position, shootVolume);
    }

    public void PlayReload()
    {
        if (audioPlayer != null)
            audioPlayer.PlayOneShot(reloadSound, reloadVolume);
        else if (muzzlePoint != null)
            AudioFeedbackPlayer.PlayClipAtPoint(reloadSound, muzzlePoint.position, reloadVolume);
    }

    public void PlayImpact(Vector3 position, Vector3 normal, SurfaceType surfaceType)
    {
        GetImpactFeedback(surfaceType, out GameObject impactPrefab, out AudioClip impactSound);

        if (impactPrefab != null)
        {
            Quaternion rotation = normal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            var impact = Instantiate(impactPrefab, position, rotation);
            Destroy(impact, impactLifetime);
        }

        AudioFeedbackPlayer.PlayClipAtPoint(impactSound, position, impactVolume);
    }

    public void PlayHitMarker()
    {
        if (hitMarker == null)
            return;

        if (hitMarkerRoutine != null)
            StopCoroutine(hitMarkerRoutine);

        hitMarkerRoutine = StartCoroutine(ShowHitMarkerRoutine());
    }

    private IEnumerator ShowHitMarkerRoutine()
    {
        hitMarker.SetActive(true);
        yield return new WaitForSeconds(hitMarkerDuration);
        hitMarker.SetActive(false);
        hitMarkerRoutine = null;
    }

    private void GetImpactFeedback(SurfaceType surfaceType, out GameObject impactPrefab, out AudioClip impactSound)
    {
        impactPrefab = defaultImpactPrefab;
        impactSound = defaultImpactSound;

        if (surfaceImpacts == null)
            return;

        foreach (var feedback in surfaceImpacts)
        {
            if (feedback != null && feedback.surfaceType == surfaceType)
            {
                if (feedback.impactPrefab != null)
                    impactPrefab = feedback.impactPrefab;

                if (feedback.impactSound != null)
                    impactSound = feedback.impactSound;

                return;
            }
        }
    }
}
