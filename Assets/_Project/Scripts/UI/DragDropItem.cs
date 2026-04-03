using UnityEngine;
using UnityEngine.EventSystems;

public class DragDropItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
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
    private bool isDragging;

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

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (previewPrefab != null)
        {
            currentPreview = Instantiate(previewPrefab, Vector3.zero, Quaternion.identity);
            placementPreview = currentPreview.GetComponent<PlacementPreview>();

            if (placementPreview != null)
            {
                placementPreview.Initialize(grid);
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

        if (currentPreview == null || !TryGetWorldPos(eventData.position, out Vector3 worldPos))
        {
            return;
        }

        if (placementPreview != null)
        {
            placementPreview.UpdatePosition(worldPos);
            return;
        }

        Vector2Int cell = grid.WorldToCell(worldPos);
        currentPreview.transform.position = grid.CellToWorld(cell) + Vector3.up * 0.1f;

        Renderer previewRenderer = currentPreview.GetComponentInChildren<Renderer>();
        if (previewRenderer != null)
        {
            previewRenderer.material.color = grid.IsValidCell(cell) ? Color.green : Color.red;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;

        if (TryGetWorldPos(eventData.position, out Vector3 worldPos))
        {
            Vector2Int cell = grid.WorldToCell(worldPos);
            if (grid.Place(cell.x, cell.y, typeId))
            {
                Vector3 spawnPos = grid.CellToWorld(cell);
                Instantiate(buildingPrefab, spawnPos, Quaternion.identity);
                Debug.Log($"Placed building {typeId} at [{cell.x}, {cell.y}]");
            }
            else
            {
                Debug.Log("Cannot place building on this cell.");
            }
        }

        CleanupDrag();
    }

    private bool TryGetWorldPos(Vector2 screenPos, out Vector3 worldPos)
    {
        if (grid == null || cam == null)
        {
            worldPos = Vector3.zero;
            return false;
        }

        Plane groundPlane = new Plane(Vector3.up, grid.Origin);
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (groundPlane.Raycast(ray, out float distance))
        {
            worldPos = ray.GetPoint(distance);
            return true;
        }

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
    }
}
