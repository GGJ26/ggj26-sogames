using UnityEngine;

public class ZoneTracker2D : MonoBehaviour
{
    [Header("Debug")]
    public Zone2D CurrentZone;

    private int _insideCount = 0;

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Zone2D z = other.GetComponent<Zone2D>();
        if (z == null) z = other.GetComponentInParent<Zone2D>();
        if (z == null) return;

        _insideCount++;
        CurrentZone = z;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Zone2D z = other.GetComponent<Zone2D>();
        if (z == null) z = other.GetComponentInParent<Zone2D>();
        if (z == null) return;

        _insideCount = Mathf.Max(0, _insideCount - 1);

        if (_insideCount == 0 && CurrentZone == z)
            CurrentZone = null;
    }
}
