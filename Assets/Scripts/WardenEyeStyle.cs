using UnityEngine;

[CreateAssetMenu(menuName = "Ghost Lava/Warden Eye Style")]
public sealed class WardenEyeStyle : ScriptableObject
{
    public string styleId;
    [Tooltip("Meshes are in the local space of the eye sockets. The sockets remain animated when the mesh changes.")]
    public Mesh leftMesh;
    public Mesh rightMesh;
}
