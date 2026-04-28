using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class DodecahedronMeshGenerator : MonoBehaviour
{
    [Tooltip("Circumradius of the dodecahedron in local space")]
    public float radius = 0.58f;

    void Awake()
    {
        GetComponent<MeshFilter>().sharedMesh = Build(radius);
    }

    public static Mesh Build(float radius = 0.58f)
    {
        float phi  = (1f + Mathf.Sqrt(5f)) / 2f;
        float iPhi = 1f / phi;
        // Vertex circumradius in canonical coords is sqrt(3); scale to target
        float s = radius / Mathf.Sqrt(3f);

        // 20 canonical dodecahedron vertices
        Vector3[] v = new Vector3[]
        {
            new Vector3( 1,  1,  1) * s, // 0
            new Vector3( 1,  1, -1) * s, // 1
            new Vector3( 1, -1,  1) * s, // 2
            new Vector3( 1, -1, -1) * s, // 3
            new Vector3(-1,  1,  1) * s, // 4
            new Vector3(-1,  1, -1) * s, // 5
            new Vector3(-1, -1,  1) * s, // 6
            new Vector3(-1, -1, -1) * s, // 7
            new Vector3( 0,  iPhi,  phi) * s, // 8
            new Vector3( 0,  iPhi, -phi) * s, // 9
            new Vector3( 0, -iPhi,  phi) * s, // 10
            new Vector3( 0, -iPhi, -phi) * s, // 11
            new Vector3( iPhi,  phi, 0) * s,  // 12
            new Vector3( iPhi, -phi, 0) * s,  // 13
            new Vector3(-iPhi,  phi, 0) * s,  // 14
            new Vector3(-iPhi, -phi, 0) * s,  // 15
            new Vector3( phi, 0,  iPhi) * s,  // 16
            new Vector3( phi, 0, -iPhi) * s,  // 17
            new Vector3(-phi, 0,  iPhi) * s,  // 18
            new Vector3(-phi, 0, -iPhi) * s,  // 19
        };

        // 12 pentagonal faces — CCW winding for outward normals
        int[][] faces = new int[][]
        {
            new int[] { 0,  8, 10,  2, 16 },
            new int[] { 0, 16, 17,  1, 12 },
            new int[] { 0, 12, 14,  4,  8 },
            new int[] { 1,  9,  5, 14, 12 },
            new int[] { 1, 17,  3, 11,  9 },
            new int[] { 2, 10,  6, 15, 13 },
            new int[] { 2, 13,  3, 17, 16 },
            new int[] { 3, 13, 15,  7, 11 },
            new int[] { 4, 14,  5, 19, 18 },
            new int[] { 4, 18,  6, 10,  8 },
            new int[] { 5,  9, 11,  7, 19 },
            new int[] { 6, 18, 19,  7, 15 },
        };

        // Flat-shaded: duplicate vertices per face so each face has its own normal
        var verts   = new List<Vector3>(60);
        var normals = new List<Vector3>(60);
        var tris    = new List<int>(108);

        foreach (var face in faces)
        {
            int baseIdx = verts.Count;

            // Face normal via cross product of first two edges
            Vector3 e1     = v[face[1]] - v[face[0]];
            Vector3 e2     = v[face[2]] - v[face[0]];
            Vector3 normal = Vector3.Cross(e1, e2).normalized;

            for (int i = 0; i < 5; i++)
            {
                verts.Add(v[face[i]]);
                normals.Add(normal);
            }

            // Fan triangulation from vertex 0 of each pentagon
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 4);
        }

        Mesh mesh      = new Mesh();
        mesh.name      = "Dodecahedron";
        mesh.vertices  = verts.ToArray();
        mesh.normals   = normals.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }
}
