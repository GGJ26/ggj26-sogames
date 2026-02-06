using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
public class Path : MonoBehaviour
{
    [Header("Roots")]
    public string anchorsRootName = "Anchors";
    public string generatedRootName = "GeneratedPoints";

    [Header("Multi-Chains")]
    [Tooltip("Préfixe des sous-dossiers de chaines sous Anchors / GeneratedPoints.")]
    public string chainPrefix = "Chain_";
    [Tooltip("Nom de la chain legacy si des anchors sont directement sous Anchors.")]
    public string legacyChainName = "Chain_00";

    [Header("Generation")]
    [Tooltip("Inclure un Point exactement sur chaque anchor.")]
    public bool includeAnchorsAsPoints = true;

    [Header("Point defaults")]
    public bool defaultWalkable = true;
    private PointType defaultPointType = PointType.GROUNDED;

    [Tooltip("Si true, les points d’un segment prennent le segmentType de l’anchor de départ.")]
    public bool useAnchorSegmentType = true;

    [Header("CircleCollider2D radius constraints")]
    [Min(0f)] public float radiusMin = 0.15f;
    [Min(0f)] public float radiusMax = 0.35f;

    [Tooltip("Si true, tous les points d'un segment ont le même radius, calculé pour combler l'espace.")]
    public bool uniformRadiusPerSegment = true;

    [Header("Runtime (read)")]
    public List<Point> points = new List<Point>();

    public MaskItem maskItem;

    // --------------------------------------------------------------------
    // PointType overrides (persistent across RebuildNow)
    // --------------------------------------------------------------------
    [Serializable]
    public struct PointTypeOverride
    {
        public Vector3 position;
        public PointType type;
    }

    [Header("PointType Overrides (persistent)")]
    [Tooltip("Overrides persistants basés sur la position (en world). Permet d'éditer les points générés sans perdre au Rebuild.")]
    public List<PointTypeOverride> typeOverrides = new List<PointTypeOverride>();

    [Tooltip("Tolérance de matching en world units pour retrouver un override par position.")]
    public float overrideMatchTolerance = 0.05f;

    private Transform _anchorsRoot;
    private Transform _generatedRoot;

    private void OnEnable()
    {
        EnsureRoots();
        RebuildNow();
    }

    private void OnValidate()
    {
        radiusMin = Mathf.Max(0f, radiusMin);
        radiusMax = Mathf.Max(radiusMin, radiusMax);
        overrideMatchTolerance = Mathf.Max(0.001f, overrideMatchTolerance);

        EnsureRoots();
        RebuildNow();
    }

    public void EnsureRoots()
    {
        _anchorsRoot = transform.Find(anchorsRootName);
        if (_anchorsRoot == null)
        {
            var go = new GameObject(anchorsRootName);
            go.transform.SetParent(transform, false);
            _anchorsRoot = go.transform;
        }

        _generatedRoot = transform.Find(generatedRootName);
        if (_generatedRoot == null)
        {
            var go = new GameObject(generatedRootName);
            go.transform.SetParent(transform, false);
            _generatedRoot = go.transform;
        }
    }

    /// <summary>
    /// Retourne toutes les chaines d'anchors (multi-chemins).
    /// - Si Anchors contient des sous-dossiers (Chain_XX), on les utilise.
    /// - Si Anchors contient des PathAnchor directement, ils forment la chain legacy (Chain_00).
    /// </summary>
    public List<(string chainName, List<PathAnchor> anchors)> GetAnchorChainsOrdered()
    {
        EnsureRoots();
        var result = new List<(string chainName, List<PathAnchor> anchors)>();

        if (_anchorsRoot == null) return result;

        // 1) Legacy: anchors directement sous Anchors
        var legacy = _anchorsRoot.GetComponentsInChildren<PathAnchor>(true)
            .Where(a => a != null && a.transform.parent == _anchorsRoot)
            .OrderBy(a => a.transform.GetSiblingIndex())
            .ToList();

        if (legacy.Count > 0)
            result.Add((legacyChainName, legacy));

        // 2) Chaines: sous-dossiers sous Anchors
        for (int i = 0; i < _anchorsRoot.childCount; i++)
        {
            var child = _anchorsRoot.GetChild(i);
            if (child == null) continue;

            // ignore legacy anchors (PathAnchor) at root level
            if (child.GetComponent<PathAnchor>() != null) continue;

            var list = child.GetComponentsInChildren<PathAnchor>(true)
                .Where(a => a != null && a.transform.parent == child)
                .OrderBy(a => a.transform.GetSiblingIndex())
                .ToList();

            if (list.Count > 0)
                result.Add((child.name, list));
        }

        // Dédupe par nom (si legacyChainName identique à un dossier existant, on garde le dossier)
        // => on retire la legacy si un dossier du même nom existe.
        var hasFolderSame = result.Any(r => r.chainName == legacyChainName && _anchorsRoot.Find(legacyChainName) != null);
        if (hasFolderSame)
            result = result.Where(r => r.chainName != legacyChainName || _anchorsRoot.Find(legacyChainName) != null).ToList();

        return result;
    }

    /// <summary>Crée/retourne un root de chaine sous Anchors.</summary>
    public Transform EnsureChainRoot(string chainName)
    {
        EnsureRoots();
        if (_anchorsRoot == null) return null;

        var t = _anchorsRoot.Find(chainName);
        if (t == null)
        {
            var go = new GameObject(chainName);
            go.transform.SetParent(_anchorsRoot, false);
            t = go.transform;
        }
        return t;
    }

    /// <summary>Crée/retourne un root de chaine sous GeneratedPoints.</summary>
    public Transform EnsureGeneratedChainRoot(string chainName)
    {
        EnsureRoots();
        if (_generatedRoot == null) return null;

        var t = _generatedRoot.Find(chainName);
        if (t == null)
        {
            var go = new GameObject(chainName);
            go.transform.SetParent(_generatedRoot, false);
            t = go.transform;
        }
        return t;
    }

    [ContextMenu("Rebuild Now")]
    public void RebuildNow()
    {
        EnsureRoots();

        var chains = GetAnchorChainsOrdered();

        // Nettoie tout et regénère
        ClearGenerated();
        points.Clear();

        bool hasAnyValidChain = false;
        foreach (var ch in chains)
        {
            if (ch.anchors == null || ch.anchors.Count < 2) continue;
            hasAnyValidChain = true;

            var genChain = EnsureGeneratedChainRoot(ch.chainName);
            GeneratePointsFromAnchors(ch.anchors, genChain);
        }

        if (!hasAnyValidChain)
        {
            ClearGenerated();
            points.Clear();
            return;
        }

        RefreshPointsList();
        BuildGraph();
    }

    private void ClearGenerated()
    {
        if (_generatedRoot == null) return;

        for (int i = _generatedRoot.childCount - 1; i >= 0; i--)
        {
            var child = _generatedRoot.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    private void GeneratePointsFromAnchors(List<PathAnchor> anchors, Transform generatedChainRoot)
    {
        if (generatedChainRoot == null) return;

        // 1) Build samples per segment
        var samples = new List<(Vector3 pos, PointType type, float radius)>();

        for (int seg = 0; seg < anchors.Count - 1; seg++)
        {
            Vector3 a = anchors[seg].transform.position;
            Vector3 b = anchors[seg + 1].transform.position;

            PointType segType = useAnchorSegmentType ? anchors[seg].segmentType : defaultPointType;

            // radius choisi: soit uniform par segment (pour combler), soit interpolation entre min/max.
            float dist = Vector3.Distance(a, b);
            float radius;

            if (uniformRadiusPerSegment)
            {
                // Nombre de points approximatif pour combler avec un petit overlap
                float targetDiameter = Mathf.Clamp(dist / 6f, radiusMin * 2f, radiusMax * 2f);
                radius = Mathf.Clamp(targetDiameter * 0.5f, radiusMin, radiusMax);
            }
            else
            {
                radius = Mathf.Lerp(radiusMin, radiusMax, 0.5f);
            }

            float step = Mathf.Max(0.001f, radius * 1.8f); // overlap léger
            int count = Mathf.Max(2, Mathf.CeilToInt(dist / step) + 1);

            for (int i = 0; i < count; i++)
            {
                float t = (count <= 1) ? 0f : (i / (float)(count - 1));
                Vector3 p = Vector3.Lerp(a, b, t);

                // évite doublons (fin d'un segment == début du suivant)
                if (samples.Count > 0)
                {
                    if ((samples[^1].pos - p).sqrMagnitude < 0.000001f)
                        continue;
                }

                // option: exclure anchors exact si includeAnchorsAsPoints false
                if (!includeAnchorsAsPoints)
                {
                    if ((p - a).sqrMagnitude < 0.000001f) continue;
                    if ((p - b).sqrMagnitude < 0.000001f) continue;
                }

                samples.Add((p, segType, radius));
            }
        }

        // 2) Spawn Points
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];

            var go = new GameObject($"P_{i:000}");
            go.transform.SetParent(generatedChainRoot, true);
            go.transform.position = s.pos;

            var point = go.AddComponent<Point>();
            point.walkable = defaultWalkable;

            // Applique override si existant
            if (TryGetOverrideType(s.pos, out var overrideType))
                point.pointType = overrideType;
            else
                point.pointType = s.type;

            point.EnsureCollider();

            if (point.circle != null)
                point.circle.radius = s.radius;
        }
    }

    private bool TryGetOverrideType(Vector3 worldPos, out PointType type)
    {
        float tol2 = overrideMatchTolerance * overrideMatchTolerance;
        for (int i = 0; i < typeOverrides.Count; i++)
        {
            var o = typeOverrides[i];
            if ((o.position - worldPos).sqrMagnitude <= tol2)
            {
                type = o.type;
                return true;
            }
        }

        type = default;
        return false;
    }

    private void RefreshPointsList()
    {
        points = (_generatedRoot != null)
            ? _generatedRoot.GetComponentsInChildren<Point>(true).ToList()
            : GetComponentsInChildren<Point>(true).ToList();
    }

    // --------------------------------------------------------------------
    // Graph / Navigation
    // --------------------------------------------------------------------

    [ContextMenu("Build Graph")]
    public void BuildGraph()
    {
        EnsureRoots();
        if (points == null) RefreshPointsList();

        // clear
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null) continue;
            points[i].neighbors.Clear();
        }

        if (_generatedRoot == null) return;

        // 1) connexions intra-chaine (i <-> i+1)
        for (int c = 0; c < _generatedRoot.childCount; c++)
        {
            var chainRoot = _generatedRoot.GetChild(c);
            if (chainRoot == null) continue;

            var chainPoints = chainRoot.GetComponentsInChildren<Point>(true)
                .Where(p => p != null && p.transform.parent == chainRoot)
                .OrderBy(p => p.transform.GetSiblingIndex())
                .ToList();

            for (int i = 0; i < chainPoints.Count - 1; i++)
            {
                Link(chainPoints[i], chainPoints[i + 1], bidirectional: true);
            }
        }

        // 2) PathLinks explicites (sous ce Path)
        var links = GetComponentsInChildren<PathLink>(true);
        foreach (var l in links)
        {
            if (l == null || !l.isActiveAndEnabled) continue;
            if (l.a == null || l.b == null) continue;

            Link(l.a, l.b, l.bidirectional);
        }
    }

    private static void Link(Point a, Point b, bool bidirectional)
    {
        if (a == null || b == null) return;
        if (!a.neighbors.Contains(b)) a.neighbors.Add(b);
        if (bidirectional)
        {
            if (!b.neighbors.Contains(a)) b.neighbors.Add(a);
        }
    }

    /// <summary>
    /// BFS simple sur le graphe. Retourne un chemin incluant start et goal.
    /// </summary>
    public bool TryFindPath(Point start, Point goal, List<Point> outPath, bool allowNonWalkable = false)
    {
        outPath?.Clear();
        if (start == null || goal == null) return false;
        if (start == goal)
        {
            outPath?.Add(start);
            return true;
        }

        // si graph pas construit (ex: en play, mais pas rebuild)
        if (start.neighbors == null || start.neighbors.Count == 0)
            BuildGraph();

        var q = new Queue<Point>();
        var prev = new Dictionary<Point, Point>(256);
        var visited = new HashSet<Point>();

        bool IsOk(Point p) => p != null && (allowNonWalkable || p.walkable);

        if (!IsOk(start) || !IsOk(goal)) return false;

        visited.Add(start);
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == null) continue;

            var neigh = cur.neighbors;
            for (int i = 0; i < neigh.Count; i++)
            {
                var n = neigh[i];
                if (!IsOk(n)) continue;
                if (visited.Contains(n)) continue;

                visited.Add(n);
                prev[n] = cur;

                if (n == goal)
                {
                    Reconstruct(goal, prev, outPath);
                    return true;
                }

                q.Enqueue(n);
            }
        }

        return false;
    }

    private static void Reconstruct(Point goal, Dictionary<Point, Point> prev, List<Point> outPath)
    {
        outPath.Clear();
        var cur = goal;
        outPath.Add(cur);

        while (prev.TryGetValue(cur, out var p))
        {
            cur = p;
            outPath.Add(cur);
        }

        outPath.Reverse();
    }
}
