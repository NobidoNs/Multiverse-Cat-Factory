using System.Collections.Generic;
using UnityEngine;

public class GridFieldSpawner : MonoBehaviour
{
    [SerializeField] private GridField sourceGrid;
    [SerializeField] private Transform spawnedGridParent;
    [SerializeField] private float horizontalSpacing = 2f;
    [SerializeField] private string spawnedGridNamePrefix = "GridField";

    private readonly List<GridField> spawnedGrids = new List<GridField>();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        horizontalSpacing = Mathf.Max(0f, horizontalSpacing);
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
        GridField templateGrid = GetTemplateGrid();

        Transform parent = spawnedGridParent != null ? spawnedGridParent : templateGrid.transform.parent;
        GameObject clonedGridObject = Instantiate(templateGrid.gameObject, parent);
        clonedGridObject.name = $"{spawnedGridNamePrefix}_{spawnedGrids.Count + 1}";

        GridField clonedGrid = clonedGridObject.GetComponent<GridField>();
        if (clonedGrid == null)
        {
            Debug.LogWarning("GridFieldSpawner: cloned object does not contain GridField.");
            Destroy(clonedGridObject);
            return;
        }

        float clonedGridHalfWidth = clonedGrid.width * clonedGrid.cellSize * 0.5f;
        float rightEdge = GetRightmostEdge();
        Vector3 targetPosition = sourceGrid.transform.position;
        targetPosition.x = rightEdge + horizontalSpacing + clonedGridHalfWidth;

        clonedGridObject.transform.SetPositionAndRotation(targetPosition, templateGrid.transform.rotation);
        clonedGridObject.transform.localScale = templateGrid.transform.localScale;
        clonedGrid.RebuildGrid();
        templateGrid.CopyPlacedObjectsTo(clonedGrid);

        spawnedGrids.Add(clonedGrid);
    }

    private float GetRightmostEdge()
    {
        float rightmostEdge = GetRightEdge(sourceGrid);

        for (int i = 0; i < spawnedGrids.Count; i++)
        {
            GridField grid = spawnedGrids[i];
            if (grid == null)
            {
                continue;
            }

            rightmostEdge = Mathf.Max(rightmostEdge, GetRightEdge(grid));
        }

        return rightmostEdge;
    }

    private static float GetRightEdge(GridField grid)
    {
        return grid.transform.position.x + grid.width * grid.cellSize * 0.5f;
    }

    private GridField GetTemplateGrid()
    {
        if (spawnedGrids.Count > 0)
        {
            return spawnedGrids[spawnedGrids.Count - 1];
        }

        return sourceGrid;
    }

    private void ResolveReferences()
    {
        if (sourceGrid == null)
        {
            sourceGrid = GetComponent<GridField>();
        }
    }
}
