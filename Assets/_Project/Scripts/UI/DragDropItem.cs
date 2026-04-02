using UnityEngine;
using UnityEngine.EventSystems;

public class DragDropItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Настройки")]
    public int typeId = 1;
    public GameObject previewPrefab;
    public GridField grid;
    public Camera cam;

    private PlacementPreview preview;
    private bool isDragging;

    private void Awake()
    {
        if (previewPrefab != null)
        {
            preview = Instantiate(previewPrefab, Vector3.zero, Quaternion.identity).GetComponent<PlacementPreview>();
            preview.Initialize(grid);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (preview == null) return;
        isDragging = true;
        preview.Show();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || preview == null) return;
        
        if (TryGetWorldPos(eventData.position, out Vector3 worldPos))
            preview.UpdatePosition(worldPos);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || preview == null) return;
        isDragging = false;

        if (preview.IsPreviewValid())
        {
            Vector2Int cell = preview.GetTargetCell();
            if (grid.Place(cell.x, cell.y, typeId))
                Debug.Log($"✅ Объект {typeId} размещён в [{cell.x}, {cell.y}]");
        }

        preview.Hide();
    }

    private bool TryGetWorldPos(Vector2 screenPos, out Vector3 worldPos)
    {
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
}