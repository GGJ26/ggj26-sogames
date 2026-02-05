using System.Collections.Generic;
using UnityEngine;

public enum PointType
{
    GROUNDED,
    JUMP_VERTICAL,
    JUMP_HORIZONTAL,
    FLY
}

[DisallowMultipleComponent]
public class Point : MonoBehaviour
{
    public PointType pointType = PointType.GROUNDED;
    public bool walkable = true;

    [Header("Collider")]
    public CircleCollider2D circle;

    // Runtime graph neighbors (non-sérialisé)
    [System.NonSerialized] public readonly List<Point> neighbors = new List<Point>(4);

    private void Reset() => EnsureCollider();
    private void OnValidate() => EnsureCollider();

    public void EnsureCollider()
    {
        if (circle == null) circle = GetComponent<CircleCollider2D>();
        if (circle == null) circle = gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
    }
}
