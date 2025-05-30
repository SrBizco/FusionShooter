using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 MoveDirection; // WASD
    public float Yaw;             // Rotaci�n horizontal del mouse
}