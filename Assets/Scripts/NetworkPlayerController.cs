using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkPlayerController : NetworkBehaviour
{
    private NetworkCharacterController cc;
    private Health health;

    [Networked]
    private float NetworkYaw { get; set; }

    [Networked]
    public float NetworkMoveSpeed { get; private set; }

    [Networked]
    public float NetworkMoveX { get; private set; }

    [Networked]
    public float NetworkMoveY { get; private set; }

    public override void Spawned()
    {
        cc = GetComponent<NetworkCharacterController>();
        health = GetComponent<Health>();

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = HasInputAuthority ? Color.gray : Color.red;
    }

    public override void FixedUpdateNetwork()
    {
        if (health == null || !health.IsAlive)
        {
            ResetNetworkMovement();
            return;
        }

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

            Vector2 animationMove = input.MoveDirection;
            if (animationMove.sqrMagnitude > 1f)
                animationMove.Normalize();

            NetworkMoveX = animationMove.x;
            NetworkMoveY = animationMove.y;
            NetworkMoveSpeed = move.magnitude;
            cc.Move(move * moveSpeed * Runner.DeltaTime);
        }
        else
        {
            ResetNetworkMovement();
        }

        // Rotación visible para todos
        transform.rotation = Quaternion.Euler(0, NetworkYaw, 0);
    }

    private void ResetNetworkMovement()
    {
        NetworkMoveX = 0f;
        NetworkMoveY = 0f;
        NetworkMoveSpeed = 0f;
    }
}
