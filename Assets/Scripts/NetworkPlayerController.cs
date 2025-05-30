using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkPlayerController : NetworkBehaviour
{
    private NetworkCharacterController cc;

    [SerializeField] private Transform cameraHolder;

    [Networked]
    private float NetworkYaw { get; set; }

    public override void Spawned()
    {
        cc = GetComponent<NetworkCharacterController>();

        if (HasInputAuthority && Camera.main != null)
        {
            Camera.main.transform.SetParent(cameraHolder);
            Camera.main.transform.localPosition = Vector3.zero;
            Camera.main.transform.localRotation = Quaternion.identity;
        }

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = HasInputAuthority ? Color.gray : Color.red;
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData input))
        {
            NetworkYaw = input.Yaw;

            float moveSpeed = 5f;
            Quaternion yawRotation = Quaternion.Euler(0, NetworkYaw, 0);
            Vector3 forward = yawRotation * Vector3.forward;
            Vector3 right = yawRotation * Vector3.right;

            Vector3 move = forward * input.MoveDirection.y + right * input.MoveDirection.x;

            if (move.sqrMagnitude > 1f)
                move.Normalize();

            cc.Move(move * moveSpeed * Runner.DeltaTime);
        }

        // Rotación visible para todos
        transform.rotation = Quaternion.Euler(0, NetworkYaw, 0);
    }
}
