using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class WallGrid : MonoBehaviour
{
    public const float GridSize   = 1f;
    public const float WallHeight = 1f;

    [SerializeField] List<Vector2Int> _cells = new List<Vector2Int>();

    HashSet<Vector2Int> _set;

    MeshFilter   _filter;
    MeshCollider _col;

    void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _col    = GetComponent<MeshCollider>();
        SyncSet();
        RebuildMesh();
    }

    void SyncSet()
    {
        _set = new HashSet<Vector2Int>(_cells);
    }

    public bool HasCell(Vector2Int c)
    {
        if (_set == null) SyncSet();
        return _set.Contains(c);
    }

    public void AddCell(Vector2Int c)
    {
        if (_set == null) SyncSet();
        if (!_set.Add(c)) return;
        _cells.Add(c);
        RebuildMesh();
    }

    public void RemoveCell(Vector2Int c)
    {
        if (_set == null) SyncSet();
        if (!_set.Remove(c)) return;
        _cells.Remove(c);
        RebuildMesh();
    }

    public void RebuildMesh()
    {
        if (_filter == null) _filter = GetComponent<MeshFilter>();
        if (_col    == null) _col    = GetComponent<MeshCollider>();
        if (_set    == null) SyncSet();

        var verts   = new List<Vector3>();
        var tris    = new List<int>();
        var uvs     = new List<Vector2>();
        var normals = new List<Vector3>();

        foreach (var c in _set)
        {
            float x0 = c.x * GridSize - GridSize * 0.5f;
            float x1 = c.x * GridSize + GridSize * 0.5f;
            float z0 = c.y * GridSize - GridSize * 0.5f; 
            float z1 = c.y * GridSize + GridSize * 0.5f;
            const float y0 = 0f, y1 = WallHeight;


            AddFace(verts, tris, uvs, normals,
                new Vector3(x0, y1, z0), new Vector3(x0, y1, z1),
                new Vector3(x1, y1, z1), new Vector3(x1, y1, z0),
                Vector3.up, GridSize, GridSize);

            if (!_set.Contains(new Vector2Int(c.x, c.y + 1)))
                AddFace(verts, tris, uvs, normals,
                    new Vector3(x1, y0, z1), new Vector3(x1, y1, z1),
                    new Vector3(x0, y1, z1), new Vector3(x0, y0, z1),
                    Vector3.forward, GridSize, WallHeight);

            if (!_set.Contains(new Vector2Int(c.x, c.y - 1)))
                AddFace(verts, tris, uvs, normals,
                    new Vector3(x0, y0, z0), new Vector3(x0, y1, z0),
                    new Vector3(x1, y1, z0), new Vector3(x1, y0, z0),
                    Vector3.back, GridSize, WallHeight);

            if (!_set.Contains(new Vector2Int(c.x + 1, c.y)))
                AddFace(verts, tris, uvs, normals,
                    new Vector3(x1, y0, z0), new Vector3(x1, y1, z0),
                    new Vector3(x1, y1, z1), new Vector3(x1, y0, z1),
                    Vector3.right, GridSize, WallHeight);

            if (!_set.Contains(new Vector2Int(c.x - 1, c.y)))
                AddFace(verts, tris, uvs, normals,
                    new Vector3(x0, y0, z1), new Vector3(x0, y1, z1),
                    new Vector3(x0, y1, z0), new Vector3(x0, y0, z0),
                    Vector3.left, GridSize, WallHeight);
        }

        var mesh = _filter.sharedMesh;
        if (mesh == null || mesh.name != "WallGrid_Mesh")
        {
            mesh = new Mesh { name = "WallGrid_Mesh" };
            _filter.sharedMesh = mesh;
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();

        _col.sharedMesh = null;
        _col.sharedMesh = mesh;
    }

    static void AddFace(
        List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Vector3> normals,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 normal, float uSize, float vSize)
    {
        int i = verts.Count;
        verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);

        float uTile = uSize / GridSize;
        float vTile = vSize / GridSize;
        uvs.Add(new Vector2(0,     0    ));
        uvs.Add(new Vector2(0,     vTile));
        uvs.Add(new Vector2(uTile, vTile));
        uvs.Add(new Vector2(uTile, 0    ));

        normals.Add(normal); normals.Add(normal);
        normals.Add(normal); normals.Add(normal);

        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }
}
