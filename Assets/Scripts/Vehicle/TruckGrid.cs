using System.Collections.Generic;
using UnityEngine;

public class TruckGrid : MonoBehaviour
{
    [Header("Grid Size")]
    [SerializeField] private Vector2 gridAreaSize = new Vector2(5.5f, 3.5f);
    [Min(1)]
    [SerializeField] private int width = 5;
    [Min(1)]
    [SerializeField] private int height = 3;
    [SerializeField] private Vector2 cellGap = new Vector2(0.08f, 0.08f);

    [Header("Generation")]
    [SerializeField] private GridCell cellPrefab;
    [SerializeField] private Transform cellParent;
    [SerializeField] private string generatedParentName = "GeneratedGridCells";
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool centerGrid = true;

    [SerializeField, HideInInspector] private Vector2 cellSize = Vector2.one;
    private readonly List<GridCell> cells = new List<GridCell>();

    public int Width => width;
    public int Height => height;
    public Vector2 CellSize => cellSize;
    public Vector2 CellStep => cellSize + cellGap;
    public IReadOnlyList<GridCell> Cells => cells;
    public GridCell CellPrefab => cellPrefab;

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        gridAreaSize = new Vector2(Mathf.Max(0.1f, gridAreaSize.x), Mathf.Max(0.1f, gridAreaSize.y));
        cellGap = new Vector2(Mathf.Max(0f, cellGap.x), Mathf.Max(0f, cellGap.y));
        RecalculateCellSize();
    }

    private void RecalculateCellSize()
    {
        int w = Mathf.Max(1, width);
        int h = Mathf.Max(1, height);

        float calculatedCellWidth = (gridAreaSize.x - (w - 1) * cellGap.x) / w;
        float calculatedCellHeight = (gridAreaSize.y - (h - 1) * cellGap.y) / h;

        cellSize = new Vector2(
            Mathf.Max(0.05f, calculatedCellWidth),
            Mathf.Max(0.05f, calculatedCellHeight)
        );
    }

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateGrid();
        }
    }

    [ContextMenu("Generate Grid")]
    public void GenerateGrid()
    {
        RecalculateCellSize();
        ClearGrid();

        Transform parent = GetOrCreateCellParent();
        Vector2 step = cellSize + cellGap;
        Vector2 originOffset = centerGrid
            ? new Vector2((width - 1) * step.x * -0.5f, (height - 1) * step.y * -0.5f)
            : Vector2.zero;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GridCell cell = CreateCell(parent);
                cell.transform.localPosition = new Vector3(
                    originOffset.x + x * step.x,
                    originOffset.y + y * step.y,
                    0f);

                cell.Initialize(this, x, y, cellSize);
                cells.Add(cell);
            }
        }
    }

    public GridCell GetCell(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return null;
        }

        return cells[y * width + x];
    }

    public void ClearPlacedBlocks(bool destroyBlocks)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            GridCell cell = cells[i];
            if (cell != null)
            {
                cell.ClearAllPlacedBlocks(destroyBlocks);
            }
        }
    }

    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        Transform parent = GetOrCreateCellParent();
        cells.Clear();

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private GridCell CreateCell(Transform parent)
    {
        GridCell cell;

        if (cellPrefab != null)
        {
            cell = Instantiate(cellPrefab, parent);
        }
        else
        {
            GameObject cellObject = new GameObject("GridCell");
            cellObject.transform.SetParent(parent, false);
            cell = cellObject.AddComponent<GridCell>();
        }

        return cell;
    }

    private Transform GetOrCreateCellParent()
    {
        if (cellParent != null)
        {
            return cellParent;
        }

        Transform existingParent = transform.Find(generatedParentName);
        if (existingParent != null)
        {
            return existingParent;
        }

        GameObject parentObject = new GameObject(generatedParentName);
        parentObject.transform.SetParent(transform, false);
        return parentObject.transform;
    }

    private void OnDrawGizmosSelected()
    {
        RecalculateCellSize();

        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector2 step = cellSize + cellGap;
        Vector2 totalSize = new Vector2(
            width * cellSize.x + Mathf.Max(0, width - 1) * cellGap.x,
            height * cellSize.y + Mathf.Max(0, height - 1) * cellGap.y);

        // 1. 실제 셀들이 정렬 배치되는 영역 기즈모 (Cyan)
        Vector3 center = centerGrid
            ? Vector3.zero
            : new Vector3((totalSize.x - cellSize.x) * 0.5f, (totalSize.y - cellSize.y) * 0.5f, 0f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, totalSize);

        // 2. 사용자가 설정한 가이드라인 전체 한계 영역 기즈모 (반투명 연두색)
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.35f);
        Vector3 areaCenter = centerGrid
            ? Vector3.zero
            : new Vector3((gridAreaSize.x - cellSize.x) * 0.5f, (gridAreaSize.y - cellSize.y) * 0.5f, 0f);
        Gizmos.DrawWireCube(areaCenter, gridAreaSize);

        Gizmos.matrix = originalMatrix;
    }
}
