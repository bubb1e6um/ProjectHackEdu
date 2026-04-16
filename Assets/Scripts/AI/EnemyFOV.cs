using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws a transparent FOV cone in front of the drone and maintains
/// a coloured Point Light that reacts to patrol / alert state.
///
/// Attach to the same GameObject that has EnemyController.
/// The cone stays flat on the ground regardless of the enemy's Y position.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class EnemyFOV : MonoBehaviour
{
    [Header("Vision geometry")]
    public float ViewDistance = 12f;
    [Range(30f, 120f)] public float ViewAngle = 70f;
    [Range(8, 48)]     public int   Segments  = 32;

    [Header("Patrol colours")]
    public Color PatrolFill    = new Color(0.00f, 1.00f, 0.30f, 0.18f);
    public Color PatrolEdge    = new Color(0.00f, 1.00f, 0.30f, 0.55f);
    public Color PatrolLight   = new Color(0.00f, 0.85f, 0.25f, 1.00f);

    [Header("Alert colours")]
    public Color AlertFill     = new Color(1.00f, 0.08f, 0.00f, 0.38f);
    public Color AlertEdge     = new Color(1.00f, 0.15f, 0.00f, 0.80f);
    public Color AlertLight    = new Color(1.00f, 0.10f, 0.00f, 1.00f);

    [Header("Light settings")]
    public float LightRangePatrol = 4.5f;
    public float LightRangeAlert  = 8.0f;

    // ── Runtime refs ──────────────────────────────────────────────────────────
    EnemyController _enemy;

    // Fill cone
    GameObject   _fillGO;
    MeshFilter   _fillFilter;
    MeshRenderer _fillRenderer;
    Material     _fillMat;

    // Edge lines (two LineRenderers — left & right boundary + arc)
    GameObject     _edgeGO;
    LineRenderer   _edgeLeft;
    LineRenderer   _edgeRight;
    LineRenderer   _edgeArc;

    // Point light
    Light _light;

    float _blinkTimer;
    bool  _wasAlert;

    // =========================================================================
    void Awake()
    {
        _enemy = GetComponent<EnemyController>();

        BuildFillCone();
        BuildEdgeLines();
        BuildLight();
    }

    void Update()
    {
        bool alert = _enemy != null && _enemy.IsAlert;

        // ── Ground-snap ───────────────────────────────────────────────────────
        // Keep cone flat at y=0.06 world regardless of enemy elevation
        float groundOffsetY = -transform.position.y + 0.06f;
        _fillGO.transform.localPosition = new Vector3(0f, groundOffsetY, 0f);
        _edgeGO.transform.localPosition = new Vector3(0f, groundOffsetY, 0f);

        // ── Colour lerp ───────────────────────────────────────────────────────
        float t = Time.deltaTime * 7f;
        _fillMat.color = Color.Lerp(_fillMat.color, alert ? AlertFill : PatrolFill, t);

        Color targetEdge = alert ? AlertEdge : PatrolEdge;
        SetLineColor(_edgeLeft,  targetEdge);
        SetLineColor(_edgeRight, targetEdge);
        SetLineColor(_edgeArc,   targetEdge);

        // ── Light ─────────────────────────────────────────────────────────────
        if (alert)
        {
            _blinkTimer += Time.deltaTime * 9f;
            float blink     = (Mathf.Sin(_blinkTimer) * 0.5f + 0.5f);   // 0..1
            _light.color    = Color.Lerp(new Color(0.55f,0f,0f), AlertLight, blink);
            _light.range     = LightRangeAlert;
            _light.intensity = 2.5f + blink * 1.5f;
        }
        else
        {
            _blinkTimer    = 0f;
            _light.color   = Color.Lerp(_light.color, PatrolLight, t);
            _light.range   = Mathf.Lerp(_light.range, LightRangePatrol, t);
            _light.intensity = 1.8f;
        }

        _wasAlert = alert;
    }

    // =========================================================================
    //  Build fill cone
    // =========================================================================
    void BuildFillCone()
    {
        _fillGO = new GameObject("[FOV_Fill]");
        _fillGO.transform.SetParent(transform, false);

        _fillFilter   = _fillGO.AddComponent<MeshFilter>();
        _fillRenderer = _fillGO.AddComponent<MeshRenderer>();
        _fillRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _fillRenderer.receiveShadows    = false;

        _fillMat = MakeTransparentMat();
        _fillMat.color = PatrolFill;
        _fillRenderer.sharedMaterial = _fillMat;

        // Build fan mesh
        var mesh  = new Mesh { name = "FOV_Fill" };
        float half = ViewAngle * 0.5f;
        var verts = new Vector3[Segments + 2];
        var tris  = new int[Segments * 3];

        verts[0] = Vector3.zero;
        float step = ViewAngle / Segments;

        for (int i = 0; i <= Segments; i++)
        {
            float a = (-half + step * i) * Mathf.Deg2Rad;
            verts[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * ViewDistance;
        }
        for (int i = 0; i < Segments; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        _fillFilter.mesh = mesh;
    }

    // =========================================================================
    //  Build edge lines (two radials + arc)
    // =========================================================================
    void BuildEdgeLines()
    {
        _edgeGO = new GameObject("[FOV_Edges]");
        _edgeGO.transform.SetParent(transform, false);

        float half = ViewAngle * 0.5f;

        // Left boundary
        _edgeLeft  = MakeLine(_edgeGO, "EdgeLeft", 2);
        _edgeLeft.SetPosition(0, Vector3.zero);
        float aL   = -half * Mathf.Deg2Rad;
        _edgeLeft.SetPosition(1, new Vector3(Mathf.Sin(aL), 0f, Mathf.Cos(aL)) * ViewDistance);

        // Right boundary
        _edgeRight = MakeLine(_edgeGO, "EdgeRight", 2);
        _edgeRight.SetPosition(0, Vector3.zero);
        float aR   = half * Mathf.Deg2Rad;
        _edgeRight.SetPosition(1, new Vector3(Mathf.Sin(aR), 0f, Mathf.Cos(aR)) * ViewDistance);

        // Arc
        int arcPts = Segments + 1;
        _edgeArc   = MakeLine(_edgeGO, "EdgeArc", arcPts);
        float step = ViewAngle / Segments;
        for (int i = 0; i < arcPts; i++)
        {
            float a = (-half + step * i) * Mathf.Deg2Rad;
            _edgeArc.SetPosition(i, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * ViewDistance);
        }

        // Set initial colours
        SetLineColor(_edgeLeft,  PatrolEdge);
        SetLineColor(_edgeRight, PatrolEdge);
        SetLineColor(_edgeArc,   PatrolEdge);
    }

    LineRenderer MakeLine(GameObject parent, string name, int points)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount     = points;
        lr.startWidth        = 0.06f;
        lr.endWidth          = 0.06f;
        lr.useWorldSpace     = false;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.material          = MakeTransparentMat();
        lr.material.color    = PatrolEdge;
        lr.numCapVertices    = 4;
        return lr;
    }

    static void SetLineColor(LineRenderer lr, Color c)
    {
        if (lr == null) return;
        lr.startColor = c;
        lr.endColor   = c;
    }

    // =========================================================================
    //  Point Light
    // =========================================================================
    void BuildLight()
    {
        var lightGO = new GameObject("[DroneLight]");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        _light           = lightGO.AddComponent<Light>();
        _light.type      = LightType.Point;
        _light.color     = PatrolLight;
        _light.intensity = 1.8f;
        _light.range     = LightRangePatrol;
        _light.shadows   = LightShadows.None;
    }

    // =========================================================================
    //  Material helper — transparent unlit, works with URP
    // =========================================================================
    static Material MakeTransparentMat()
    {
        // Try URP Unlit, fall back to Sprites/Default which is always transparent
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Color");

        var mat = new Material(shader);

        // URP Unlit transparent surface
        mat.SetFloat("_Surface",   1f);   // Transparent
        mat.SetFloat("_Blend",     0f);   // Alpha
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_Cull",      0f);   // Off — visible from both sides
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",   0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        return mat;
    }
}
