using System;
using UnityEngine;

public class GridField : MonoBehaviour
{
    [Header("Grid")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;

    public Vector3 Origin => transform.position;

    private bool[] occupied;
    private int[] cellType;

    private void Awake()
    {
        RebuildGrid();
    }

    public void RebuildGrid()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        int total = width * height;
        occupied = new bool[total];
        cellType = new int[total];

        Array.Clear(occupied, 0, occupied.Length);
        Array.Clear(cellType, 0, cellType.Length);
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
    }

    private void EnsureGrid()
    {
        if (occupied == null || cellType == null)
        {
            RebuildGrid();
        }
    }
}
