using UnityEngine;

public enum SurfaceType
{
    Default,
    Metal,
    Concrete,
    Flesh,
    Wood,
    Dirt
}

public class SurfaceFeedback : MonoBehaviour
{
    [field: SerializeField] public SurfaceType SurfaceType { get; private set; } = SurfaceType.Default;
}
