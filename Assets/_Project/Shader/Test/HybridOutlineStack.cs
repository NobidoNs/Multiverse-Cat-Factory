using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public sealed class HybridOutlineStack : MonoBehaviour
{
    [Header("Silhouette")]
    [SerializeField] private Material silhouetteMaterial;

    [Header("Feature Edges")]
    [SerializeField] private Material featureEdgeMaterial;
    [SerializeField, Tooltip("Edges are drawn only when the angle between adjacent faces is greater than or equal to this value.")]
    [Range(0f, 180f)] private float angleThreshold = 35f;
    [SerializeField] private bool includeBoundaryEdges = true;

    private SilhouetteOutlineRenderer silhouetteRenderer;
    private FeatureEdgeRenderer featureEdgeRenderer;
    private bool pendingRefresh;

    private void OnEnable()
    {
#if UNITY_EDITOR
        AutoAssignMaterials();
#endif
        pendingRefresh = true;
        EnsureComponents();
        SyncNow();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        AutoAssignMaterials();
#endif
        pendingRefresh = true;
    }

    private void Reset()
    {
#if UNITY_EDITOR
        AutoAssignMaterials();
#endif
        pendingRefresh = true;
    }

    private void Update()
    {
        if (!pendingRefresh)
        {
            return;
        }

        pendingRefresh = false;
        EnsureComponents();
        SyncNow();
    }

    [ContextMenu("Sync Hybrid Outline Stack")]
    public void SyncNow()
    {
        EnsureComponents();

        if (silhouetteRenderer != null)
        {
            silhouetteRenderer.Configure(silhouetteMaterial);
            silhouetteRenderer.SyncNow();
        }

        if (featureEdgeRenderer != null)
        {
            featureEdgeRenderer.Configure(featureEdgeMaterial, angleThreshold, includeBoundaryEdges);
            featureEdgeRenderer.SyncNow();
        }
    }

    private void EnsureComponents()
    {
        silhouetteRenderer = GetComponent<SilhouetteOutlineRenderer>();
        if (silhouetteRenderer == null)
        {
            silhouetteRenderer = gameObject.AddComponent<SilhouetteOutlineRenderer>();
        }

        featureEdgeRenderer = GetComponent<FeatureEdgeRenderer>();
        if (featureEdgeRenderer == null && TryGetComponent<MeshFilter>(out _) && TryGetComponent<MeshRenderer>(out _))
        {
            featureEdgeRenderer = gameObject.AddComponent<FeatureEdgeRenderer>();
        }
    }

#if UNITY_EDITOR
    private void AutoAssignMaterials()
    {
        if (silhouetteMaterial != null && featureEdgeMaterial != null)
        {
            return;
        }

        MonoScript script = MonoScript.FromMonoBehaviour(this);
        if (script == null)
        {
            return;
        }

        string scriptPath = AssetDatabase.GetAssetPath(script);
        if (string.IsNullOrEmpty(scriptPath))
        {
            return;
        }

        string folderPath = Path.GetDirectoryName(scriptPath);
        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || material.shader == null)
            {
                continue;
            }

            if (silhouetteMaterial == null && material.shader.name == "Custom/URP/HybridSilhouetteOutline")
            {
                silhouetteMaterial = material;
            }
            else if (featureEdgeMaterial == null && material.shader.name == "Custom/URP/HybridFeatureEdges")
            {
                featureEdgeMaterial = material;
            }
        }
    }
#endif
}
