#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(Path))]
public class PathEditor : Editor
{
    private Path _path;

    private const string AnchorsRootName = "Anchors";
    private const string GeneratedRootName = "GeneratedPoints";

    private static bool _editMode = true;

    // ----------------------------
    // Pending click vs drag
    // ----------------------------
    private static bool _pendingDown;
    private static Transform _pendingAnchor;
    private static bool _pendingHasPoint;
    private static Vector3 _pendingPointPos;
    private static Vector2 _mouseDownGuiPos;

    private const float DragThresholdPixels = 6f;

    private static bool _dragging;
    private static Transform _dragAnchor;
    private static Vector3 _dragOffset;

    // ----------------------------
    // Tool selections (NOT Unity selection)
    // ----------------------------
    private static bool _hasSelectedPoint;
    private static Vector3 _selectedPointPos;

    private static Transform _selectedAnchor; // tool-selected anchor (for inspector delete button)

    // ----------------------------
    // Visual scales
    // ----------------------------
    private const float AnchorDrawScale = 0.14f;
    private const float AnchorPickScale = 0.22f;

    private const float PointDrawScale = 0.10f;
    private const float PointPickScale = 0.16f;

    private const float AddButtonScale = 0.22f;
    private const float AddButtonExtraOffset = 1.0f;
    private const float ExtendDistance = 2.0f;

    // ======================================================
    // Lifecycle
    // ======================================================

    private void OnEnable()
    {
        _path = (Path)target;
        EnsureRoots();
        Selection.activeObject = _path.gameObject;
    }

    private void OnDisable()
    {
        Tools.hidden = false;
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("Path Tool", EditorStyles.boldLabel);

        bool newEdit = GUILayout.Toggle(
            _editMode,
            _editMode ? "Edit Mode: ON" : "Edit Mode: OFF",
            "Button",
            GUILayout.Height(28)
        );

        if (newEdit != _editMode)
        {
            _editMode = newEdit;
            Tools.hidden = _editMode;
            if (_editMode) Tools.current = Tool.None;
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Rebuild Now", GUILayout.Height(24)))
        {
            _path.RebuildNow();
            EditorUtility.SetDirty(_path);
        }

        EditorGUILayout.Space(8);

        DrawSelectedAnchorInspector();  // ✅ delete button for anchor
        DrawSelectedPointInspector();   // ✅ edit point type override

        EditorGUILayout.Space(8);
        DrawDefaultInspector();

        EditorGUILayout.HelpBox(
            "Scene controls (Edit Mode ON):\n" +
            "- Path always remains selected\n" +
            "- Drag anchors by clicking their circles\n" +
            "- Click a generated point circle to select it (even if it overlaps an anchor)\n" +
            "- Ctrl + Left Click on an anchor: delete it\n" +
            "- Use +Start / +End buttons to add anchors\n",
            MessageType.Info
        );
    }

    private void OnSceneGUI()
    {
        if (_path == null) return;

        // Keep Path selected always
        Selection.activeObject = _path.gameObject;

        Tools.hidden = _editMode;
        if (_editMode) Tools.current = Tool.None;

        Transform anchorsRoot = EnsureAnchorsRoot();
        Transform genRoot = EnsureGeneratedRoot();

        List<PathAnchor> anchors = GetAnchorsOrdered(anchorsRoot);

        DrawConnections(anchors);
        DrawAddButtons(anchorsRoot, anchors);
        DrawAnchorCircles(anchors);
        DrawGeneratedPointCircles(genRoot);

        if (!_editMode) return;

        // Prevent Unity from selecting anchors/points
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        HandleAnchorAndPointInteraction(anchors, genRoot);
    }

    // ======================================================
    // Inspector: Selected anchor (tool-selected)
    // ======================================================

    private void DrawSelectedAnchorInspector()
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("Selected Anchor", EditorStyles.boldLabel);

            if (_selectedAnchor == null || _selectedAnchor.Equals(null))
            {
                EditorGUILayout.LabelField("None (click/drag an anchor circle)");
                return;
            }

            // Ensure the anchor still belongs to this path
            if (!_selectedAnchor.IsChildOf(_path.transform))
            {
                _selectedAnchor = null;
                EditorGUILayout.LabelField("None");
                return;
            }

            EditorGUILayout.LabelField($"Name: {_selectedAnchor.name}");
            EditorGUILayout.LabelField($"Pos: {_selectedAnchor.position.x:F2}, {_selectedAnchor.position.y:F2}");

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("Delete Anchor"))
                {
                    DeleteAnchor(_selectedAnchor);
                    _selectedAnchor = null;
                    SceneView.RepaintAll();
                    Repaint();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Deselect"))
                {
                    _selectedAnchor = null;
                    SceneView.RepaintAll();
                    Repaint();
                }
            }
        }
    }

    // ======================================================
    // Inspector: Selected generated point
    // ======================================================

    private void DrawSelectedPointInspector()
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("Selected Generated Point", EditorStyles.boldLabel);

            if (!_hasSelectedPoint)
            {
                EditorGUILayout.LabelField("None (click a generated point circle)");
                return;
            }

            EditorGUILayout.LabelField($"Pos: {_selectedPointPos.x:F3}, {_selectedPointPos.y:F3}");

            bool isOverride;
            PointType curType = GetCurrentTypeAtSelectedPos(out isOverride);
            EditorGUILayout.LabelField(isOverride ? "Source: Override" : "Source: Generated");

            EditorGUI.BeginChangeCheck();
            PointType newType = (PointType)EditorGUILayout.EnumPopup("PointType", curType);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_path, "Set PointType Override");
                _path.SetOverride(_selectedPointPos, newType);
                EditorUtility.SetDirty(_path);

                _path.RebuildNow();
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Override"))
                {
                    Undo.RecordObject(_path, "Clear PointType Override");
                    _path.RemoveOverrideNear(_selectedPointPos);
                    _hasSelectedPoint = false;
                    EditorUtility.SetDirty(_path);

                    _path.RebuildNow();
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Deselect"))
                {
                    _hasSelectedPoint = false;
                    SceneView.RepaintAll();
                }
            }
        }
    }

    // ======================================================
    // Interaction
    // ======================================================

    private void HandleAnchorAndPointInteraction(List<PathAnchor> anchors, Transform genRoot)
    {
        Event e = Event.current;
        if (e == null || e.alt) return;

        Vector3 mouseWorld = GetMouseWorldOnZPlane(0f);

        // MouseDown (left) => collect candidates
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            _pendingDown = true;
            _mouseDownGuiPos = e.mousePosition;

            _pendingAnchor = PickAnchorUnderMouse(anchors, mouseWorld);
            _pendingHasPoint = TryPickGeneratedPoint(genRoot, mouseWorld, PointPickScale, out _pendingPointPos);

            // If user clicked on an anchor, mark it as tool-selected immediately (even if later becomes a drag)
            if (_pendingAnchor != null)
                _selectedAnchor = _pendingAnchor;

            e.Use();
            return;
        }

        // MouseDrag => if moved enough and anchor exists, drag anchor
        if (_pendingDown && e.type == EventType.MouseDrag && e.button == 0)
        {
            float pixelMove = (e.mousePosition - _mouseDownGuiPos).magnitude;

            if (!_dragging && pixelMove >= DragThresholdPixels && _pendingAnchor != null)
            {
                _dragging = true;
                _dragAnchor = _pendingAnchor;
                _dragOffset = _dragAnchor.position - mouseWorld;
            }

            if (_dragging && _dragAnchor != null)
            {
                Undo.RecordObject(_dragAnchor, "Move Anchor");
                _dragAnchor.position = mouseWorld + _dragOffset;

                _path.RebuildNow();
                EditorUtility.SetDirty(_path);
            }

            e.Use();
            return;
        }

        // MouseUp => if not dragging: select anchor OR point (deterministic)
        if (_pendingDown && e.type == EventType.MouseUp && e.button == 0)
        {
            if (_dragging)
            {
                _dragging = false;
                _dragAnchor = null;
            }
            else
            {
                // ✅ Si on a cliqué sur un anchor => on sélectionne anchor + point associé
                if (_pendingAnchor != null)
                {
                    _selectedAnchor = _pendingAnchor;

                    // On sélectionne aussi le point généré "sur" l'anchor
                    Transform gen = _path.transform.Find(GeneratedRootName);
                    float tol = Mathf.Max(0.001f, _path.overrideMatchTolerance);

                    if (TryPickGeneratedPointNearPosition(gen, _pendingAnchor.position, tol, out Vector3 ppos))
                    {
                        _selectedPointPos = ppos;
                        _hasSelectedPoint = true;
                    }
                    else
                    {
                        // s'il n'y a aucun point à cette position (rare), on garde l'ancien état point
                    }

                    Repaint();
                    SceneView.RepaintAll();
                }
                // Sinon, clic point classique (point seul)
                else if (_pendingHasPoint)
                {
                    _selectedPointPos = _pendingPointPos;
                    _hasSelectedPoint = true;

                    // (optionnel) tu peux garder l'anchor sélectionné ou le clear.
                    // Je le clear pour éviter confusion si tu cliques un point ailleurs.
                    _selectedAnchor = null;

                    Repaint();
                    SceneView.RepaintAll();
                }
            }

            _pendingDown = false;
            _pendingAnchor = null;
            _pendingHasPoint = false;

            Selection.activeObject = _path.gameObject;
            e.Use();
            return;
        }

    }

    // ======================================================
    // Delete
    // ======================================================

    private void DeleteAnchor(Transform anchor)
    {
        if (anchor == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        Undo.DestroyObjectImmediate(anchor.gameObject);

        _path.RebuildNow();
        EditorUtility.SetDirty(_path);

        Selection.activeObject = _path.gameObject;

        Undo.CollapseUndoOperations(group);
    }

    // ======================================================
    // Drawing
    // ======================================================

    private void DrawConnections(List<PathAnchor> anchors)
    {
        if (anchors == null || anchors.Count < 2) return;

        for (int i = 0; i < anchors.Count - 1; i++)
        {
            var a = anchors[i];
            var b = anchors[i + 1];
            if (!a || !b) continue;

            Handles.color = ColorForType(a.segmentType);
            Handles.DrawAAPolyLine(3f, a.transform.position, b.transform.position);
        }
    }

    private void DrawAnchorCircles(List<PathAnchor> anchors)
    {
        if (anchors == null) return;

        for (int i = 0; i < anchors.Count; i++)
        {
            var a = anchors[i];
            if (!a) continue;

            Vector3 pos = a.transform.position;
            float size = HandleUtility.GetHandleSize(pos) * AnchorDrawScale;

            Handles.color = ColorForType(a.segmentType);
            Handles.DrawSolidDisc(pos, Vector3.forward, size);

            // outline if tool-selected anchor
            if (_selectedAnchor == a.transform)
            {
                Handles.color = Color.white;
                Handles.DrawWireDisc(pos, Vector3.forward, size * 1.35f);
            }
        }
    }

    private void DrawGeneratedPointCircles(Transform genRoot)
    {
        if (genRoot == null) return;

        float tol = Mathf.Max(0.001f, _path.overrideMatchTolerance);
        float tol2 = tol * tol;

        for (int i = 0; i < genRoot.childCount; i++)
        {
            Point pt = genRoot.GetChild(i).GetComponent<Point>();
            if (pt == null) continue;

            Vector3 pos = pt.transform.position;
            float size = HandleUtility.GetHandleSize(pos) * PointDrawScale;

            Handles.color = ColorForType(pt.pointType);
            Handles.DrawSolidDisc(pos, Vector3.forward, size);

            if (_hasSelectedPoint && (pos - _selectedPointPos).sqrMagnitude <= tol2)
            {
                Handles.color = Color.white;
                Handles.DrawWireDisc(pos, Vector3.forward, size * 1.8f);
            }
        }
    }

    // ======================================================
    // Add buttons
    // ======================================================

    private void DrawAddButtons(Transform anchorsRoot, List<PathAnchor> anchors)
    {
        Vector3 startPos, endPos;
        Vector3 startDir = Vector3.left;
        Vector3 endDir = Vector3.right;

        if (anchors == null) anchors = new List<PathAnchor>();

        if (anchors.Count >= 2)
        {
            startDir = (anchors[0].transform.position - anchors[1].transform.position).normalized;
            endDir = (anchors[^1].transform.position - anchors[^2].transform.position).normalized;

            startPos = anchors[0].transform.position + startDir * (0.45f + AddButtonExtraOffset);
            endPos = anchors[^1].transform.position + endDir * (0.45f + AddButtonExtraOffset);
        }
        else
        {
            Vector3 basePos = _path.transform.position;
            startPos = basePos + Vector3.left * (0.5f + AddButtonExtraOffset);
            endPos = basePos + Vector3.right * (0.5f + AddButtonExtraOffset);
        }

        float s1 = HandleUtility.GetHandleSize(startPos) * AddButtonScale;
        float s2 = HandleUtility.GetHandleSize(endPos) * AddButtonScale;

        Handles.color = new Color(0.75f, 0.4f, 1f);
        if (Handles.Button(startPos, Quaternion.identity, s1, s1, Handles.SphereHandleCap))
        {
            if (_editMode)
            {
                Vector3 newPos;
                if (anchors.Count == 0) newPos = _path.transform.position + Vector3.left * ExtendDistance;
                else if (anchors.Count == 1) newPos = anchors[0].transform.position + Vector3.left * ExtendDistance;
                else newPos = anchors[0].transform.position + startDir * ExtendDistance;

                CreateAnchorAt(anchorsRoot, newPos, 0);
            }
        }

        Handles.color = new Color(0.2f, 0.6f, 1f);
        if (Handles.Button(endPos, Quaternion.identity, s2, s2, Handles.SphereHandleCap))
        {
            if (_editMode)
            {
                Vector3 newPos;
                if (anchors.Count == 0) newPos = _path.transform.position + Vector3.right * ExtendDistance;
                else if (anchors.Count == 1) newPos = anchors[0].transform.position + Vector3.right * ExtendDistance;
                else newPos = anchors[^1].transform.position + endDir * ExtendDistance;

                CreateAnchorAt(anchorsRoot, newPos, -1);
            }
        }
    }

    private void CreateAnchorAt(Transform anchorsRoot, Vector3 worldPos, int siblingIndex)
    {
        if (anchorsRoot == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        GameObject go = new GameObject("Anchor");
        Undo.RegisterCreatedObjectUndo(go, "Create Anchor");
        Undo.SetTransformParent(go.transform, anchorsRoot, "Parent Anchor");
        go.transform.position = worldPos;

        var a = Undo.AddComponent<PathAnchor>(go);

        if (siblingIndex < 0)
            go.transform.SetSiblingIndex(anchorsRoot.childCount - 1);
        else
            go.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, anchorsRoot.childCount - 1));

        _path.RebuildNow();
        EditorUtility.SetDirty(_path);

        // Keep Path selected
        Selection.activeObject = _path.gameObject;

        Undo.CollapseUndoOperations(group);
    }

    // ======================================================
    // Roots / Picking / Utils
    // ======================================================

    private void EnsureRoots()
    {
        EnsureAnchorsRoot();
        EnsureGeneratedRoot();
    }

    private Transform EnsureAnchorsRoot()
    {
        Transform t = _path.transform.Find(AnchorsRootName);
        if (t == null)
        {
            GameObject go = new GameObject(AnchorsRootName);
            Undo.RegisterCreatedObjectUndo(go, "Create Anchors Root");
            go.transform.SetParent(_path.transform, false);
            t = go.transform;
        }
        return t;
    }

    private Transform EnsureGeneratedRoot()
    {
        Transform t = _path.transform.Find(GeneratedRootName);
        if (t == null)
        {
            GameObject go = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(go, "Create Generated Root");
            go.transform.SetParent(_path.transform, false);
            t = go.transform;
        }
        return t;
    }

    private List<PathAnchor> GetAnchorsOrdered(Transform root)
    {
        var res = new List<PathAnchor>();
        if (root == null) return res;

        for (int i = 0; i < root.childCount; i++)
        {
            var a = root.GetChild(i).GetComponent<PathAnchor>();
            if (a != null) res.Add(a);
        }
        return res;
    }

    private Transform PickAnchorUnderMouse(List<PathAnchor> anchors, Vector3 mouseWorld)
    {
        Transform best = null;
        float bestD2 = float.PositiveInfinity;

        if (anchors == null) return null;

        foreach (var a in anchors)
        {
            if (!a) continue;

            Vector3 pos = a.transform.position;
            float r = HandleUtility.GetHandleSize(pos) * AnchorPickScale;
            float d2 = (mouseWorld - pos).sqrMagnitude;

            if (d2 <= r * r && d2 < bestD2)
            {
                best = a.transform;
                bestD2 = d2;
            }
        }
        return best;
    }

    private bool TryPickGeneratedPoint(Transform genRoot, Vector3 mouseWorld, float pickScale, out Vector3 picked)
    {
        picked = Vector3.zero;
        if (genRoot == null) return false;

        bool found = false;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < genRoot.childCount; i++)
        {
            Point pt = genRoot.GetChild(i).GetComponent<Point>();
            if (pt == null) continue;

            Vector3 pos = pt.transform.position;
            float r = HandleUtility.GetHandleSize(pos) * pickScale;
            float d2 = (mouseWorld - pos).sqrMagnitude;

            if (d2 <= r * r && d2 < bestD2)
            {
                picked = pos;
                bestD2 = d2;
                found = true;
            }
        }
        return found;
    }

    private PointType GetCurrentTypeAtSelectedPos(out bool isOverride)
    {
        if (_path.TryGetOverride(_selectedPointPos, out PointType forced))
        {
            isOverride = true;
            return forced;
        }

        Transform gen = _path.transform.Find(GeneratedRootName);
        if (gen != null)
        {
            float tol = Mathf.Max(0.001f, _path.overrideMatchTolerance);
            float tol2 = tol * tol;

            for (int i = 0; i < gen.childCount; i++)
            {
                Point pt = gen.GetChild(i).GetComponent<Point>();
                if (pt == null) continue;

                if ((pt.transform.position - _selectedPointPos).sqrMagnitude <= tol2)
                {
                    isOverride = false;
                    return pt.pointType;
                }
            }
        }

        isOverride = false;
        return PointType.GROUNDED;
    }

    private Vector3 GetMouseWorldOnZPlane(float z)
    {
        Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (Mathf.Abs(r.direction.z) < 0.0001f) return r.origin;

        float t = (z - r.origin.z) / r.direction.z;
        return r.origin + r.direction * t;
    }

    private Color ColorForType(PointType t)
    {
        return t switch
        {
            PointType.GROUNDED => new Color(0.1f, 1f, 0.1f),
            PointType.JUMP_VERTICAL => new Color(1f, 1f, 0.1f),
            PointType.JUMP_HORIZONTAL => new Color(1f, 0.55f, 0f),
            PointType.FLY => new Color(0.1f, 1f, 1f),
            _ => Color.white
        };
    }

    private bool TryPickGeneratedPointNearPosition(Transform genRoot, Vector3 worldPos, float maxWorldDist, out Vector3 pickedPos)
    {
        pickedPos = Vector3.zero;
        if (genRoot == null) return false;

        float bestD2 = float.PositiveInfinity;
        bool found = false;

        float maxD2 = maxWorldDist * maxWorldDist;

        for (int i = 0; i < genRoot.childCount; i++)
        {
            Point pt = genRoot.GetChild(i).GetComponent<Point>();
            if (pt == null) continue;

            Vector3 pos = pt.transform.position;
            float d2 = (pos - worldPos).sqrMagnitude;

            if (d2 <= maxD2 && d2 < bestD2)
            {
                bestD2 = d2;
                pickedPos = pos;
                found = true;
            }
        }

        return found;
    }
}
#endif
