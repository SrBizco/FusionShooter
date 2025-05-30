using Fusion;
using UnityEngine;

/// <summary>
/// Datos enviados desde el cliente local hacia el host en cada tick.
/// Incluye movimiento y rotaci�n del mouse.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    public Vector2 MoveDirection;  // Movimiento en plano XZ (WASD)
    public float Yaw;              // Rotaci�n horizontal acumulada del mouse
}
