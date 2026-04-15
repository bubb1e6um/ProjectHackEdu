using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// HackEdu → Wall Painter
/// LMB drag — paint walls.  RMB drag — erase walls.
/// Green preview = will place.  Yellow = already filled.  Red = will erase.
/// </summary>
public class WallPainterWindow : EditorWindow
{
    // ── State ─────────────────────────────────────────────────────────────────
    WallGrid _grid;
    bool     _paintMode;
    bool     _eraseMode;   // false = paint, true = erase

    // Track last cell acted on during a drag to avoid duplicate ops
    Vector2Int _lastActed;
    bool       _dragging;

    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("HackEdu/Wall Painter")]
    static void Open() => GetWindow<WallPainterWindow>("Wall Painter");

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        TryFindGrid();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        _paintMode = false;
    }

    void OnFocus() => TryFindGrid();

    void TryFindGrid()
    {
        if (_grid == null)
            _grid = FindFirstObjectByType<WallGrid>();
    }

    // =========================================================================
    //  Inspector window
    // =========================================================================
    void OnGUI()
    {
        GUILayout.Label("Wall Painter", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _grid = (WallGrid)EditorGUILayout.ObjectField("Wall Grid", _grid, typeof(WallGrid), true);

        if (_grid == null)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("No WallGrid found in scene.", MessageType.Warning);
            if (GUILayout.Button("▶  Create WallGrid", GUILayout.Height(30)))
                CreateGrid();
            return;
        }

        EditorGUILayout.Space(8);

        // ── Mode buttons ──────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = (_paintMode && !_eraseMode) ? new Color(0.4f, 1f, 0.4f) : Color.white;
        if (GUILayout.Button("✏  Paint", GUILayout.Height(34)))
        { _paintMode = true; _eraseMode = false; }

        GUI.backgroundColor = (_paintMode && _eraseMode) ? new Color(1f, 0.4f, 0.4f) : Color.white;
        if (GUILayout.Button("✕  Erase", GUILayout.Height(34)))
        { _paintMode = true; _eraseMode = true; }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (_paintMode)
        {
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.9f, 0.5f);
            if (GUILayout.Button("■  Stop", GUILayout.Height(28)))
            { _paintMode = false; _dragging = false; }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "LMB drag — paint\nRMB drag — erase\n\nPreview colours:\n" +
            "  Green  = empty, will place\n" +
            "  Yellow = already filled\n" +
            "  Red    = will erase",
            MessageType.Info);

        EditorGUILayout.Space(6);

        if (GUILayout.Button("Rebuild Mesh"))
            _grid.RebuildMesh();
    }

    // =========================================================================
    //  Scene-view painting
    // =========================================================================
    void OnSceneGUI(SceneView sv)
    {
        if (!_paintMode || _grid == null) return;

        Event e = Event.current;

        // Consume all default controls so Unity doesn't deselect objects
        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // Determine erase intent: explicit erase mode OR right-button drag
        bool erase = _eraseMode || (e.button == 1 && _dragging);

        Ray   ray   = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float dist)) return;

        Vector3    hit  = ray.GetPoint(dist);
        Vector2Int cell = WorldToCell(hit);

        // ── Draw preview ──────────────────────────────────────────────────────
        bool occupied = _grid.HasCell(cell);
        if (erase)
            Handles.color = occupied
                ? new Color(1f, 0.25f, 0.25f, 0.70f)
                : new Color(1f, 0.25f, 0.25f, 0.25f);
        else
            Handles.color = occupied
                ? new Color(1f, 1f,    0f,    0.55f)
                : new Color(0.2f, 1f,  0.2f,  0.70f);

        float cx = cell.x * WallGrid.GridSize;
        float cz = cell.y * WallGrid.GridSize;
        Handles.DrawWireCube(
            new Vector3(cx, WallGrid.WallHeight * 0.5f, cz),
            new Vector3(WallGrid.GridSize, WallGrid.WallHeight, WallGrid.GridSize));

        // ── Paint / erase logic ───────────────────────────────────────────────
        bool isDown = e.type == EventType.MouseDown;
        bool isDrag = e.type == EventType.MouseDrag;

        if ((isDown || isDrag) && (e.button == 0 || e.button == 1) && !e.alt)
        {
            if (isDown) { _dragging = true; _lastActed = cell - Vector2Int.one; }

            bool actualErase = _eraseMode || e.button == 1;

            if (cell != _lastActed)   // skip repeated op on same cell during drag
            {
                _lastActed = cell;

                if (actualErase && _grid.HasCell(cell))
                {
                    Undo.RecordObject(_grid, "Erase Wall");
                    _grid.RemoveCell(cell);
                    EditorUtility.SetDirty(_grid);
                    MarkDirty();
                }
                else if (!actualErase && !_grid.HasCell(cell))
                {
                    Undo.RecordObject(_grid, "Paint Wall");
                    _grid.AddCell(cell);
                    EditorUtility.SetDirty(_grid);
                    MarkDirty();
                }
            }
            e.Use();
        }

        if (e.type == EventType.MouseUp) _dragging = false;

        sv.Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    static Vector2Int WorldToCell(Vector3 world)
    {
        return new Vector2Int(
            Mathf.RoundToInt(world.x / WallGrid.GridSize),
            Mathf.RoundToInt(world.z / WallGrid.GridSize));
    }

    static void MarkDirty()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    void CreateGrid()
    {
        var go  = new GameObject("[WallGrid]");
        go.layer = 7;   // Obstacle
        _grid = go.AddComponent<WallGrid>();

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");

        Undo.RegisterCreatedObjectUndo(go, "Create WallGrid");
        MarkDirty();
        Selection.activeGameObject = go;
        Repaint();
    }
}
