using UnityEngine;
using UnityEngine.EventSystems;

public class Building : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const string GroundPlaneName = "GridGroundPlane";
    private const float RotationStep = 90f;

    public static bool IsAnyItemDragging { get; private set; }

    [Header("Placement")]
    public int typeId = 1;
    public BuildingCatalog buildingCatalog;
    public GridField grid;
    public Camera cam;
    [Header("Preview")]
    public Color validPreviewColor = new Color(0.3f, 1f, 0.3f, 0.65f);
    public Color invalidPreviewColor = new Color(1f, 0.3f, 0.3f, 0.65f);

    private GameObject currentPreview;
    private GridField activeGrid;
    private bool isDragging;
    private float currentRotationY;
    private Renderer[] previewRenderers = System.Array.Empty<Renderer>();

    protected virtual void Update()
    {
        HandleRotationInput();
    }

    protected virtual void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ResolveReferences();

        if (!TryResolveBuildingEntry(out BuildingCatalog.BuildingEntry entry) || entry.buildingPrefab == null || grid == null || cam == null)
        {
            Debug.LogWarning("Building: assign Building Catalog entry, Grid, and Camera before dragging.");
            return;
        }

        isDragging = true;
        IsAnyItemDragging = true;
        activeGrid = grid;
        currentRotationY = 0f;

        currentPreview = CreatePreviewInstance(entry.buildingPrefab);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        if (currentPreview == null || !TryGetPlacementTarget(eventData.position, out GridField targetGrid, out Vector3 worldPos))
        {
            return;
        }

        activeGrid = targetGrid;

        Vector2Int cell = activeGrid.WorldToCell(worldPos);
        currentPreview.transform.position = activeGrid.CellToWorld(cell) + Vector3.up * 0.1f;
        UpdatePreviewVisual(cell);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;

        if (TryGetPlacementTarget(eventData.position, out GridField targetGrid, out Vector3 worldPos))
        {
            Vector2Int cell = targetGrid.WorldToCell(worldPos);
            if (!TryResolveBuildingEntry(out BuildingCatalog.BuildingEntry entry) || entry.buildingPrefab == null)
            {
                Debug.LogWarning($"Building: no building prefab found for typeId {typeId}.");
            }
            else if (targetGrid.Place(cell.x, cell.y, typeId))
            {
                Vector3 spawnPos = targetGrid.CellToWorld(cell);
                Quaternion rotation = GetCurrentRotation();
                GameObject instance = Instantiate(entry.buildingPrefab, spawnPos, rotation, targetGrid.transform);
                targetGrid.RegisterPlacedObject(cell, entry.buildingPrefab, instance, rotation);
                Debug.Log($"Placed building {typeId} at [{cell.x}, {cell.y}]");
            }
            else
            {
                Debug.Log("Cannot place building on this cell.");
            }
        }

        CleanupDrag();
    }

    private bool TryGetPlacementTarget(Vector2 screenPos, out GridField targetGrid, out Vector3 worldPos)
    {
        ResolveReferences();

        if (grid == null || cam == null)
        {
            targetGrid = null;
            worldPos = Vector3.zero;
            return false;
        }

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            GridField hitGrid = hit.collider.GetComponentInParent<GridField>();
            if (hitGrid != null && hit.collider.transform.name == GroundPlaneName)
            {
                targetGrid = hitGrid;
                worldPos = hit.point;
                return true;
            }
        }

        Plane fallbackPlane = new Plane(Vector3.up, grid.Origin);
        if (fallbackPlane.Raycast(ray, out float distance))
        {
            targetGrid = grid;
            worldPos = ray.GetPoint(distance);
            return true;
        }

        targetGrid = null;
        worldPos = Vector3.zero;
        return false;
    }

    private void CleanupDrag()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        currentPreview = null;
        previewRenderers = System.Array.Empty<Renderer>();
        activeGrid = null;
        IsAnyItemDragging = false;
    }

    private void HandleRotationInput()
    {
        if (!isDragging)
        {
            return;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        currentRotationY += Mathf.Sign(scroll) * RotationStep;

        if (currentPreview != null)
        {
            currentPreview.transform.rotation = GetCurrentRotation();
        }
    }

    private Quaternion GetCurrentRotation()
    {
        return Quaternion.Euler(0f, currentRotationY, 0f);
    }

    private void ResolveReferences()
    {
        if (buildingCatalog == null)
        {
            buildingCatalog = FindFirstObjectByType<BuildingCatalog>();
        }

        if (grid == null)
        {
            grid = FindFirstObjectByType<GridField>();
        }

        if (cam == null)
        {
            cam = Camera.main;
        }
    }

    private bool TryResolveBuildingEntry(out BuildingCatalog.BuildingEntry entry)
    {
        entry = null;

        if (buildingCatalog == null)
        {
            return false;
        }

        return buildingCatalog.TryGetEntry(typeId, out entry);
    }
    private GameObject CreatePreviewInstance(GameObject sourcePrefab)
    {
        GameObject preview = Instantiate(sourcePrefab, Vector3.zero, GetCurrentRotation());
        preview.name = $"{sourcePrefab.name}_Preview";
        previewRenderers = preview.GetComponentsInChildren<Renderer>(true);

        SetPreviewPhysics(preview);
        ApplyPreviewColor(validPreviewColor);
        return preview;
    }

    private void SetPreviewPhysics(GameObject preview)
    {
        int previewLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (previewLayer >= 0)
        {
            preview.layer = previewLayer;
        }

        foreach (Transform child in preview.GetComponentsInChildren<Transform>(true))
        {
            if (previewLayer >= 0)
            {
                child.gameObject.layer = previewLayer;
            }
        }

        foreach (Collider previewCollider in preview.GetComponentsInChildren<Collider>(true))
        {
            previewCollider.enabled = false;
        }

        foreach (Rigidbody body in preview.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private void UpdatePreviewVisual(Vector2Int cell)
    {
        if (activeGrid == null)
        {
            return;
        }

        ApplyPreviewColor(activeGrid.IsValidCell(cell) ? validPreviewColor : invalidPreviewColor);
    }

    private void ApplyPreviewColor(Color color)
    {
        for (int i = 0; i < previewRenderers.Length; i++)
        {
            Renderer renderer = previewRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }
            }
        }
    }
}
