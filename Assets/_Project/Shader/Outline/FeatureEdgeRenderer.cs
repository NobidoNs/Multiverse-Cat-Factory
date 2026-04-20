using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class FeatureEdgeRenderer : MonoBehaviour
{
    [SerializeField] private Material edgeMaterial;
    [SerializeField, Range(0f, 180f)] private float angleThreshold = 35f;
    [SerializeField] private bool includeBoundaryEdges = true;
    [SerializeField] private string childName = "__FeatureEdges";

    private MeshFilter sourceMeshFilter;
    private MeshRenderer edgeRenderer;
    private MeshFilter edgeMeshFilter;
    private Mesh cachedSourceMesh;
    private Mesh cachedBuiltMesh;
    private float cachedAngleThreshold = -1f;
    private bool cachedIncludeBoundaryEdges;
    private bool pendingRefresh;

    private void OnEnable()
    {
        pendingRefresh = true;
        EnsureChild();
        SyncNow();
    }

    private void OnValidate()
    {
        pendingRefresh = true;
    }

    private void Update()
    {
        if (!pendingRefresh)
        {
            return;
        }

        pendingRefresh = false;
        EnsureChild();
        SyncNow();
    }

    private void OnDisable()
    {
        if (edgeRenderer != null)
        {
            edgeRenderer.enabled = false;
        }
    }

    [ContextMenu("Sync Feature Edges")]
    public void SyncNow()
    {
        if (sourceMeshFilter == null)
        {
            sourceMeshFilter = GetComponent<MeshFilter>();
        }

        if (edgeRenderer == null || edgeMeshFilter == null)
        {
            EnsureChild();
        }

        if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null || edgeRenderer == null || edgeMeshFilter == null)
        {
            return;
        }

        Mesh sourceMesh = sourceMeshFilter.sharedMesh;
        if (!sourceMesh.isReadable)
        {
            edgeRenderer.enabled = false;
            return;
        }

        edgeRenderer.enabled = enabled && gameObject.activeInHierarchy && edgeMaterial != null;
        if (edgeMaterial == null)
        {
            return;
        }

        edgeRenderer.sharedMaterial = edgeMaterial;
        edgeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        edgeRenderer.receiveShadows = false;
        edgeRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        edgeRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        edgeRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        if (cachedBuiltMesh == null ||
            cachedSourceMesh != sourceMesh ||
            !Mathf.Approximately(cachedAngleThreshold, angleThreshold) ||
            cachedIncludeBoundaryEdges != includeBoundaryEdges)
        {
            cachedBuiltMesh = BuildFeatureEdgeMesh(sourceMesh, angleThreshold, includeBoundaryEdges);
            cachedSourceMesh = sourceMesh;
            cachedAngleThreshold = angleThreshold;
            cachedIncludeBoundaryEdges = includeBoundaryEdges;
        }

        edgeMeshFilter.sharedMesh = cachedBuiltMesh;
    }

    private void EnsureChild()
    {
        sourceMeshFilter = GetComponent<MeshFilter>();

        Transform child = transform.Find(childName);
        if (child == null)
        {
            var childObject = CreateChildObject(childName);
            child = childObject.transform;
            SetChildParent(child, transform);
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;

        edgeMeshFilter = child.GetComponent<MeshFilter>();
        if (edgeMeshFilter == null)
        {
            edgeMeshFilter = AddChildComponent<MeshFilter>(child.gameObject);
        }

        edgeRenderer = child.GetComponent<MeshRenderer>();
        if (edgeRenderer == null)
        {
            edgeRenderer = AddChildComponent<MeshRenderer>(child.gameObject);
        }
    }

    public void Configure(Material material, float thresholdDegrees, bool boundaryEdges)
    {
        edgeMaterial = material;
        angleThreshold = thresholdDegrees;
        includeBoundaryEdges = boundaryEdges;
        pendingRefresh = true;
    }

    public static Mesh BuildFeatureEdgeMesh(Mesh source, float thresholdDegrees, bool includeBoundary)
    {
        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        int[] triangles = source.triangles;
        int triangleCount = triangles.Length / 3;

        var faceNormals = new Vector3[triangleCount];
        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            int baseIndex = triangleIndex * 3;
            Vector3 a = vertices[triangles[baseIndex + 0]];
            Vector3 b = vertices[triangles[baseIndex + 1]];
            Vector3 c = vertices[triangles[baseIndex + 2]];
            faceNormals[triangleIndex] = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        }

        var edges = new Dictionary<EdgeKey, List<EdgeUse>>(triangles.Length);
        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            int baseIndex = triangleIndex * 3;
            int i0 = triangles[baseIndex + 0];
            int i1 = triangles[baseIndex + 1];
            int i2 = triangles[baseIndex + 2];

            AddEdge(edges, new EdgeKey(i0, i1), new EdgeUse(triangleIndex, i0, i1));
            AddEdge(edges, new EdgeKey(i1, i2), new EdgeUse(triangleIndex, i1, i2));
            AddEdge(edges, new EdgeKey(i2, i0), new EdgeUse(triangleIndex, i2, i0));
        }

        var lineStarts = new List<Vector3>();
        var lineEnds = new List<Vector3>();
        var sides = new List<Vector2>();
        var lineTriangles = new List<int>();

        foreach (KeyValuePair<EdgeKey, List<EdgeUse>> pair in edges)
        {
            List<EdgeUse> uses = pair.Value;
            bool keepEdge = false;

            if (uses.Count == 1)
            {
                keepEdge = includeBoundary;
            }
            else if (uses.Count == 2)
            {
                float faceAngle = Vector3.Angle(faceNormals[uses[0].TriangleIndex], faceNormals[uses[1].TriangleIndex]);
                float smoothAngle = faceAngle;

                if (normals != null && normals.Length == vertices.Length)
                {
                    Vector3 edgeNormalA = (normals[uses[0].IndexA] + normals[uses[0].IndexB]).normalized;
                    Vector3 edgeNormalB = (normals[uses[1].IndexA] + normals[uses[1].IndexB]).normalized;
                    smoothAngle = Vector3.Angle(edgeNormalA, edgeNormalB);
                }

                keepEdge = Mathf.Max(faceAngle, smoothAngle) >= thresholdDegrees;
            }
            else
            {
                keepEdge = true;
            }

            if (!keepEdge)
            {
                continue;
            }

            Vector3 start = vertices[uses[0].IndexA];
            Vector3 end = vertices[uses[0].IndexB];
            if ((end - start).sqrMagnitude < 1e-10f)
            {
                continue;
            }

            int baseVertex = lineStarts.Count;

            lineStarts.Add(start);
            lineEnds.Add(end);
            sides.Add(new Vector2(-1f, 0f));

            lineStarts.Add(start);
            lineEnds.Add(end);
            sides.Add(new Vector2(1f, 0f));

            lineStarts.Add(end);
            lineEnds.Add(start);
            sides.Add(new Vector2(-1f, 1f));

            lineStarts.Add(end);
            lineEnds.Add(start);
            sides.Add(new Vector2(1f, 1f));

            lineTriangles.Add(baseVertex + 0);
            lineTriangles.Add(baseVertex + 1);
            lineTriangles.Add(baseVertex + 2);

            lineTriangles.Add(baseVertex + 2);
            lineTriangles.Add(baseVertex + 1);
            lineTriangles.Add(baseVertex + 3);
        }

        var mesh = new Mesh
        {
            name = source.name + " (Feature Edge Lines)"
        };

        if (lineStarts.Count > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.SetVertices(lineStarts);
        mesh.SetUVs(0, lineEnds);
        mesh.SetUVs(1, sides);
        mesh.SetTriangles(lineTriangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddEdge(Dictionary<EdgeKey, List<EdgeUse>> edges, EdgeKey key, EdgeUse use)
    {
        if (!edges.TryGetValue(key, out List<EdgeUse> list))
        {
            list = new List<EdgeUse>(2);
            edges.Add(key, list);
        }

        list.Add(use);
    }

    private readonly struct EdgeUse
    {
        public EdgeUse(int triangleIndex, int indexA, int indexB)
        {
            TriangleIndex = triangleIndex;
            IndexA = indexA;
            IndexB = indexB;
        }

        public int TriangleIndex { get; }
        public int IndexA { get; }
        public int IndexB { get; }
    }

    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        private readonly int a;
        private readonly int b;

        public EdgeKey(int i0, int i1)
        {
            if (i0 < i1)
            {
                a = i0;
                b = i1;
            }
            else
            {
                a = i1;
                b = i0;
            }
        }

        public bool Equals(EdgeKey other)
        {
            return a == other.a && b == other.b;
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (a * 397) ^ b;
            }
        }
    }

    private static GameObject CreateChildObject(string objectName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var childObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(childObject, "Create Feature Edge Child");
            return childObject;
        }
#endif
        return new GameObject(objectName);
    }

    private static void SetChildParent(Transform child, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.SetTransformParent(child, parent, "Parent Feature Edge Child");
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return;
        }
#endif
        child.SetParent(parent, false);
    }

    private static T AddChildComponent<T>(GameObject target) where T : Component
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return Undo.AddComponent<T>(target);
        }
#endif
        return target.AddComponent<T>();
    }
}
