using UnityEngine;

public enum PathLinkType
{
    WALK,
    JUMP,
    FALL,
    TELEPORT,
    CUSTOM
}

[DisallowMultipleComponent]
public class PathLink : MonoBehaviour
{
    [Header("Endpoints")]
    public Point a;
    public Point b;

    [Header("Link")]
    public bool bidirectional = true;
    public PathLinkType linkType = PathLinkType.WALK;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (a == null || b == null) return;

        Gizmos.color = linkType switch
        {
            PathLinkType.WALK => new Color(0.2f, 0.9f, 0.2f),
            PathLinkType.JUMP => new Color(0.9f, 0.8f, 0.2f),
            PathLinkType.FALL => new Color(0.2f, 0.6f, 0.95f),
            PathLinkType.TELEPORT => new Color(0.95f, 0.2f, 0.95f),
            _ => new Color(0.8f, 0.8f, 0.8f),
        };

        Vector3 pa = a.transform.position;
        Vector3 pb = b.transform.position;
        Gizmos.DrawLine(pa, pb);

        // petite flèche pour unidirectionnel
        if (!bidirectional)
        {
            var mid = Vector3.Lerp(pa, pb, 0.5f);
            var dir = (pb - pa).normalized;
            var right = Vector3.Cross(dir, Vector3.forward).normalized;
            Gizmos.DrawLine(mid, mid - dir * 0.2f + right * 0.1f);
            Gizmos.DrawLine(mid, mid - dir * 0.2f - right * 0.1f);
        }
    }
#endif
}
