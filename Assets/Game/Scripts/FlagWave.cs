using System.Collections.Generic;
using UnityEngine;

namespace VictorianAristocrat
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FlagWave : MonoBehaviour
    {
        Mesh mesh;
        Vector3[] vertices;

        void Awake()
        {
            mesh = new Mesh { name = "Country Flag" };
            mesh.MarkDynamic();
            vertices = new Vector3[30];
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 10; col++)
                    vertices[row * 10 + col] = new Vector3(col * .34f, .55f - row * .55f, 0);
            mesh.vertices = vertices;
            var upper = new List<int>();
            var lower = new List<int>();
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 9; col++)
                {
                    int a = row * 10 + col, b = a + 1, c = a + 10, d = c + 1;
                    var triangles = row == 0 ? upper : lower;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    triangles.Add(b); triangles.Add(c); triangles.Add(a);
                    triangles.Add(d); triangles.Add(c); triangles.Add(b);
                }
            mesh.subMeshCount = 2;
            mesh.SetTriangles(upper, 0);
            mesh.SetTriangles(lower, 1);
            mesh.RecalculateNormals();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        public void SetColors(Color primary, Color secondary)
        {
            GetComponent<MeshRenderer>().materials = new[] { WorldRuntime.Material(primary), WorldRuntime.Material(secondary) };
        }

        void Update()
        {
            if (mesh == null) return;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 10; col++)
                {
                    int index = row * 10 + col;
                    var v = vertices[index];
                    v.z = Mathf.Sin(Time.time * 3f - col * .65f) * .22f * (col / 9f);
                    vertices[index] = v;
                }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
