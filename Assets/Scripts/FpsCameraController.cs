using Fusion;
using UnityEngine;

public class FpsCameraController : NetworkBehaviour
{
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float mouseSensitivity = 1f;
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;
    [SerializeField] private float localCameraNearClip = 0.01f;

    private float pitch;
    public float Pitch => pitch;
    public float Yaw { get; private set; }

    private Health health;

    public override void Spawned()
    {
        health = GetComponent<Health>();

        if (!HasInputAuthority)
        {
            SetOwnedCameraState(false);
            return;
        }

        SetOwnedCameraState(true);
        DisableOtherAudioListeners();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority || health == null || !health.IsAlive)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        Yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
        ApplyPitch();
    }

    private void LateUpdate()
    {
        if (!HasInputAuthority || health == null || !health.IsAlive)
            return;

        ApplyPitch();
    }

    private void SetOwnedCameraState(bool enabled)
    {
        if (cameraHolder == null)
            return;

        foreach (var camera in cameraHolder.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = enabled;
            if (enabled)
                camera.nearClipPlane = localCameraNearClip;
        }

        foreach (var listener in cameraHolder.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = enabled;
    }

    private void DisableOtherAudioListeners()
    {
        if (cameraHolder == null)
            return;

        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (listener.transform.IsChildOf(cameraHolder))
                continue;

            listener.enabled = false;
        }
    }

    private void ApplyPitch()
    {
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
