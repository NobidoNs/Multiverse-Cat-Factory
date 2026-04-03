using System;
using UnityEngine;

public class GridField : MonoBehaviour
{
    private const string GroundPlaneName = "GridGroundPlane";
    private static readonly Color DefaultGridColor = Color.white;
    private static readonly Color SelectedGridColor = new Color(0.55f, 0.9f, 0.65f, 1f);

    [Header("Grid")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;

    public Vector3 Origin => transform.position;

    private bool[] occupied;
    private int[] cellType;
    private GameObject[] placedPrefabs;
    private GameObject[] placedInstances;
    private Transform groundPlane;
    private Renderer groundPlaneRenderer;

    private void Awake()
    {
        RebuildGrid();
    }

    private void OnValidate()
    {
        SyncGroundPlane();
    }

    public void RebuildGrid()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.01f, cellSize);

        int total = width * height;
        occupied = new bool[total];
        cellType = new int[total];
        placedPrefabs = new GameObject[total];
        placedInstances = new GameObject[total];

        Array.Clear(occupied, 0, occupied.Length);
        Array.Clear(cellType, 0, cellType.Length);
        Array.Clear(placedPrefabs, 0, placedPrefabs.Length);
        Array.Clear(placedInstances, 0, placedInstances.Length);

        SyncGroundPlane();
    }

    public bool IsValidCell(Vector2Int cell)
    {
        return occupied != null &&
               cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < width &&
               cell.y < height &&
               !occupied[cell.y * width + cell.x];
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        float halfW = (width * cellSize) * 0.5f;
        float halfH = (height * cellSize) * 0.5f;

        int x = Mathf.RoundToInt((worldPos.x - Origin.x + halfW - cellSize * 0.5f) / cellSize);
        int z = Mathf.RoundToInt((worldPos.z - Origin.z + halfH - cellSize * 0.5f) / cellSize);

        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        float halfW = (width * cellSize) * 0.5f;
        float halfH = (height * cellSize) * 0.5f;

        return Origin + new Vector3(
            cell.x * cellSize - halfW + cellSize * 0.5f,
            0f,
            cell.y * cellSize - halfH + cellSize * 0.5f
        );
    }

    public bool Place(int x, int y, int typeId)
    {
        EnsureGrid();

        Vector2Int cell = new Vector2Int(x, y);
        if (!IsValidCell(cell))
        {
            return false;
        }

        int idx = cell.y * width + cell.x;
        occupied[idx] = true;
        cellType[idx] = typeId;
        return true;
    }

    public void ClearCell(int x, int y)
    {
        EnsureGrid();

        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        int idx = y * width + x;
        occupied[idx] = false;
        cellType[idx] = 0;
        placedPrefabs[idx] = null;
        placedInstances[idx] = null;
    }

    public void RegisterPlacedObject(Vector2Int cell, GameObject prefab, GameObject instance)
    {
        EnsureGrid();

        if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
        {
            return;
        }

        int idx = cell.y * width + cell.x;
        placedPrefabs[idx] = prefab;
        placedInstances[idx] = instance;

        if (instance != null)
        {
            instance.transform.SetParent(transform, true);
        }
    }

    public void CopyPlacedObjectsTo(GridField targetGrid)
    {
        EnsureGrid();

        if (targetGrid == null)
        {
            return;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (!occupied[idx] || placedPrefabs[idx] == null)
                {
                    continue;
                }

                if (!targetGrid.Place(x, y, cellType[idx]))
                {
                    continue;
                }

                Vector3 spawnPosition = targetGrid.CellToWorld(new Vector2Int(x, y));
                GameObject instance = Instantiate(placedPrefabs[idx], spawnPosition, Quaternion.identity, targetGrid.transform);
                targetGrid.RegisterPlacedObject(new Vector2Int(x, y), placedPrefabs[idx], instance);
            }
        }
    }

    public void SetSelectionVisual(bool isSelected)
    {
        Renderer renderer = GetGroundPlaneRenderer();
        if (renderer == null)
        {
            return;
        }

        renderer.material.color = isSelected ? SelectedGridColor : DefaultGridColor;
    }

    private void EnsureGrid()
    {
        if (occupied == null || cellType == null || placedPrefabs == null || placedInstances == null)
        {
            RebuildGrid();
        }
    }

    private void SyncGroundPlane()
    {
        Transform planeTransform = GetOrCreateGroundPlane();
        planeTransform.SetPositionAndRotation(Origin, Quaternion.identity);
        planeTransform.localScale = new Vector3(
            width * cellSize / 10f,
            1f,
            height * cellSize / 10f
        );
        GetGroundPlaneRenderer();
    }

    private Transform GetOrCreateGroundPlane()
    {
        if (groundPlane != null)
        {
            return groundPlane;
        }

        Transform existingPlane = transform.Find(GroundPlaneName);
        if (existingPlane != null)
        {
            groundPlane = existingPlane;
            EnsureGroundPlanePhysics(existingPlane.gameObject);
            CacheGroundPlaneRenderer(existingPlane.gameObject);
            return groundPlane;
        }

        GameObject planeObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        planeObject.name = GroundPlaneName;
        planeObject.transform.SetParent(transform, true);

        EnsureGroundPlanePhysics(planeObject);
        CacheGroundPlaneRenderer(planeObject);

        groundPlane = planeObject.transform;
        return groundPlane;
    }

    private Renderer GetGroundPlaneRenderer()
    {
        if (groundPlaneRenderer != null)
        {
            return groundPlaneRenderer;
        }

        if (groundPlane == null)
        {
            return null;
        }

        CacheGroundPlaneRenderer(groundPlane.gameObject);
        return groundPlaneRenderer;
    }

    private void CacheGroundPlaneRenderer(GameObject planeObject)
    {
        groundPlaneRenderer = planeObject.GetComponent<Renderer>();
    }

    private static void EnsureGroundPlanePhysics(GameObject planeObject)
    {
        Collider collider = planeObject.GetComponent<Collider>();
        if (collider == null)
        {
            planeObject.AddComponent<MeshCollider>();
        }
    }
}
