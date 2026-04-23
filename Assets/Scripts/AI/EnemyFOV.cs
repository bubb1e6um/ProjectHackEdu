using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(EnemyController))]
public class EnemyFOV : MonoBehaviour
{
    [Header("Vision Geometry")]
    [Range(30f, 120f)] public float ViewAngle = 70f;
    [Range(8,  64)]    public int   Segments  = 40;

    [Header("Patrol Colours")]
    public Color PatrolFill  = new Color(1.00f, 0.08f, 0.04f, 0.28f);
    public Color PatrolEdge  = new Color(1.00f, 0.30f, 0.10f, 1.00f);
    public Color PatrolRing  = new Color(1.00f, 0.15f, 0.05f, 0.55f);
    public Color PatrolPulse = new Color(1.00f, 0.55f, 0.10f, 1.00f);
    public Color PatrolLight = new Color(1.00f, 0.10f, 0.05f, 1.00f);

    [Header("Alert Colours")]
    public Color AlertFill  = new Color(1.00f, 0.25f, 0.00f, 0.55f);
    public Color AlertEdge  = new Color(1.00f, 0.65f, 0.30f, 1.00f);
    public Color AlertRing  = new Color(1.00f, 0.30f, 0.10f, 0.80f);
    public Color AlertPulse = new Color(1.00f, 0.85f, 0.20f, 1.00f);
    public Color AlertLight = new Color(1.00f, 0.20f, 0.00f, 1.00f);

    [Header("Light")]
    public float LightRangePatrol = 4.5f;
    public float LightRangeAlert  = 8.0f;

    [Header("Scan Pulse")]
    public float PulseDurationPatrol = 2.0f;
    public float PulseDurationAlert  = 0.5f;

    [Header("Detail")]
    [Range(1, 6)]      public int   RingCount       = 3;
    [Range(0, 8)]      public int   RadialCount     = 4;
    [Range(0f, 0.15f)] public float BracketFraction = 0.07f;

    // ── Private ───────────────────────────────────────────────────────────────
    EnemyController _enemy;
    GameObject      _root;

    MeshRenderer _fillRenderer;
    Material     _fillMat;
    Mesh         _fillMesh;

    LineRenderer   _edgeLeft, _edgeRight, _edgeArc;
    LineRenderer[] _rings;
    LineRenderer[] _radials;
    LineRenderer   _pulse;
    LineRenderer[] _brackets;
    Light          _light;

    float _pulseT;
    float _flickerVal  = 1f;
    float _flickerNext = 0f;
    float _flickerTime = 0f;
    float _blinkT;

    float[] _hitDists;   // wall-clipped distance per ray

    // =========================================================================
    void Awake()
    {
        _enemy    = GetComponent<EnemyController>();
        _hitDists = new float[Segments + 1];
        for (int i = 0; i <= Segments; i++) _hitDists[i] = _enemy.visionDistance;

        _root = new GameObject("[FOV]");
        _root.transform.SetParent(transform, false);

        BuildFill();
        BuildBoundary();
        BuildRings();
        BuildRadials();
        BuildPulse();
        BuildBrackets();
        BuildLight();
    }

    // =========================================================================
    void Update()
    {
        bool  alert = _enemy != null && _enemy.IsAlert;
        float dt    = Time.deltaTime;
        float lerp  = 1f - Mathf.Exp(-8f * dt);

        _root.transform.localPosition = new Vector3(0f, -transform.position.y + 0.55f, 0f);

        // CRT flicker
        _flickerTime += dt;
        if (_flickerTime >= _flickerNext)
        {
            _flickerTime = 0f;
            _flickerNext = Random.Range(alert ? 0.03f : 0.06f, alert ? 0.12f : 0.25f);
            _flickerVal  = Random.Range(alert ? 0.75f : 0.88f, 1.00f);
        }
        float flicker = _flickerVal;

        // Raycast → rebuild geometry
        CastFOVRays();
        RebuildFillMesh();
        RebuildBoundary();
        RebuildRings();
        RebuildPulse(dt, alert);

        // Target colours
        Color tFill  = alert ? AlertFill  : PatrolFill;
        Color tEdge  = alert ? AlertEdge  : PatrolEdge;
        Color tRing  = alert ? AlertRing  : PatrolRing;

        _fillMat.color = Color.Lerp(_fillMat.color, tFill, lerp);

        float edgeW   = alert ? 0.10f : 0.07f;
        Color edgeCol = tEdge; edgeCol.a *= flicker;
        SetLine(_edgeLeft,  edgeCol, edgeW);
        SetLine(_edgeRight, edgeCol, edgeW);
        SetLine(_edgeArc,   edgeCol, edgeW);

        if (_rings != null)
        { Color rc = tRing; rc.a *= flicker; foreach (var r in _rings) SetLine(r, rc, 0.025f); }

        if (_radials != null)
        { Color rc = tRing; rc.a *= 0.55f * flicker; foreach (var r in _radials) SetLine(r, rc, 0.020f); }

        if (_brackets != null)
        { Color bc = tEdge; bc.a *= flicker; foreach (var b in _brackets) SetLine(b, bc, edgeW * 1.4f); }

        // Light
        if (alert)
        {
            _blinkT += dt * 10f;
            float blink      = Mathf.Sin(_blinkT) * 0.5f + 0.5f;
            _light.color     = Color.Lerp(new Color(0.5f, 0f, 0f), AlertLight, blink);
            _light.range     = LightRangeAlert;
            _light.intensity = 2.0f + blink * 2.0f;
        }
        else
        {
            _blinkT          = 0f;
            _light.color     = Color.Lerp(_light.color, PatrolLight, lerp);
            _light.range     = Mathf.Lerp(_light.range, LightRangePatrol, lerp);
            _light.intensity = 1.8f;
        }
    }

    // =========================================================================
    //  Raycasting
    // =========================================================================
    void CastFOVRays()
    {
        float   vd     = _enemy.visionDistance;
        Vector3 origin = _enemy.eyes != null ? _enemy.eyes.position : transform.position;
        float   h      = ViewAngle * 0.5f;
        float   step   = ViewAngle / Segments;

        for (int i = 0; i <= Segments; i++)
        {
            float   a        = (-h + step * i) * Mathf.Deg2Rad;
            Vector3 worldDir = transform.TransformDirection(Dir(a));
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-6f) { _hitDists[i] = vd; continue; }
            worldDir.Normalize();

            _hitDists[i] = Physics.Raycast(origin, worldDir, out RaycastHit hit, vd, _enemy.obstacleMask)
                ? hit.distance
                : vd;
        }
    }

    // =========================================================================
    //  Dynamic geometry rebuild (every frame)
    // =========================================================================
    void RebuildFillMesh()
    {
        float h    = ViewAngle * 0.5f;
        float step = ViewAngle / Segments;
        var verts  = new Vector3[Segments + 2];
        var tris   = new int[Segments * 3];

        verts[0] = Vector3.zero;
        for (int i = 0; i <= Segments; i++)
            verts[i + 1] = Dir((-h + step * i) * Mathf.Deg2Rad) * _hitDists[i];
        for (int i = 0; i < Segments; i++)
        { tris[i*3] = 0; tris[i*3+1] = i+1; tris[i*3+2] = i+2; }

        _fillMesh.Clear();
        _fillMesh.vertices  = verts;
        _fillMesh.triangles = tris;
        _fillMesh.RecalculateBounds();
    }

    void RebuildBoundary()
    {
        float h    = ViewAngle * 0.5f * Mathf.Deg2Rad;
        float step = ViewAngle / Segments;

        _edgeLeft.SetPosition(1,  Dir(-h) * _hitDists[0]);
        _edgeRight.SetPosition(1, Dir( h) * _hitDists[Segments]);

        for (int i = 0; i <= Segments; i++)
        {
            float a = (-ViewAngle * 0.5f + ViewAngle / Segments * i) * Mathf.Deg2Rad;
            _edgeArc.SetPosition(i, Dir(a) * _hitDists[i]);
        }
    }

    void RebuildRings()
    {
        if (_rings == null) return;
        float vd   = _enemy.visionDistance;
        float h    = ViewAngle * 0.5f;
        float step = ViewAngle / Segments;

        for (int r = 0; r < _rings.Length; r++)
        {
            float ringDist = vd * (r + 1f) / (RingCount + 1f);
            for (int i = 0; i <= Segments; i++)
            {
                float a = (-h + step * i) * Mathf.Deg2Rad;
                float d = Mathf.Min(ringDist, _hitDists[i]);
                _rings[r].SetPosition(i, Dir(a) * d);
            }
        }
    }

    void RebuildPulse(float dt, bool alert)
    {
        float dur = alert ? PulseDurationAlert : PulseDurationPatrol;
        _pulseT += dt / dur;
        if (_pulseT > 1f) _pulseT -= 1f;

        float vd     = _enemy.visionDistance;
        float target = _pulseT * vd;
        float h      = ViewAngle * 0.5f;
        float step   = ViewAngle / Segments;

        for (int i = 0; i <= Segments; i++)
        {
            float a = (-h + step * i) * Mathf.Deg2Rad;
            float d = Mathf.Min(target, _hitDists[i]);
            _pulse.SetPosition(i, Dir(a) * d);
        }

        float fade   = Mathf.Clamp01(Mathf.Sin(_pulseT * Mathf.PI));
        Color tPulse = alert ? AlertPulse : PatrolPulse;
        Color pc     = tPulse; pc.a = tPulse.a * fade * _flickerVal;
        SetLine(_pulse, pc, Mathf.Lerp(0.13f, 0.04f, _pulseT));
    }

    // =========================================================================
    //  Build helpers (initial build in Awake)
    // =========================================================================
    void BuildFill()
    {
        var go        = Child("Fill");
        var mf        = go.AddComponent<MeshFilter>();
        _fillRenderer = go.AddComponent<MeshRenderer>();
        _fillRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _fillRenderer.receiveShadows    = false;
        _fillMat       = TransparentMat(doubleSided: true);
        _fillMat.color = PatrolFill;
        _fillRenderer.sharedMaterial = _fillMat;
        _fillMesh      = new Mesh { name = "FOV" };
        mf.mesh        = _fillMesh;
        RebuildFillMesh();
    }

    void BuildBoundary()
    {
        float vd = _enemy.visionDistance;
        var   go = Child("Boundary");
        float h  = ViewAngle * 0.5f * Mathf.Deg2Rad;

        _edgeLeft  = Line(go, "L",   2,          0.07f);
        _edgeLeft.SetPosition(0, Vector3.zero);
        _edgeLeft.SetPosition(1, Dir(-h) * vd);

        _edgeRight = Line(go, "R",   2,          0.07f);
        _edgeRight.SetPosition(0, Vector3.zero);
        _edgeRight.SetPosition(1, Dir( h) * vd);

        _edgeArc = Line(go, "Arc", Segments + 1, 0.07f);
        BuildArc(_edgeArc, vd);

        SetLine(_edgeLeft,  PatrolEdge, 0.07f);
        SetLine(_edgeRight, PatrolEdge, 0.07f);
        SetLine(_edgeArc,   PatrolEdge, 0.07f);
    }

    void BuildRings()
    {
        if (RingCount <= 0) return;
        float vd = _enemy.visionDistance;
        var go   = Child("Rings");
        _rings   = new LineRenderer[RingCount];
        for (int i = 0; i < RingCount; i++)
        {
            _rings[i] = Line(go, "Ring" + i, Segments + 1, 0.025f);
            BuildArc(_rings[i], vd * (i + 1f) / (RingCount + 1f));
            SetLine(_rings[i], PatrolRing, 0.025f);
        }
    }

    void BuildRadials()
    {
        if (RadialCount <= 0) return;
        float vd  = _enemy.visionDistance;
        var   go  = Child("Radials");
        _radials  = new LineRenderer[RadialCount];
        float h   = ViewAngle * 0.5f;
        for (int i = 0; i < RadialCount; i++)
        {
            float a = (-h + (i + 1f) / (RadialCount + 1f) * ViewAngle) * Mathf.Deg2Rad;
            _radials[i] = Line(go, "Rad" + i, 2, 0.020f);
            _radials[i].SetPosition(0, Vector3.zero);
            _radials[i].SetPosition(1, Dir(a) * vd);
            Color rc = PatrolRing; rc.a *= 0.55f;
            SetLine(_radials[i], rc, 0.020f);
        }
    }

    void BuildPulse()
    {
        var go = Child("Pulse");
        _pulse = Line(go, "P", Segments + 1, 0.10f);
        BuildArc(_pulse, 0.01f);
        SetLine(_pulse, PatrolPulse, 0.10f);
    }

    void BuildBrackets()
    {
        if (BracketFraction <= 0f) return;
        float vd  = _enemy.visionDistance;
        float h   = ViewAngle * 0.5f * Mathf.Deg2Rad;
        float len = vd * BracketFraction;
        var   go  = Child("Brackets");
        _brackets = new LineRenderer[4];

        Vector3 cL = Dir(-h) * vd;
        _brackets[0] = LinePair(go, "BL0", cL, cL + (Dir(-h + 0.001f) * vd - cL).normalized * len);
        _brackets[1] = LinePair(go, "BL1", cL, cL + (-Dir(-h)) * len);

        Vector3 cR = Dir(h) * vd;
        _brackets[2] = LinePair(go, "BR0", cR, cR + (Dir(h - 0.001f) * vd - cR).normalized * len);
        _brackets[3] = LinePair(go, "BR1", cR, cR + (-Dir(h)) * len);

        foreach (var b in _brackets) SetLine(b, PatrolEdge, 0.06f);
    }

    void BuildLight()
    {
        var go = new GameObject("[DroneLight]");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        _light              = go.AddComponent<Light>();
        _light.type         = LightType.Point;
        _light.color        = PatrolLight;
        _light.intensity    = 1.8f;
        _light.range        = LightRangePatrol;
        _light.shadows      = LightShadows.None;
    }

    // =========================================================================
    //  Geometry helpers
    // =========================================================================
    void BuildArc(LineRenderer lr, float dist)
    {
        float h    = ViewAngle * 0.5f;
        float step = ViewAngle / Segments;
        lr.positionCount = Segments + 1;
        for (int i = 0; i <= Segments; i++)
            lr.SetPosition(i, Dir((-h + step * i) * Mathf.Deg2Rad) * dist);
    }

    static Vector3 Dir(float radians) =>
        new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));

    // =========================================================================
    //  LineRenderer helpers
    // =========================================================================
    LineRenderer Line(GameObject parent, string n, int pts, float w)
    {
        var go = new GameObject(n);
        go.transform.SetParent(parent.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount     = pts;
        lr.startWidth        = w;
        lr.endWidth          = w;
        lr.useWorldSpace     = false;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.numCapVertices    = 4;
        lr.material          = TransparentMat(doubleSided: false);
        return lr;
    }

    LineRenderer LinePair(GameObject parent, string n, Vector3 a, Vector3 b)
    {
        var lr = Line(parent, n, 2, 0.06f);
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        return lr;
    }

    static void SetLine(LineRenderer lr, Color c, float w)
    {
        if (lr == null) return;
        lr.startColor      = c;
        lr.endColor        = c;
        lr.widthMultiplier = w;
    }

    // =========================================================================
    //  Material helper
    // =========================================================================
    static Material TransparentMat(bool doubleSided)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.SetFloat("_Surface",   1f);
        mat.SetFloat("_Blend",     0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_Cull",      doubleSided ? 0f : 2f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",   0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        return mat;
    }

    GameObject Child(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(_root.transform, false);
        return go;
    }
}
