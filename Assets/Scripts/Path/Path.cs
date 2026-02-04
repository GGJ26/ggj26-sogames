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
    public float overrideMatchTolerance = 0.15f;

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

        _generatedRoot = transform.Find(generatedRootName);
        if (_generatedRoot == null)
        {
            var go = new GameObject(generatedRootName);
            go.transform.SetParent(transform, false);
            _generatedRoot = go.transform;
        }
    }

    [ContextMenu("Rebuild Now")]
    public void RebuildNow()
    {
        EnsureRoots();
        var anchors = GetAnchorsOrdered();

        if (anchors.Count < 2)
        {
            ClearGenerated();
            points.Clear();
            return;
        }

        GeneratePointsFromAnchors(anchors);
        RefreshPointsList();
    }

    private List<PathAnchor> GetAnchorsOrdered()
    {
        IEnumerable<PathAnchor> anchors;

        if (_anchorsRoot != null)
            anchors = _anchorsRoot.GetComponentsInChildren<PathAnchor>(true);
        else
            anchors = GetComponentsInChildren<PathAnchor>(true);

        anchors = anchors.Where(a => a != null && _generatedRoot != null && !a.transform.IsChildOf(_generatedRoot));

        // ordre = sibling index (si sous Anchors)
        if (_anchorsRoot != null)
            return anchors.Where(a => a.transform.parent == _anchorsRoot).OrderBy(a => a.transform.GetSiblingIndex()).ToList();

        // fallback
        return anchors.OrderBy(a => a.transform.GetSiblingIndex()).ToList();
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

    private void GeneratePointsFromAnchors(List<PathAnchor> anchors)
    {
        // 1) Build samples per segment
        var samples = new List<(Vector3 pos, PointType type, float radius)>();

        for (int seg = 0; seg < anchors.Count - 1; seg++)
        {
            Vector3 a = anchors[seg].transform.position;
            Vector3 b = anchors[seg + 1].transform.position;
            float dist = Vector3.Distance(a, b);

            if (dist < 0.0001f)
                continue;

            PointType segType = useAnchorSegmentType ? anchors[seg].segmentType : defaultPointType;

            // --- Choose count & radius for THIS segment so circles "fill" the distance ---
            int count = 2; // at least endpoints
            float radius;

            // Prefer as few points as possible while keeping radius <= radiusMax
            int tentative = Mathf.FloorToInt(dist / (2f * Mathf.Max(0.0001f, radiusMax))) + 1;
            count = Mathf.Max(2, tentative);

            float spacing = dist / (count - 1);
            radius = spacing * 0.5f;

            // If radius got too small, enforce min radius (will create overlap, still ok)
            if (radius < radiusMin)
            {
                radius = radiusMin;
                count = Mathf.CeilToInt(dist / (2f * Mathf.Max(0.0001f, radiusMin))) + 1;
                count = Mathf.Max(2, count);
                spacing = dist / (count - 1);
            }
            else
            {
                radius = Mathf.Min(radius, radiusMax);
            }

            // Add points along segment
            for (int i = 0; i < count; i++)
            {
                float t = (count <= 1) ? 0f : (float)i / (count - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                samples.Add((p, segType, radius));
            }
        }

        // 2) Remove near-duplicates (anchors shared between segments)
        samples = RemoveNearDuplicates(samples, 0.0001f);

        // 3) Apply to generated children
        EnsureGeneratedChildrenCount(samples.Count);

        for (int i = 0; i < samples.Count; i++)
        {
            Point pt = GetGeneratedPointChild(i);
            if (pt == null) continue;

            pt.transform.position = samples[i].pos;
            pt.walkable = defaultWalkable;

            // Default type from generation...
            PointType computedType = samples[i].type;

            // ...but override if any (persistent)
            if (TryGetOverride(pt.transform.position, out PointType forcedType))
                pt.pointType = forcedType;
            else
                pt.pointType = computedType;

            pt.EnsureCollider();
            pt.circle.radius = uniformRadiusPerSegment
                ? samples[i].radius
                : Mathf.Clamp(samples[i].radius, radiusMin, radiusMax);

            pt.gameObject.name = $"Point_{i:000}_{pt.pointType}";
            pt.transform.SetSiblingIndex(i);
        }
    }

    private List<(Vector3 pos, PointType type, float radius)> RemoveNearDuplicates(List<(Vector3 pos, PointType type, float radius)> src, float eps)
    {
        var res = new List<(Vector3 pos, PointType type, float radius)>();
        float eps2 = eps * eps;

        for (int i = 0; i < src.Count; i++)
        {
            if (res.Count == 0)
            {
                res.Add(src[i]);
                continue;
            }

            var last = res[res.Count - 1];
            if ((src[i].pos - last.pos).sqrMagnitude > eps2)
                res.Add(src[i]);
        }
        return res;
    }

    private void EnsureGeneratedChildrenCount(int needed)
    {
        if (_generatedRoot == null) return;

        // direct children only
        var existing = new List<Point>();
        for (int i = 0; i < _generatedRoot.childCount; i++)
        {
            var p = _generatedRoot.GetChild(i).GetComponent<Point>();
            if (p != null) existing.Add(p);
        }

        // delete extra
        for (int i = existing.Count - 1; i >= needed; i--)
        {
            var go = existing[i].gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(go);
            else Destroy(go);
#else
            Destroy(go);
#endif
        }

        // create missing
        for (int i = existing.Count; i < needed; i++)
        {
            var go = new GameObject("Point_" + i.ToString("000"));
            go.transform.SetParent(_generatedRoot, false);
            var pt = go.AddComponent<Point>();
            pt.pointType = defaultPointType;
            pt.walkable = defaultWalkable;
            pt.EnsureCollider();
        }
    }

    private Point GetGeneratedPointChild(int index)
    {
        if (_generatedRoot == null) return null;

        int found = -1;
        for (int i = 0; i < _generatedRoot.childCount; i++)
        {
            var child = _generatedRoot.GetChild(i);
            var p = child.GetComponent<Point>();
            if (p == null) continue;
            found++;
            if (found == index) return p;
        }
        return null;
    }

    private void RefreshPointsList()
    {
        points.Clear();
        if (_generatedRoot == null) return;

        for (int i = 0; i < _generatedRoot.childCount; i++)
        {
            var p = _generatedRoot.GetChild(i).GetComponent<Point>();
            if (p != null) points.Add(p);
        }
    }

    // --------------------------------------------------------------------
    // Overrides API (used by the editor tool)
    // --------------------------------------------------------------------
    public bool TryGetOverride(Vector3 pos, out PointType t)
    {
        float tol2 = overrideMatchTolerance * overrideMatchTolerance;

        for (int i = 0; i < typeOverrides.Count; i++)
        {
            if ((typeOverrides[i].position - pos).sqrMagnitude <= tol2)
            {
                t = typeOverrides[i].type;
                return true;
            }
        }

        t = default;
        return false;
    }

    public void SetOverride(Vector3 pos, PointType t)
    {
        float tol2 = overrideMatchTolerance * overrideMatchTolerance;

        for (int i = 0; i < typeOverrides.Count; i++)
        {
            if ((typeOverrides[i].position - pos).sqrMagnitude <= tol2)
            {
                var o = typeOverrides[i];
                o.position = pos; // recale la position au cas où
                o.type = t;
                typeOverrides[i] = o;
                return;
            }
        }

        typeOverrides.Add(new PointTypeOverride { position = pos, type = t });
    }

    public void RemoveOverrideNear(Vector3 pos)
    {
        float tol2 = overrideMatchTolerance * overrideMatchTolerance;

        for (int i = typeOverrides.Count - 1; i >= 0; i--)
        {
            if ((typeOverrides[i].position - pos).sqrMagnitude <= tol2)
                typeOverrides.RemoveAt(i);
        }
    }
}
