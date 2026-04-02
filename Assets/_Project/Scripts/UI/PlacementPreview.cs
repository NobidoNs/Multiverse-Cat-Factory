using UnityEngine;

public class PlacementPreview : MonoBehaviour
{
    [SerializeField] private Renderer previewRenderer;
    [SerializeField] private Material validMat;
    [SerializeField] private Material invalidMat;

    private GridField grid;
    private Vector2Int currentCell = new Vector2Int(-999, -999);
    private bool isActive;

    public void Initialize(GridField gridRef)
    {
        grid = gridRef;
        gameObject.SetActive(false);
    }

    public void UpdatePosition(Vector3 worldPos)
    {
        if (!isActive) return;
        
        currentCell = grid.WorldToCell(worldPos);
        transform.position = grid.CellToWorld(currentCell);
        
        bool isValid = grid.IsValidCell(currentCell);
        previewRenderer.sharedMaterial = isValid ? validMat : invalidMat;
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

    public Vector2Int GetTargetCell() => currentCell;
    public bool IsPreviewValid() => grid.IsValidCell(currentCell);
}