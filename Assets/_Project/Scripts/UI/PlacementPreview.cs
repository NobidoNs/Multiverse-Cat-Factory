using UnityEngine;

public class PlacementPreview : MonoBehaviour
{
    [SerializeField] private Renderer previewRenderer;
    [SerializeField] private Material validMat;
    [SerializeField] private Material invalidMat;

    private GridField grid;
    private Vector2Int currentCell = new Vector2Int(-999, -999);
    private bool isActive;

    private void Awake()
    {
        if (previewRenderer == null)
        {
            previewRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public void Initialize(GridField gridRef)
    {
        grid = gridRef;
        gameObject.SetActive(false);
    }

    public void SetGrid(GridField gridRef)
    {
        grid = gridRef;
    }

    public void UpdatePosition(Vector3 worldPos)
    {
        if (!isActive || grid == null)
        {
            return;
        }

        currentCell = grid.WorldToCell(worldPos);
        transform.position = grid.CellToWorld(currentCell);

        if (previewRenderer != null)
        {
            bool isValid = grid.IsValidCell(currentCell);
            previewRenderer.sharedMaterial = isValid ? validMat : invalidMat;
        }
    }

    public void Show()
    {
        isActive = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        isActive = false;
        gameObject.SetActive(false);
    }

    public Vector2Int GetTargetCell()
    {
        return currentCell;
    }

    public bool IsPreviewValid()
    {
        return grid != null && grid.IsValidCell(currentCell);
    }
}
