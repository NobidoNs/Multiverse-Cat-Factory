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

        // Вычитаем сдвиг, чтобы компенсировать его при обратном преобразовании
        int x = Mathf.FloorToInt(worldPos.x - Origin.x + halfW);
        int z = Mathf.FloorToInt(worldPos.z - Origin.z + halfH);
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        float halfW = (width * cellSize) * 0.5f;
        float halfH = (height * cellSize) * 0.5f;

        // Прибавляем сдвиг, чтобы центры клеток ушли вправо и вниз
        return Origin + new Vector3(
            cell.x * cellSize - halfW + cellSize * 0.5f + (cellSize * -0.41f), 
            0, 
            cell.y * cellSize - halfH + cellSize * 0.5f + (cellSize * 0.25f)
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