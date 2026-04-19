using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public sealed class SilhouetteOutlineRenderer : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private string childName = "__SilhouetteOutline";

    private Renderer sourceRenderer;
    private Renderer outlineRenderer;
    private MeshFilter sourceMeshFilter;
    private MeshFilter outlineMeshFilter;
    private SkinnedMeshRenderer sourceSkinnedMesh;
    private SkinnedMeshRenderer outlineSkinnedMesh;
    private Mesh cachedSourceMesh;
    private Mesh cachedSmoothMesh;
    private bool pendingRefresh;

    private void OnEnable()
    {
        pendingRefresh = true;
        EnsureOutlineChild();
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
        EnsureOutlineChild();
        SyncNow();
    }

    private void OnDisable()
    {
        if (outlineRenderer != null)
        {
            outlineRenderer.enabled = false;
        }
    }

    private void EnsureOutlineChild()
    {
        sourceRenderer = GetComponent<Renderer>();
        sourceMeshFilter = GetComponent<MeshFilter>();
        sourceSkinnedMesh = GetComponent<SkinnedMeshRenderer>();

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

        if (sourceSkinnedMesh != null)
        {
            EnsureSkinnedOutline(child.gameObject);
        }
        else
        {
            EnsureMeshOutline(child.gameObject);
        }
    }

    private void EnsureMeshOutline(GameObject childObject)
    {
        outlineSkinnedMesh = null;
        var existingSkinned = childObject.GetComponent<SkinnedMeshRenderer>();
        if (existingSkinned != null)
        {
            DestroyImmediate(existingSkinned);
        }

        outlineMeshFilter = childObject.GetComponent<MeshFilter>();
        if (outlineMeshFilter == null)
        {
            outlineMeshFilter = AddChildComponent<MeshFilter>(childObject);
        }

        outlineRenderer = childObject.GetComponent<MeshRenderer>();
        if (outlineRenderer == null)
        {
            outlineRenderer = AddChildComponent<MeshRenderer>(childObject);
        }
    }

    private void EnsureSkinnedOutline(GameObject childObject)
    {
        outlineMeshFilter = null;
        var existingMeshRenderer = childObject.GetComponent<MeshRenderer>();
        if (existingMeshRenderer != null)
        {
            DestroyImmediate(existingMeshRenderer);
        }

        var existingMeshFilter = childObject.GetComponent<MeshFilter>();
        if (existingMeshFilter != null)
        {
            DestroyImmediate(existingMeshFilter);
        }

        outlineSkinnedMesh = childObject.GetComponent<SkinnedMeshRenderer>();
        if (outlineSkinnedMesh == null)
        {
            outlineSkinnedMesh = AddChildComponent<SkinnedMeshRenderer>(childObject);
        }

        outlineRenderer = outlineSkinnedMesh;
    }

    [ContextMenu("Sync Outline Now")]
    public void SyncNow()
    {
        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<Renderer>();
        }

        if (outlineRenderer == null)
        {
            EnsureOutlineChild();
        }

        if (outlineRenderer == null)
        {
            return;
        }

        outlineRenderer.enabled = enabled && gameObject.activeInHierarchy && outlineMaterial != null;

        if (outlineMaterial == null)
        {
            return;
        }

        outlineRenderer.sharedMaterial = outlineMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        outlineRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        outlineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        if (sourceMeshFilter != null && outlineMeshFilter != null)
        {
            Mesh sourceMesh = sourceMeshFilter.sharedMesh;
            if (sourceMesh != null && sourceMesh.isReadable)
            {
                if (cachedSmoothMesh == null || cachedSourceMesh != sourceMesh)
                {
                    cachedSmoothMesh = BuildSmoothOutlineMesh(sourceMesh);
                    cachedSourceMesh = sourceMesh;
                }

                outlineMeshFilter.sharedMesh = cachedSmoothMesh;
            }
            else
            {
                outlineMeshFilter.sharedMesh = sourceMesh;
            }
        }

        if (sourceSkinnedMesh != null && outlineSkinnedMesh != null)
        {
            outlineSkinnedMesh.sharedMesh = sourceSkinnedMesh.sharedMesh;
            outlineSkinnedMesh.rootBone = sourceSkinnedMesh.rootBone;
            outlineSkinnedMesh.bones = sourceSkinnedMesh.bones;
            outlineSkinnedMesh.localBounds = sourceSkinnedMesh.localBounds;
            outlineSkinnedMesh.updateWhenOffscreen = sourceSkinnedMesh.updateWhenOffscreen;
            outlineSkinnedMesh.quality = sourceSkinnedMesh.quality;
            outlineSkinnedMesh.skinnedMotionVectors = false;
        }
    }

    public void Configure(Material material)
    {
        outlineMaterial = material;
        pendingRefresh = true;
    }

    private static Mesh BuildSmoothOutlineMesh(Mesh source)
    {
        Mesh smoothMesh = Object.Instantiate(source);
        var vertices = smoothMesh.vertices;
        var sourceNormals = smoothMesh.normals;
        var smoothedNormals = new Vector3[vertices.Length];
        var groups = new Dictionary<PositionKey, List<int>>(vertices.Length);

        for (int i = 0; i < vertices.Length; i++)
        {
            var key = new PositionKey(vertices[i]);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<int>(4);
                groups.Add(key, list);
            }

            list.Add(i);
        }

        foreach (var pair in groups)
        {
            Vector3 normalSum = Vector3.zero;
            var indices = pair.Value;
            for (int i = 0; i < indices.Count; i++)
            {
                normalSum += sourceNormals[indices[i]];
            }

            Vector3 smoothNormal = normalSum.sqrMagnitude > 1e-8f ? normalSum.normalized : Vector3.up;
            for (int i = 0; i < indices.Count; i++)
            {
                smoothedNormals[indices[i]] = smoothNormal;
            }
        }

        smoothMesh.normals = smoothedNormals;
        smoothMesh.name = source.name + " (Smooth Outline)";
        return smoothMesh;
    }

    private readonly struct PositionKey : System.IEquatable<PositionKey>
    {
        private const float Precision = 10000f;
        private readonly int x;
        private readonly int y;
        private readonly int z;

        public PositionKey(Vector3 position)
        {
            x = Mathf.RoundToInt(position.x * Precision);
            y = Mathf.RoundToInt(position.y * Precision);
            z = Mathf.RoundToInt(position.z * Precision);
        }

        public bool Equals(PositionKey other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is PositionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = x;
                hash = (hash * 397) ^ y;
                hash = (hash * 397) ^ z;
                return hash;
            }
        }
    }

    private static GameObject CreateChildObject(string objectName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var childObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(childObject, "Create Silhouette Child");
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
            Undo.SetTransformParent(child, parent, "Parent Silhouette Child");
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
