using Fusion;
using UnityEngine;

/// <summary>
/// Datos persistentes por jugador en red.
/// Actualmente sólo maneja el puntaje, pero puede expandirse con muertes, asistencias, etc.
/// </summary>
public class PlayerStats : NetworkBehaviour
{
    [Networked] public int Score { get; set; }
}
