using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class GridFieldSpawner : MonoBehaviour
{
    private const float ExtraGridChance = 0.5f;
    private const string GroundPlaneName = "GridGroundPlane";

    [SerializeField] private GridField sourceGrid;
    [SerializeField] private Transform spawnedGridParent;
    [SerializeField] private float horizontalSpacing = 2f;
    [SerializeField] private float verticalSpacing = 2f;
    [SerializeField] private string spawnedGridNamePrefix = "GridField";

    private readonly List<GridField> spawnedGrids = new List<GridField>();
    private Camera selectionCamera;
    private GridField activeGrid;

    private void Awake()
    {
        ResolveReferences();
        SetActiveGrid(sourceGrid);
    }

    private void Update()
    {
        HandleGridSelection();
    }

    private void OnValidate()
    {
        horizontalSpacing = Mathf.Max(0f, horizontalSpacing);
        verticalSpacing = Mathf.Max(0f, verticalSpacing);
        ResolveReferences();
    }

    public void SpawnGridToRight()
    {
        ResolveReferences();

        if (sourceGrid == null)
        {
            Debug.LogWarning("GridFieldSpawner: source grid is not assigned.");
            return;
        }

        spawnedGrids.RemoveAll(grid => grid == null);
        GridField growthGrid = GetGrowthGrid();
        GridField rightGrid = SpawnGrid(growthGrid, Vector3.right);
        if (rightGrid != null && Random.value < ExtraGridChance)
        {
            Vector3 branchDirection = Random.value < 0.5f ? Vector3.forward : Vector3.back;
            SpawnGrid(growthGrid, branchDirection);
        }

        if (rightGrid != null)
        {
            SetActiveGrid(rightGrid);
        }
    }

    private GridField SpawnGrid(GridField templateGrid, Vector3 direction)
    {
        if (templateGrid == null)
        {
            return null;
        } 

        Transform parent = spawnedGridParent != null ? spawnedGridParent : templateGrid.transform.parent;
        GameObject clonedGridObject = Instantiate(templateGrid.gameObject, parent);
        clonedGridObject.name = $"{spawnedGridNamePrefix}_{spawnedGrids.Count + 1}";

        GridField clonedGrid = clonedGridObject.GetComponent<GridField>();
        if (clonedGrid == null)
        {
            Debug.LogWarning("GridFieldSpawner: cloned object does not contain GridField.");
            Destroy(clonedGridObject);
            return null;
        }

        GridFieldSpawner clonedSpawner = clonedGridObject.GetComponent<GridFieldSpawner>();
        if (clonedSpawner != null)
        {
            clonedSpawner.enabled = false;
            Destroy(clonedSpawner);
        }

        Vector3 targetPosition = GetSpawnPosition(templateGrid, clonedGrid, direction);
        clonedGridObject.transform.SetPositionAndRotation(targetPosition, templateGrid.transform.rotation);
        clonedGridObject.transform.localScale = templateGrid.transform.localScale;
        clonedGrid.RebuildGrid();
        templateGrid.CopyPlacedObjectsTo(clonedGrid);
        clonedGrid.SetSelectionVisual(false);

        spawnedGrids.Add(clonedGrid);
        return clonedGrid;
    }

    private Vector3 GetSpawnPosition(GridField templateGrid, GridField clonedGrid, Vector3 direction)
    {
        Vector3 targetPosition = templateGrid.transform.position;
        float cloneHalfWidth = clonedGrid.width * clonedGrid.cellSize * 0.5f;
        float cloneHalfHeight = clonedGrid.height * clonedGrid.cellSize * 0.5f;

        if (direction == Vector3.right)
        {
            float templateRightEdge = templateGrid.transform.position.x + templateGrid.width * templateGrid.cellSize * 0.5f;
            targetPosition.x = templateRightEdge + horizontalSpacing + cloneHalfWidth;
            return targetPosition;
        }

        if (direction == Vector3.left)
        {
            float templateLeftEdge = templateGrid.transform.position.x - templateGrid.width * templateGrid.cellSize * 0.5f;
            targetPosition.x = templateLeftEdge - horizontalSpacing - cloneHalfWidth;
            return targetPosition;
        }

        if (direction == Vector3.back)
        {
            float templateBottomEdge = templateGrid.transform.position.z - templateGrid.height * templateGrid.cellSize * 0.5f;
            targetPosition.z = templateBottomEdge - verticalSpacing - cloneHalfHeight;
            return targetPosition;
        }

        float templateTopEdge = templateGrid.transform.position.z + templateGrid.height * templateGrid.cellSize * 0.5f;
        targetPosition.z = templateTopEdge + verticalSpacing + cloneHalfHeight;
        return targetPosition;
    }

    private GridField GetGrowthGrid()
    {
        if (activeGrid != null)
        {
            return activeGrid;
        }

        return sourceGrid;
    }

    private void HandleGridSelection()
    {
        if (!Input.GetMouseButtonDown(0) || Input.GetKey(KeyCode.LeftShift))
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera cam = GetSelectionCamera();
        if (cam == null)
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            return;
        }

        if (hit.collider.transform.name != GroundPlaneName)
        {
            return;
        }

        GridField clickedGrid = hit.collider.GetComponentInParent<GridField>();
        if (clickedGrid != null)
        {
            SetActiveGrid(clickedGrid);
        }
    }

    private Camera GetSelectionCamera()
    {
        if (selectionCamera == null)
        {
            selectionCamera = Camera.main;
        }

        return selectionCamera;
    }

    private void SetActiveGrid(GridField newActiveGrid)
    {
        if (activeGrid == newActiveGrid || newActiveGrid == null)
        {
            return;
        }

        if (activeGrid != null)
        {
            activeGrid.SetSelectionVisual(false);
        }

        activeGrid = newActiveGrid;
        activeGrid.SetSelectionVisual(true);
    }

    private void ResolveReferences()
    {
        if (sourceGrid == null)
        {
            sourceGrid = GetComponent<GridField>();
        }

        if (activeGrid == null && sourceGrid != null)
        {
            activeGrid = sourceGrid;
        }
    }
}
