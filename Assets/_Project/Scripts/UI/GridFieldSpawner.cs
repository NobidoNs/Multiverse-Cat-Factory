using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class GridFieldSpawner : MonoBehaviour
{
    private const float ExtraGridChance = 0.5f;
    private const string GroundPlaneName = "GridGroundPlane";
    private const float ArrowHeadLength = 0.75f;
    private const float ArrowHeadAngle = 25f;

    [SerializeField] private GridField sourceGrid;
    [SerializeField] private Transform spawnedGridParent;
    [SerializeField] private float horizontalSpacing = 2f;
    [SerializeField] private float verticalSpacing = 2f;
    [SerializeField] private string spawnedGridNamePrefix = "GridField";
    [SerializeField] private Color connectionArrowColor = Color.yellow;
    [SerializeField] private float connectionArrowWidth = 0.2f;
    [SerializeField] private float connectionArrowHeightOffset = 0.5f;
    [SerializeField] private Material connectionArrowMaterial;

    private readonly List<GridField> spawnedGrids = new List<GridField>();
    private readonly List<GridConnection> spawnedConnections = new List<GridConnection>();
    private Camera selectionCamera;
    private GridField activeGrid;
    private readonly HashSet<int> occupiedRows = new HashSet<int>();
    private Material runtimeArrowMaterial;

    private class GridConnection
    {
        public GridConnection(GridField parent, GridField child, GameObject visualRoot, LineRenderer mainLine, LineRenderer leftHead, LineRenderer rightHead)
        {
            Parent = parent;
            Child = child;
            VisualRoot = visualRoot;
            MainLine = mainLine;
            LeftHead = leftHead;
            RightHead = rightHead;
        }

        public GridField Parent { get; }
        public GridField Child { get; }
        public GameObject VisualRoot { get; }
        public LineRenderer MainLine { get; }
        public LineRenderer LeftHead { get; }
        public LineRenderer RightHead { get; }
    }

    private void Awake()
    {
        ResolveReferences();
        SetActiveGrid(sourceGrid);

        if (sourceGrid != null)
        {
            int row = GetRowIndex(sourceGrid.transform.position.z);
            occupiedRows.Add(row);
        }
    }

    // Преобразуем координату в индекс строки (округляем до целого)
    private int GetRowIndex(float zPosition)
    {
        return Mathf.RoundToInt(zPosition / (sourceGrid.cellSize * sourceGrid.height + verticalSpacing));
    }

    // Проверка: занята ли строка по оси X?
    private bool IsRowOccupied(float zPosition)
    {
        int row = GetRowIndex(zPosition);
        return occupiedRows.Contains(row);
    }

    private float GetRowStep()
    {
        return sourceGrid.cellSize * sourceGrid.height + verticalSpacing;
    }

    private float GetNextFreeRowZ(float startZ, Vector3 direction)
    {
        float rowStep = GetRowStep();
        float candidateZ = startZ;
        float zDirection = direction == Vector3.forward ? 1f : -1f;

        while (IsRowOccupied(candidateZ))
        {
            candidateZ += rowStep * zDirection;
        }

        return candidateZ;
    }

    private Vector3 GetBranchDirection(GridField grid)
    {
        int sourceRow = GetRowIndex(sourceGrid.transform.position.z);
        int currentRow = GetRowIndex(grid.transform.position.z);

        if (currentRow > sourceRow)
        {
            return Vector3.forward;
        }

        if (currentRow < sourceRow)
        {
            return Vector3.back;
        }

        return Random.value < 0.5f ? Vector3.forward : Vector3.back;
    }

    // Добавить строку в список занятых
    private void MarkRowAsOccupied(float zPosition)
    {
        int row = GetRowIndex(zPosition);
        occupiedRows.Add(row);
        Debug.Log($"Row {row} marked as occupied. Total occupied rows: {occupiedRows.Count}");
    }

    private void Update()
    {
        HandleGridSelection();
        RefreshConnectionVisuals();
    }

    private void OnValidate()
    {
        horizontalSpacing = Mathf.Max(0f, horizontalSpacing);
        verticalSpacing = Mathf.Max(0f, verticalSpacing);
        ResolveReferences();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < spawnedConnections.Count; i++)
        {
            GridConnection connection = spawnedConnections[i];
            if (connection != null && connection.VisualRoot != null)
            {
                Destroy(connection.VisualRoot);
            }
        }

        if (runtimeArrowMaterial != null)
        {
            Destroy(runtimeArrowMaterial);
        }
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
            Vector3 branchDirection = GetBranchDirection(growthGrid);
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
        spawnedConnections.Add(CreateConnection(templateGrid, clonedGrid));
        MarkRowAsOccupied(targetPosition.z);
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
            float startZ = templateBottomEdge - verticalSpacing - cloneHalfHeight;
            targetPosition.z = GetNextFreeRowZ(startZ, direction);
            return targetPosition;
        }

        float templateTopEdge = templateGrid.transform.position.z + templateGrid.height * templateGrid.cellSize * 0.5f;
        float forwardStartZ = templateTopEdge + verticalSpacing + cloneHalfHeight;
        targetPosition.z = GetNextFreeRowZ(forwardStartZ, direction);
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

    private GridConnection CreateConnection(GridField parent, GridField child)
    {
        GameObject visualRoot = new GameObject($"Arrow_{parent.name}_to_{child.name}");
        visualRoot.transform.SetParent(transform, false);

        LineRenderer mainLine = CreateArrowLineRenderer("Main", visualRoot.transform);
        LineRenderer leftHead = CreateArrowLineRenderer("LeftHead", visualRoot.transform);
        LineRenderer rightHead = CreateArrowLineRenderer("RightHead", visualRoot.transform);

        GridConnection connection = new GridConnection(parent, child, visualRoot, mainLine, leftHead, rightHead);
        UpdateConnectionVisual(connection);
        return connection;
    }

    private LineRenderer CreateArrowLineRenderer(string lineName, Transform parent)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(parent, false);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.positionCount = 2;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.material = GetArrowMaterial();
        lineRenderer.startColor = connectionArrowColor;
        lineRenderer.endColor = connectionArrowColor;
        lineRenderer.startWidth = connectionArrowWidth;
        lineRenderer.endWidth = connectionArrowWidth;
        return lineRenderer;
    }

    private Material GetArrowMaterial()
    {
        if (connectionArrowMaterial != null)
        {
            return connectionArrowMaterial;
        }

        if (runtimeArrowMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader != null)
            {
                runtimeArrowMaterial = new Material(shader);
            }
        }

        return runtimeArrowMaterial;
    }

    private void RefreshConnectionVisuals()
    {
        for (int i = spawnedConnections.Count - 1; i >= 0; i--)
        {
            GridConnection connection = spawnedConnections[i];
            if (connection.Parent == null || connection.Child == null)
            {
                if (connection.VisualRoot != null)
                {
                    Destroy(connection.VisualRoot);
                }

                spawnedConnections.RemoveAt(i);
                continue;
            }

            UpdateConnectionVisual(connection);
        }
    }

    private void UpdateConnectionVisual(GridConnection connection)
    {
        Vector3 heightOffset = Vector3.up * connectionArrowHeightOffset;
        Vector3 start = connection.Parent.transform.position + heightOffset;
        Vector3 end = connection.Child.transform.position + heightOffset;
        Vector3 direction = end - start;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 arrowDirection = direction.normalized;
        Quaternion leftRotation = Quaternion.LookRotation(arrowDirection) * Quaternion.Euler(0f, 180f + ArrowHeadAngle, 0f);
        Quaternion rightRotation = Quaternion.LookRotation(arrowDirection) * Quaternion.Euler(0f, 180f - ArrowHeadAngle, 0f);
        Vector3 leftHeadEnd = end + leftRotation * Vector3.forward * ArrowHeadLength;
        Vector3 rightHeadEnd = end + rightRotation * Vector3.forward * ArrowHeadLength;

        ApplyLineStyle(connection.MainLine);
        ApplyLineStyle(connection.LeftHead);
        ApplyLineStyle(connection.RightHead);

        connection.MainLine.SetPosition(0, start);
        connection.MainLine.SetPosition(1, end);
        connection.LeftHead.SetPosition(0, end);
        connection.LeftHead.SetPosition(1, leftHeadEnd);
        connection.RightHead.SetPosition(0, end);
        connection.RightHead.SetPosition(1, rightHeadEnd);
    }

    private void ApplyLineStyle(LineRenderer lineRenderer)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.material = GetArrowMaterial();
        lineRenderer.startColor = connectionArrowColor;
        lineRenderer.endColor = connectionArrowColor;
        lineRenderer.startWidth = connectionArrowWidth;
        lineRenderer.endWidth = connectionArrowWidth;
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
