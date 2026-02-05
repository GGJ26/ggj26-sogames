#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Path))]
public class PathEditor : Editor
{
    private Path _path;

    private const float ExtendDistance = 1.5f;
    private const float AddButtonScale = 0.12f;

    private bool _editMode;

    private string PrefKeyActiveChain => $"PathEditor.ActiveChain.{_path.GetInstanceID()}";

    private void OnEnable()
    {
        _path = (Path)target;
    }

    public override void OnInspectorGUI()
    {
        if (_path == null) return;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Multi-Chains", EditorStyles.boldLabel);

        var anchorsRoot = _path.transform.Find(_path.anchorsRootName);
        if (anchorsRoot == null)
        {
            if (GUILayout.Button("Create Anchors Root"))
            {
                Undo.RegisterCompleteObjectUndo(_path.gameObject, "Create Anchors Root");
                _path.EnsureRoots();
                EditorUtility.SetDirty(_path);
            }
        }
        else
        {
            string active = GetActiveChainName();
            var chainNames = GetChainNames(anchorsRoot);

            int idx = Mathf.Max(0, chainNames.IndexOf(active));
            int newIdx = EditorGUILayout.Popup("Active Chain", idx, chainNames.ToArray());
            if (newIdx != idx && newIdx >= 0 && newIdx < chainNames.Count)
            {
                SetActiveChainName(chainNames[newIdx]);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New Chain"))
            {
                CreateNewChain(anchorsRoot);
            }
            if (GUILayout.Button(_editMode ? "Edit: ON" : "Edit: OFF"))
            {
                _editMode = !_editMode;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Rebuild Now"))
        {
            Undo.RegisterCompleteObjectUndo(_path.gameObject, "Rebuild Path");
            _path.RebuildNow();
            EditorUtility.SetDirty(_path);
        }
        if (GUILayout.Button("Build Graph"))
        {
            _path.BuildGraph();
            EditorUtility.SetDirty(_path);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void OnSceneGUI()
    {
        if (_path == null) return;
        _path.EnsureRoots();

        var anchorsRoot = _path.transform.Find(_path.anchorsRootName);
        if (anchorsRoot == null) return;

        // Draw all chains
        var chains = _path.GetAnchorChainsOrdered();
        foreach (var ch in chains)
        {
            DrawChain(ch.chainName, ch.anchors, isActive: ch.chainName == GetActiveChainName());
        }

        // Add buttons only on active chain
        if (_editMode)
        {
            string activeName = GetActiveChainName();
            var activeRoot = _path.EnsureChainRoot(activeName);
            var activeAnchors = GetAnchorsOrdered(activeRoot);

            DrawExtendButtons(activeRoot, activeAnchors);
        }
    }

    private void DrawChain(string chainName, List<PathAnchor> anchors, bool isActive)
    {
        if (anchors == null || anchors.Count == 0) return;

        Handles.color = isActive ? new Color(0.2f, 0.9f, 1f) : new Color(0.6f, 0.6f, 0.6f);
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            if (anchors[i] == null || anchors[i + 1] == null) continue;
            Handles.DrawAAPolyLine(4f, anchors[i].transform.position, anchors[i + 1].transform.position);
        }

        // label chain
        var first = anchors[0];
        if (first != null)
        {
            var pos = first.transform.position + Vector3.up * 0.4f;
            Handles.Label(pos, chainName, EditorStyles.whiteLabel);
        }
    }

    private void DrawExtendButtons(Transform chainRoot, List<PathAnchor> anchors)
    {
        // compute start/end pos and dir
        Vector3 startPos, endPos;
        Vector3 startDir = Vector3.left, endDir = Vector3.right;

        if (anchors.Count == 0)
        {
            startPos = _path.transform.position + Vector3.left * ExtendDistance;
            endPos = _path.transform.position + Vector3.right * ExtendDistance;
        }
        else if (anchors.Count == 1)
        {
            startPos = anchors[0].transform.position + Vector3.left * ExtendDistance;
            endPos = anchors[0].transform.position + Vector3.right * ExtendDistance;
        }
        else
        {
            startDir = (anchors[0].transform.position - anchors[1].transform.position).normalized;
            endDir = (anchors[^1].transform.position - anchors[^2].transform.position).normalized;
            startPos = anchors[0].transform.position + startDir * ExtendDistance;
            endPos = anchors[^1].transform.position + endDir * ExtendDistance;
        }

        float s1 = HandleUtility.GetHandleSize(startPos) * AddButtonScale;
        float s2 = HandleUtility.GetHandleSize(endPos) * AddButtonScale;

        Handles.color = new Color(0.75f, 0.4f, 1f);
        if (Handles.Button(startPos, Quaternion.identity, s1, s1, Handles.SphereHandleCap))
        {
            Vector3 newPos;
            if (anchors.Count == 0) newPos = _path.transform.position + Vector3.left * ExtendDistance;
            else if (anchors.Count == 1) newPos = anchors[0].transform.position + Vector3.left * ExtendDistance;
            else newPos = anchors[0].transform.position + startDir * ExtendDistance;

            CreateAnchorAt(chainRoot, newPos, 0);
        }

        Handles.color = new Color(0.2f, 0.6f, 1f);
        if (Handles.Button(endPos, Quaternion.identity, s2, s2, Handles.SphereHandleCap))
        {
            Vector3 newPos;
            if (anchors.Count == 0) newPos = _path.transform.position + Vector3.right * ExtendDistance;
            else if (anchors.Count == 1) newPos = anchors[0].transform.position + Vector3.right * ExtendDistance;
            else newPos = anchors[^1].transform.position + endDir * ExtendDistance;

            CreateAnchorAt(chainRoot, newPos, -1);
        }
    }

    private void CreateAnchorAt(Transform chainRoot, Vector3 worldPos, int siblingIndex)
    {
        if (chainRoot == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var go = new GameObject("Anchor");
        Undo.RegisterCreatedObjectUndo(go, "Create Anchor");

        go.transform.SetParent(chainRoot, true);
        go.transform.position = worldPos;

        var anchor = go.AddComponent<PathAnchor>();

        if (siblingIndex >= 0)
            go.transform.SetSiblingIndex(siblingIndex);

        Undo.CollapseUndoOperations(group);

        _path.RebuildNow();
        EditorUtility.SetDirty(_path);
        Selection.activeObject = go;
    }

    private List<PathAnchor> GetAnchorsOrdered(Transform chainRoot)
    {
        if (chainRoot == null) return new List<PathAnchor>();
        return chainRoot.GetComponentsInChildren<PathAnchor>(true)
            .Where(a => a != null && a.transform.parent == chainRoot)
            .OrderBy(a => a.transform.GetSiblingIndex())
            .ToList();
    }

    private List<string> GetChainNames(Transform anchorsRoot)
    {
        var names = new List<string>();

        // legacy anchors directly under Anchors => expose Chain_00
        bool hasLegacy = anchorsRoot.GetComponentsInChildren<PathAnchor>(true).Any(a => a != null && a.transform.parent == anchorsRoot);
        if (hasLegacy) names.Add(_path.legacyChainName);

        for (int i = 0; i < anchorsRoot.childCount; i++)
        {
            var child = anchorsRoot.GetChild(i);
            if (child == null) continue;

            // ignore PathAnchor root objects (legacy)
            if (child.GetComponent<PathAnchor>() != null) continue;

            // only include folders that contain direct PathAnchor children (or empty folders too)
            names.Add(child.name);
        }

        // if nothing, create default chain folder
        if (names.Count == 0)
        {
            var t = _path.EnsureChainRoot(_path.legacyChainName);
            names.Add(t.name);
        }

        // keep stable order
        names = names.Distinct().ToList();
        return names;
    }

    private string GetActiveChainName()
    {
        string v = EditorPrefs.GetString(PrefKeyActiveChain, _path.legacyChainName);
        if (string.IsNullOrEmpty(v)) v = _path.legacyChainName;
        return v;
    }

    private void SetActiveChainName(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        EditorPrefs.SetString(PrefKeyActiveChain, name);
        SceneView.RepaintAll();
    }

    private void CreateNewChain(Transform anchorsRoot)
    {
        // trouve un index libre
        int i = 0;
        while (anchorsRoot.Find($"{_path.chainPrefix}{i:00}") != null) i++;

        string name = $"{_path.chainPrefix}{i:00}";
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Chain");
        go.transform.SetParent(anchorsRoot, false);

        Undo.CollapseUndoOperations(group);

        SetActiveChainName(name);
        EditorUtility.SetDirty(_path);
    }
}
#endif
