using System;
using UnityEngine;

public class GridField : MonoBehaviour
{
    [Header("Параметры")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;

    public Vector3 Origin => transform.position;
    private bool[] occupied;
    private int[] cellType;

    private void Awake() => RebuildGrid();

    public void RebuildGrid()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        int total = width * height;
        occupied = new bool[total];
        cellType = new int[total];
        
        // Исправление для Unity 6 / .NET 6+: требуются start index и length
        Array.Clear(occupied, 0, occupied.Length);
        Array.Clear(cellType, 0, cellType.Length);
    }

    public bool IsValidCell(Vector2Int cell) => 
        cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height && 
        !occupied[cell.y * width + cell.x];

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        float halfW = (width * cellSize) * 0.5f;
        float halfH = (height * cellSize) * 0.5f;
        
        // Оффсеты (те же, что и в CellToWorld, но с обратным знаком для инверсии)
        // CellToWorld: + (-0.41) по X, + (0.25) по Z

        // ГЛАВНОЕ ИЗМЕНЕНИЕ: RoundToInt вместо FloorToInt
        // Это переключает логику с "пол от угла" на "ближайший центр"
        int x = Mathf.RoundToInt((worldPos.x - Origin.x + halfW - cellSize * 0.5f + cellSize) / cellSize);
        int z = Mathf.RoundToInt((worldPos.z - Origin.z + halfH - cellSize * 0.5f + cellSize) / cellSize);
        
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        float halfW = (width * cellSize) * 0.5f;
        float halfH = (height * cellSize) * 0.5f;

        return Origin + new Vector3(
            cell.x * cellSize - halfW + cellSize * 0.5f + cellSize, 
            0, 
            cell.y * cellSize - halfH + cellSize * 0.5f + cellSize
        );
    }

    public bool Place(int x, int y, int typeId)
    {
        Vector2Int cell = new Vector2Int(x, y);
        if (!IsValidCell(cell)) return false;

        int idx = cell.y * width + cell.x;
        occupied[idx] = true;
        cellType[idx] = typeId;
        return true;
    }

    public void ClearCell(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        int idx = y * width + x;
        occupied[idx] = false;
        cellType[idx] = 0;
    }
}