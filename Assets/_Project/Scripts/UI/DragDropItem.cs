using UnityEngine;
using UnityEngine.EventSystems;

public class DragDropItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const string GroundPlaneName = "GridGroundPlane";
    private const float RotationStep = 90f;

    public static bool IsAnyItemDragging { get; private set; }

    [Header("Placement")]
    public int typeId = 1;
    public GameObject buildingPrefab;
    public GridField grid;
    public Camera cam;
    public GameObject previewPrefab;

    [Header("UI")]
    public CanvasGroup canvasGroup;

    private GameObject currentPreview;
    private PlacementPreview placementPreview;
    private GridField activeGrid;
    private bool isDragging;
    private float currentRotationY;

    private void Update()
    {
        HandleRotationInput();
    }

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (cam == null)
        {
            cam = Camera.main;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (buildingPrefab == null || grid == null || cam == null)
        {
            Debug.LogWarning("DragDropItem: assign Building Prefab, Grid, and Camera before dragging.");
            return;
        }

        isDragging = true;
        IsAnyItemDragging = true;
        activeGrid = grid;
        currentRotationY = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (previewPrefab != null)
        {
            currentPreview = Instantiate(previewPrefab, Vector3.zero, GetCurrentRotation());
            placementPreview = currentPreview.GetComponent<PlacementPreview>();

            if (placementPreview != null)
            {
                placementPreview.Initialize(activeGrid);
                placementPreview.Show();
            }
        }
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

        if (placementPreview != null)
        {
            placementPreview.SetGrid(activeGrid);
            placementPreview.UpdatePosition(worldPos);
            return;
        }

        Vector2Int cell = activeGrid.WorldToCell(worldPos);
        currentPreview.transform.position = activeGrid.CellToWorld(cell) + Vector3.up * 0.1f;

        Renderer previewRenderer = currentPreview.GetComponentInChildren<Renderer>();
        if (previewRenderer != null)
        {
            previewRenderer.material.color = activeGrid.IsValidCell(cell) ? Color.green : Color.red;
        }
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
            if (targetGrid.Place(cell.x, cell.y, typeId))
            {
                Vector3 spawnPos = targetGrid.CellToWorld(cell);
                Quaternion rotation = GetCurrentRotation();
                GameObject instance = Instantiate(buildingPrefab, spawnPos, rotation, targetGrid.transform);
                targetGrid.RegisterPlacedObject(cell, buildingPrefab, instance, rotation);
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
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        currentPreview = null;
        placementPreview = null;
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
}
