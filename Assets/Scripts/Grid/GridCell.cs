using System.Collections.Generic;
using TMPro;
using UnityEngine;

public struct PlacedTurret
{
    public TurretBlock block;
    public TurretInstance instance;
}

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class GridCell : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int gridX;
    [SerializeField] private int gridY;
    
    private List<PlacedTurret> placedTurrets = new List<PlacedTurret>();
    private TurretStats effectiveStats;
    private TurretDataSO effectiveData;

    [Header("Visual")]
    [SerializeField] private Color normalColor   = new Color(0.25f, 0.55f, 0.75f, 0.35f);
    [SerializeField] private Color hoverColor    = new Color(0.35f, 0.9f,  1f,    0.75f);
    [SerializeField] private Color occupiedColor = new Color(1f,    0.65f, 0.2f,  0.75f);
    [SerializeField] private Color stackedColor  = new Color(1f,    0.35f, 0.1f,  0.85f);
    [SerializeField] private bool showLayerText = true;

    [Header("Layer Text")]
    [SerializeField] private TextMeshProUGUI layerTextMesh;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;
    private bool isHovered;
    private Color originalLayerTextColor = Color.white;

    public int GridX => gridX;
    public int GridY => gridY;
    public TruckGrid Grid { get; private set; }
    
    public bool IsOccupied => placedTurrets.Count > 0;
    public TurretDataSO OccupiedTurretData => IsOccupied ? placedTurrets[0].instance.data : null;
    public int CurrentTotalLevel { get; private set; }
    public TurretStats EffectiveStats => effectiveStats;
    public Vector3 SnapPosition => transform.position;
    
    public IReadOnlyList<PlacedTurret> PlacedTurrets => placedTurrets;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider    = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;

        if (layerTextMesh != null)
        {
            originalLayerTextColor = layerTextMesh.color;
        }

        // Ensure the World Space Canvas for the Layer Text renders on top of 2D SpriteRenderers
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.sortingOrder = 10;
        }

        RefreshVisual();
    }

    private void Update()
    {
        bool pointerOverCell = false;

        if (TurretBlock.DraggedBlock != null)
        {
            pointerOverCell = IsCoveredByDraggingBlock();
        }
        else
        {
            pointerOverCell = IsPointerOverCell();
        }

        if (isHovered == pointerOverCell) return;

        isHovered = pointerOverCell;
        RefreshVisual();
    }

    public void Initialize(TruckGrid grid, int x, int y, Vector2 size)
    {
        Grid   = grid;
        gridX  = x;
        gridY  = y;
        name   = $"GridCell_{x}_{y}";

        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider    = GetComponent<BoxCollider2D>();
        if (boxCollider != null) boxCollider.isTrigger = true;

        if (spriteRenderer != null && spriteRenderer.drawMode == SpriteDrawMode.Sliced)
        {
            spriteRenderer.size = size;
            transform.localScale = Vector3.one;
            if (boxCollider != null)
            {
                boxCollider.size = size;
                boxCollider.offset = Vector2.zero;
            }
        }
        else
        {
            transform.localScale = new Vector3(size.x, size.y, 1f);
            if (boxCollider != null)
            {
                boxCollider.size = Vector2.one;
                boxCollider.offset = Vector2.zero;
            }
        }

        RefreshVisual();
    }

    public bool IsTopInstance(TurretInstance instance)
    {
        if (placedTurrets.Count == 0) return false;
        return placedTurrets[placedTurrets.Count - 1].instance == instance;
    }

    public bool CanPlace(TurretInstance instance)
    {
        if (instance == null || instance.data == null) return false;
        
        if (IsOccupied)
        {
            if (OccupiedTurretData != instance.data) return false;
            
        }
        
        return true;
    }

    public bool TryPlace(TurretBlock block, TurretInstance instance)
    {
        if (!CanPlace(instance)) return false;
        
        placedTurrets.Add(new PlacedTurret { block = block, instance = instance });
        RecalculateStats();
        RefreshVisual();
        return true;
    }

    public void ClearPlacedBlock(TurretBlock block)
    {
        bool removedAny = false;
        for (int i = placedTurrets.Count - 1; i >= 0; i--)
        {
            if (placedTurrets[i].block == block)
            {
                placedTurrets.RemoveAt(i);
                removedAny = true;
            }
        }
        
        if (removedAny)
        {
            RecalculateStats();
            RefreshVisual();
        }
    }

    public void ClearAllPlacedBlocks(bool destroyBlocks)
    {
        if (destroyBlocks)
        {
            HashSet<TurretBlock> blocksToDestroy = new HashSet<TurretBlock>();
            foreach(var pt in placedTurrets)
            {
                if (pt.block != null) blocksToDestroy.Add(pt.block);
            }
            foreach(var b in blocksToDestroy)
            {
                Destroy(b.gameObject);
            }
        }

        placedTurrets.Clear();
        RecalculateStats();
        RefreshVisual();
    }

    private void RecalculateStats()
    {
        if (!IsOccupied)
        {
            effectiveStats = TurretStats.Zero;
            effectiveData = null;
            CurrentTotalLevel = 0;
            return;
        }

        effectiveData = placedTurrets[0].instance.data;
        int totalLevel = 0;
        for (int i = 0; i < placedTurrets.Count; i++)
        {
            totalLevel += placedTurrets[i].instance.level;
        }
        totalLevel = Mathf.Min(10, totalLevel);
        CurrentTotalLevel = totalLevel;

        effectiveStats = effectiveData.GetStatsForLevel(totalLevel);

        // Dynamic visual upgrade based on total level
        for (int i = 0; i < placedTurrets.Count; i++)
        {
            var pt = placedTurrets[i];
            if (pt.instance == null || pt.instance.visualObject == null) continue;

            SpriteRenderer sr = pt.instance.visualObject.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) continue;

            if (i == placedTurrets.Count - 1)
            {
                // Top instance displays the sprite matching the total combined level
                sr.enabled = true;
                if (pt.instance.data != null)
                {
                    sr.sprite = pt.instance.data.GetSpriteForLevel(totalLevel);
                }
            }
            else
            {
                // Hide underneath instances inside the stack to prevent z-fighting and rendering artifacts
                sr.enabled = false;
            }
        }
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null) return;

        if (IsOccupied)
        {
            spriteRenderer.color = placedTurrets.Count > 1 ? stackedColor : occupiedColor;
        }
        else
        {
            spriteRenderer.color = isHovered ? hoverColor : normalColor;
        }

        RefreshLayerText();
    }

    private void RefreshLayerText()
    {
        if (layerTextMesh == null) return;

        bool shouldShow = showLayerText && IsOccupied;

        // 드래그 중인 블록이 이 셀을 덮고 있으면 텍스트 숨김
        // (배치 가능/불가 여부 관계없이: 어느 경우든 미리보기나 충돌 표시가 우선)
        if (shouldShow && isHovered && TurretBlock.DraggedBlock != null)
        {
            GridCell anchor = TurretBlock.DraggedBlock.GetHoveredAnchorCell();
            if (anchor != null)
            {
                shouldShow = false;
            }
        }

        layerTextMesh.gameObject.SetActive(shouldShow);
        if (shouldShow)
        {
            int totalLevel = 0;
            for (int i = 0; i < placedTurrets.Count; i++) totalLevel += placedTurrets[i].instance.level;
            totalLevel = Mathf.Min(10, totalLevel);
            layerTextMesh.text = $"{totalLevel}";

            if (totalLevel >= 10)
            {
                layerTextMesh.color = Color.red;
            }
            else
            {
                layerTextMesh.color = originalLayerTextColor;
            }
        }
        else
        {
            layerTextMesh.text = string.Empty;
        }
    }

    private bool IsCoveredByDraggingBlock()
    {
        var dragged = TurretBlock.DraggedBlock;
        if (dragged == null) return false;

        GridCell anchor = dragged.GetHoveredAnchorCell();
        if (anchor == null) return false;

        // Check if the dragged block is hovering over the same grid
        if (anchor.Grid != Grid) return false;

        // Check if any active cell of the dragged block overlaps this cell
        foreach (var inst in dragged.Instances)
        {
            int targetX = anchor.GridX + inst.localPosition.x;
            int targetY = anchor.GridY + inst.localPosition.y;
            if (targetX == gridX && targetY == gridY)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointerOverCell()
    {
        if (UnityEngine.InputSystem.Mouse.current == null || Camera.main == null || boxCollider == null)
        {
            return false;
        }

        Vector2 screenPosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Vector3 worldPosition  = Camera.main.ScreenToWorldPoint(new Vector3(
            screenPosition.x,
            screenPosition.y,
            Mathf.Abs(Camera.main.transform.position.z - transform.position.z)));

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == boxCollider) return true;
        }

        return false;
    }
}
