using UnityEngine;

public class TargetPoint : MonoBehaviour
{
    [Header("Identity")]
    public int id = 0; // numéro dans le chemin (1,2,3... ou 0,1,2...)

    [Header("Spawn")]
    public Vector2 spawnOffset = new Vector2(0f, 0.5f);
    public int level = 0;
    public bool canTeleport = false;

    public Vector2 SpawnWorldPos()
    {
        Vector3 worldOffset = transform.TransformVector((Vector3)spawnOffset);
        return (Vector2)(transform.position + worldOffset);
    }
}
