using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TriggerPoint : MonoBehaviour
{
    [Header("Runtime")]
    public Point currentPoint;
    public Point lastPoint;

    /// <summary>
    /// Appelé quand on entre dans un Point.
    /// (newPoint, previousPoint)
    /// </summary>
    public event Action<Point, Point> OnEnterPoint;

    [Header("Debug")]
    public bool logOnEnter = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Point directement sur le collider
        Point p = other.GetComponent<Point>();
        if (p == null) p = other.GetComponentInParent<Point>();
        if (p == null) return;

        // Si on re-rentre dans le même point, on ignore (optionnel)
        if (p == currentPoint) return;

        lastPoint = currentPoint;
        currentPoint = p;

        if (logOnEnter)
            Debug.Log($"[{name}] Enter Point: {p.name} (type={p.pointType})", this);

        OnEnterPoint?.Invoke(currentPoint, lastPoint);
    }
}
