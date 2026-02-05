using UnityEngine;

public class PathAnchor : MonoBehaviour
{
    public PointType segmentType = PointType.GROUNDED;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.85f, 0.2f, 1f); // violet pour anchors
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.25f);
    }
#endif
}
