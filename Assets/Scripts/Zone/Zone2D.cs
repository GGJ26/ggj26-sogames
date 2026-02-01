using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class Zone2D : MonoBehaviour
{
    [Header("Spawn points")]
    [Tooltip("Optionnel : parent contenant les spawnPoints. Si null, on prend les enfants directs de ce GameObject.")]
    public Transform spawnRoot;

    [Tooltip("Spawn points récupérés automatiquement à l'Awake.")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Debug")]
    public bool drawGizmos = true;
    public float gizmoRadius = 0.2f;

    private PolygonCollider2D _poly;

    private void Awake()
    {
        _poly = GetComponent<PolygonCollider2D>();
        _poly.isTrigger = true;

        // RefreshSpawnPoints();
    }

    [ContextMenu("Refresh Spawn Points")]
    public void RefreshSpawnPoints()
    {
        spawnPoints.Clear();

        Transform root = (spawnRoot != null) ? spawnRoot : transform;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform t = root.GetChild(i);
            spawnPoints.Add(t);
        }
    }

    public Transform GetClosestSpawnPoint(Vector2 referencePos)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
            return null;

        Transform best = null;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            Transform sp = spawnPoints[i];
            if (sp == null) continue;

            float d2 = ((Vector2)sp.position - referencePos).sqrMagnitude;
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = sp;
            }
        }

        return best;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.cyan;
        if (spawnPoints != null)
        {
            foreach (var sp in spawnPoints)
            {
                if (sp == null) continue;
                Gizmos.DrawWireSphere(sp.position, gizmoRadius);
            }
        }
    }
}
