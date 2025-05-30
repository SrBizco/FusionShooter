using Fusion;
using UnityEngine;

public class FpsCameraController : NetworkBehaviour
{
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float mouseSensitivity = 1f;
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;

    private float pitch;
    public float Yaw { get; private set; } // <- Esta propiedad pública es clave

    public override void Spawned()
    {
        if (!HasInputAuthority)
        {
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.enabled = false;
                var listener = cam.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;
            }

            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        Yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
