using UnityEngine;

[DisallowMultipleComponent]
public class ScanBarController : MonoBehaviour
{
    static readonly Color ColBg     = new Color(0.10f, 0.00f, 0.00f, 0.80f);
    static readonly Color ColFill   = new Color(1.00f, 0.08f, 0.04f, 0.90f);
    static readonly Color ColBorder = new Color(1.00f, 0.30f, 0.10f, 0.90f);
    static readonly Color ColText   = new Color(1.00f, 0.30f, 0.10f, 1.00f);

    const float BAR_W = 1.6f;
    const float BAR_H = 0.10f;

    Mesh         _fillMesh;
    Material     _fillMat;
    TextMesh     _label;
    Transform    _cam;
    float        _flickerT;
    float        _progress;

    // ── Factory ──────────────────────────────────────────────────────────────

    public static ScanBarController Create(Transform parent)
    {
        var go = new GameObject("[ScanBar]");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        return go.AddComponent<ScanBarController>();
    }

    // ── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        _cam = UnityEngine.Camera.main?.transform;
        Build();
    }

    void LateUpdate()
    {
        if (_cam != null)
            transform.LookAt(
                transform.position + _cam.rotation * Vector3.forward,
                _cam.rotation * Vector3.up);

        _flickerT += Time.deltaTime;
        float f = 0.75f
                + Mathf.Sin(_flickerT * 11.3f) * 0.12f
                + Mathf.Sin(_flickerT * 23.7f) * 0.13f;
        _fillMat.color = new Color(ColFill.r, ColFill.g, ColFill.b, ColFill.a * f);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void SetProgress(float t)
    {
        _progress = Mathf.Clamp01(t);
        RebuildFillMesh();
        if (_label != null)
            _label.text = $"[ SCANNING... {Mathf.RoundToInt(_progress * 100):D3}% ]";
    }

    // ── Build ────────────────────────────────────────────────────────────────

    void Build()
    {
        BuildBgMesh();
        BuildFillMesh();
        BuildFrame();
        BuildBrackets();
        BuildLabel();
    }

    void BuildBgMesh()
    {
        var go = Child("BG");
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        mr.sharedMaterial    = MakeMat(ColBg, 3000);

        float hw = BAR_W / 2f, hh = BAR_H / 2f;
        var mesh = new Mesh();
        mesh.vertices  = new[] {
            new Vector3(-hw, -hh, 0), new Vector3(-hw, hh, 0),
            new Vector3( hw,  hh, 0), new Vector3( hw,-hh, 0)
        };
        mesh.triangles = new[] { 0,1,2, 0,2,3 };
        mesh.RecalculateBounds();
        mf.mesh = mesh;
    }

    void BuildFillMesh()
    {
        var go = Child("Fill");
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        _fillMat             = MakeMat(ColFill, 3001);
        mr.sharedMaterial    = _fillMat;
        _fillMesh            = new Mesh { name = "ScanFill" };
        mf.mesh              = _fillMesh;
    }

    void RebuildFillMesh()
    {
        _fillMesh.Clear();
        if (_progress <= 0f) return;

        float left  = -BAR_W / 2f;
        float right = left + BAR_W * _progress;
        float hh    = BAR_H * 0.4f;

        _fillMesh.vertices = new[] {
            new Vector3(left,  -hh, -0.01f), new Vector3(left,  hh, -0.01f),
            new Vector3(right,  hh, -0.01f), new Vector3(right,-hh, -0.01f)
        };
        _fillMesh.triangles = new[] { 0,1,2, 0,2,3 };
        _fillMesh.RecalculateBounds();
    }

    void BuildFrame()
    {
        float hw = BAR_W / 2f, hh = BAR_H / 2f;
        float w  = 0.018f;
        MakeLine("FrT", new Vector3(-hw,  hh, 0), new Vector3( hw,  hh, 0), w);
        MakeLine("FrB", new Vector3(-hw, -hh, 0), new Vector3( hw, -hh, 0), w);
        MakeLine("FrL", new Vector3(-hw, -hh, 0), new Vector3(-hw,  hh, 0), w);
        MakeLine("FrR", new Vector3( hw, -hh, 0), new Vector3( hw,  hh, 0), w);
    }

    void BuildBrackets()
    {
        float hw  = BAR_W / 2f + 0.05f;
        float hh  = BAR_H / 2f + 0.05f;
        float arm = 0.18f;
        float w   = 0.025f;

        MakeLine("TL_H", new Vector3(-hw,  hh, 0), new Vector3(-hw + arm,  hh, 0), w);
        MakeLine("TL_V", new Vector3(-hw,  hh, 0), new Vector3(-hw,  hh - arm, 0), w);
        MakeLine("TR_H", new Vector3( hw,  hh, 0), new Vector3( hw - arm,  hh, 0), w);
        MakeLine("TR_V", new Vector3( hw,  hh, 0), new Vector3( hw,  hh - arm, 0), w);
        MakeLine("BL_H", new Vector3(-hw, -hh, 0), new Vector3(-hw + arm, -hh, 0), w);
        MakeLine("BL_V", new Vector3(-hw, -hh, 0), new Vector3(-hw, -hh + arm, 0), w);
        MakeLine("BR_H", new Vector3( hw, -hh, 0), new Vector3( hw - arm, -hh, 0), w);
        MakeLine("BR_V", new Vector3( hw, -hh, 0), new Vector3( hw, -hh + arm, 0), w);
    }

    void BuildLabel()
    {
        var go = Child("Label");
        go.transform.localPosition = new Vector3(0f, BAR_H / 2f + 0.10f, 0f);
        go.transform.localScale    = Vector3.one * 0.055f;
        _label           = go.AddComponent<TextMesh>();
        _label.text      = "[ SCANNING... 000% ]";
        _label.color     = ColText;
        _label.fontSize  = 20;
        _label.fontStyle = FontStyle.Bold;
        _label.anchor    = TextAnchor.MiddleCenter;
        _label.alignment = TextAlignment.Center;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void MakeLine(string name, Vector3 a, Vector3 b, float width)
    {
        var go = Child(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount     = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.useWorldSpace     = false;
        lr.startWidth        = width;
        lr.endWidth          = width;
        lr.startColor        = ColBorder;
        lr.endColor          = ColBorder;
        lr.numCapVertices    = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.material          = MakeMat(ColBorder, 3002);
    }

    GameObject Child(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        return go;
    }

    static Material MakeMat(Color col, int queue)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.color = col;
        mat.SetFloat("_Surface",   1f);
        mat.SetFloat("_Blend",     0f);
        mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",    0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = queue;
        return mat;
    }
}
