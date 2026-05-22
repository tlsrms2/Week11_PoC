using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[System.Serializable]
public class TurretInstance
{
    public Vector2Int localPosition;
    public TurretDataSO data;
    public int level;
    [HideInInspector] public GameObject visualObject;
    [HideInInspector] public TurretWeapon weapon;
    [HideInInspector] public SpriteRenderer backgroundCellRenderer;
}

[RequireComponent(typeof(BoxCollider2D))]
public class TurretBlock : MonoBehaviour
{
    private int gridSize = 2; // N for N*N
    private List<TurretInstance> instances = new List<TurretInstance>();
    private Color baseCellColor = new Color(0.25f, 0.55f, 0.75f, 0.35f);

    [Header("Out of Bounds Despawn")]
    [SerializeField] private float outOfBoundsLifeTime = 3f;
    private float outOfBoundsTimer = 0f;
    private bool isOutOfBounds = false;

    [HideInInspector] public Color gradeColor = Color.white;
    [HideInInspector] public string gradeName = "Normal";
    private readonly List<GameObject> levelTextCanvases = new List<GameObject>();

    [Header("Drag")]
    [SerializeField] private float dragDepthFromCamera = 10f;
    [SerializeField] private LayerMask gridCellMask    = ~0;

    [Header("Preview Text")]
    [Tooltip("두 포탑이 합쳐질 때 표시되는 미리보기 레벨 텍스트 색상")]
    [SerializeField] private Color previewLevelTextColor = new Color(1f, 0.95f, 0.4f, 1f);  // 기본: 밝은 노란색

    private readonly List<GridCell> occupiedCells = new List<GridCell>();

    private Camera   mainCamera;
    private GridCell anchorCell;
    private GridCell dragStartAnchorCell;
    private Vector3  dragOffset;
    private Vector3  startPosition;
    private bool     isDragging;
    private GridCell lastHoveredCellForPreview;

    // Cache the original instances to handle invalid rotation fallback
    private List<Vector2Int> dragStartLocalPositions;

    public int GridSize => gridSize;
    public IReadOnlyList<TurretInstance> Instances => instances;
    public GridCell AnchorCell => anchorCell;
    public IReadOnlyList<GridCell> OccupiedCells => occupiedCells;
    public bool IsPlaced => occupiedCells.Count > 0;
    
    private bool isInInventory;
    public bool IsInInventory => isInInventory;
    // Tracks whether the drag started from inside the inventory (set in OnMouseDown, cleared in OnMouseUp)
    private bool dragStartedFromInventory;

    public static TurretBlock DraggedBlock { get; private set; }

    private void OnDestroy()
    {
        if (DraggedBlock == this)
        {
            DraggedBlock = null;
        }
    }

    private TruckGrid FindActiveTruckGrid()
    {
        TruckGrid[] grids = FindObjectsByType<TruckGrid>(FindObjectsSortMode.None);
        foreach (var g in grids)
        {
            if (g != null && g.CellPrefab != null)
            {
                return g;
            }
        }
        return FindFirstObjectByType<TruckGrid>(); // fallback
    }

    public void SetInInventory(bool inInventory)
    {
        isInInventory = inInventory;
        if (inInventory)
        {
            // Reparenting and scale adjustment are handled by BlockInventory.TryAddBlock.
            // Here we only reset rotation and refresh visuals.
            transform.localRotation = Quaternion.identity;
        }
        SetColor(new Color(1f, 1f, 1f, 1f));
        UpdateVisualPositions();
        UpdateColliderToShape();
        SetLevelTextsActive(!IsPlaced);
    }

    public void Initialize(int nSize, List<TurretInstance> newInstances, Color blockGradeColor, string nameOfGrade = "Normal")
    {
        gridSize = nSize;
        instances = newInstances;
        this.gradeColor = blockGradeColor;
        this.gradeName = nameOfGrade;
        
        // Clear any old background cells
        foreach (var inst in instances)
        {
            if (inst.backgroundCellRenderer != null)
            {
                Destroy(inst.backgroundCellRenderer.gameObject);
                inst.backgroundCellRenderer = null;
            }
        }
        foreach (var canvasGo in levelTextCanvases)
        {
            if (canvasGo != null)
            {
                Destroy(canvasGo);
            }
        }
        levelTextCanvases.Clear();

        // Dynamically fetch cell sprite and color from TruckGrid cellPrefab
        Sprite cellBackgroundSprite = null;
        Color cellColor = new Color(0.25f, 0.55f, 0.75f, 0.35f);

        TruckGrid grid = FindActiveTruckGrid();
        if (grid != null && grid.CellPrefab != null)
        {
            SpriteRenderer prefabSr = grid.CellPrefab.GetComponent<SpriteRenderer>();
            if (prefabSr != null)
            {
                cellBackgroundSprite = prefabSr.sprite;
                cellColor = prefabSr.color;
            }
        }
        else
        {
            // Fallback: try finding any active GridCell in scene
            GridCell activeCell = FindFirstObjectByType<GridCell>();
            if (activeCell != null)
            {
                SpriteRenderer activeSr = activeCell.GetComponent<SpriteRenderer>();
                if (activeSr != null)
                {
                    cellBackgroundSprite = activeSr.sprite;
                    cellColor = activeSr.color;
                }
            }
        }

        // Apply gradeColor to baseCellColor
        // alpha는 cellColor.a 대신 고정값 사용 (프리팹 값이 너무 낮을 수 있으므로)
        baseCellColor = new Color(blockGradeColor.r, blockGradeColor.g, blockGradeColor.b, 0.3f);
        
        // 1. Spawn cell backgrounds only for the occupied cell coordinates dynamically
        if (cellBackgroundSprite != null)
        {
            foreach (var inst in instances)
            {
                GameObject cellGo = new GameObject($"CellVisual_{inst.localPosition.x}_{inst.localPosition.y}");
                cellGo.transform.SetParent(transform);
                // Slightly behind the turret (Z = 0.05f in local space)
                cellGo.transform.localPosition = new Vector3(inst.localPosition.x, inst.localPosition.y, 0.05f);

                SpriteRenderer sr = cellGo.AddComponent<SpriteRenderer>();
                sr.sprite = cellBackgroundSprite;
                sr.sortingOrder = -1; // Render behind the weapon
                sr.color = baseCellColor;

                // Scale to 1x1 in grid cell units
                float spriteWidth = cellBackgroundSprite.rect.width / cellBackgroundSprite.pixelsPerUnit;
                float spriteHeight = cellBackgroundSprite.rect.height / cellBackgroundSprite.pixelsPerUnit;
                cellGo.transform.localScale = new Vector3(
                    spriteWidth > 0f ? 1.0f / spriteWidth : 1f,
                    spriteHeight > 0f ? 1.0f / spriteHeight : 1f,
                    1f
                );

                // Add BoxCollider2D to cell background for click detection
                BoxCollider2D cellCollider = cellGo.AddComponent<BoxCollider2D>();
                cellCollider.isTrigger = true;
                cellCollider.size = new Vector2(spriteWidth > 0f ? spriteWidth : 1f, spriteHeight > 0f ? spriteHeight : 1f);
                cellCollider.offset = Vector2.zero;

                cellGo.AddComponent<TurretBlockChildCollider>().Initialize(this);

                inst.backgroundCellRenderer = sr;

                // ── 레벨 텍스트: GridCell 프리팹의 Canvas/LayerText 재사용 ────────
                // CellPrefab에 있는 Canvas 자식 오브젝트를 그대로 Instantiate해서 사용.
                // 이렇게 하면 Inspector에서 설정한 폰트·크기·정렬이 그대로 상속된다.
                GameObject clonedCanvas = null;
                if (grid != null && grid.CellPrefab != null)
                {
                    Canvas prefabCanvas = grid.CellPrefab.GetComponentInChildren<Canvas>(true);
                    if (prefabCanvas != null)
                    {
                        clonedCanvas = Instantiate(prefabCanvas.gameObject, transform);
                        clonedCanvas.name = "LevelCanvas";
                        clonedCanvas.transform.localRotation = Quaternion.identity;
                        clonedCanvas.transform.localPosition = new Vector3(
                            inst.localPosition.x, inst.localPosition.y, -0.1f);
                        clonedCanvas.transform.localScale = Vector3.one;

                        // 텍스트 초기화
                        TextMeshProUGUI clonedTMP = clonedCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (clonedTMP != null)
                        {
                            clonedTMP.text = $"{inst.level}";
                            clonedTMP.color = Color.white;
                            clonedTMP.raycastTarget = false;
                        }

                        // Canvas sortingOrder를 배경 셀보다 위로 설정
                        Canvas clonedCanvasComp = clonedCanvas.GetComponent<Canvas>();
                        if (clonedCanvasComp != null)
                        {
                            clonedCanvasComp.sortingLayerID = sr.sortingLayerID;
                            clonedCanvasComp.sortingOrder   = sr.sortingOrder + 12;
                        }

                        // GraphicRaycaster가 마우스 이벤트를 가로채지 못하도록 삭제
                        UnityEngine.UI.GraphicRaycaster raycaster = clonedCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                        if (raycaster != null)
                        {
                            Destroy(raycaster);
                        }
                    }
                }

                // 프리팹 Canvas를 못 찾은 경우 폴백: 기존 방식으로 동적 생성
                if (clonedCanvas == null)
                {
                    clonedCanvas = new GameObject("LevelCanvas");
                    clonedCanvas.transform.SetParent(transform);
                    clonedCanvas.transform.localRotation = Quaternion.identity;
                    clonedCanvas.transform.localPosition = new Vector3(
                        inst.localPosition.x, inst.localPosition.y, -0.1f);
                    clonedCanvas.transform.localScale = Vector3.one;

                    Canvas canvas = clonedCanvas.AddComponent<Canvas>();
                    canvas.renderMode     = RenderMode.WorldSpace;
                    canvas.sortingLayerID = sr.sortingLayerID;
                    canvas.sortingOrder   = sr.sortingOrder + 12;

                    RectTransform canvasRect = clonedCanvas.GetComponent<RectTransform>();
                    canvasRect.sizeDelta = new Vector2(1f, 1f);

                    GameObject textGo = new GameObject("LevelText");
                    textGo.transform.SetParent(clonedCanvas.transform);
                    textGo.transform.localPosition = Vector3.zero;
                    textGo.transform.localRotation = Quaternion.identity;
                    textGo.transform.localScale    = Vector3.one;

                    TextMeshProUGUI textMesh = textGo.AddComponent<TextMeshProUGUI>();
                    textMesh.text          = $"{inst.level}";
                    textMesh.alignment     = TextAlignmentOptions.Center;
                    textMesh.fontSize      = 0.3f;
                    textMesh.color         = Color.white;
                    textMesh.raycastTarget = false;

                    RectTransform textRect = textGo.GetComponent<RectTransform>();
                    textRect.sizeDelta = new Vector2(1f, 1f);
                }

                levelTextCanvases.Add(clonedCanvas);
                // ─────────────────────────────────────────────────────────────────
            }
        }

        // 2. Spawn turret weapon visuals (only on specified occupied coordinates)
        foreach(var inst in instances)
        {
            if (inst.data != null && inst.data.weaponPrefab != null)
            {
                TurretWeapon weapon = Instantiate(inst.data.weaponPrefab, transform);
                inst.visualObject = weapon.gameObject;
                inst.weapon = weapon;
                
                // Assign stats/logic if weapon requires initialization
                weapon.Initialize(this, inst);
            }
        }
        
        AdjustScaleToGrid();
        ResetVisualSpritesToDefault();
        SetColor(new Color(1f, 1f, 1f, 1f));

        // Add TurretBlockChildCollider to all child colliders in visualObjects
        foreach (var inst in instances)
        {
            if (inst.visualObject != null)
            {
                Collider2D[] colliders = inst.visualObject.GetComponentsInChildren<Collider2D>(true);
                foreach (var col in colliders)
                {
                    if (col.gameObject.GetComponent<TurretBlockChildCollider>() == null)
                    {
                        col.gameObject.AddComponent<TurretBlockChildCollider>().Initialize(this);
                    }
                }
            }
        }
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        UpdateColliderToShape();
        SetColor(new Color(1f, 1f, 1f, 1f));

        // Ensure a Kinematic Rigidbody2D is present on Awake to guarantee collision detection with the player vehicle.
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
    }

    private void Update()
    {
        // 1. Handle magnet pull and out-of-bounds timer when dropped on the field
        if (!IsPlaced && !isInInventory && !isDragging)
        {
            if (TruckBody.Instance != null && TruckBody.Instance.gameObject.activeInHierarchy && !TruckBody.Instance.IsDestroyed)
            {
                // Magnet pull towards TruckBody.Instance
                Vector3 targetPos = TruckBody.Instance.transform.position;
                Vector3 dir = (targetPos - transform.position).normalized;
                float dist = Vector3.Distance(transform.position, targetPos);
                
                // Accelerates as it gets closer
                float magnetSpeed = Mathf.Max(3f, 15f / (dist + 0.5f));
                transform.position += dir * (magnetSpeed * Time.deltaTime);
            }
            else
            {
                // Fallback: Scroll leftwards with the map speed
                float speed = GameFlowManager.CurrentScrollSpeed;
                transform.position += Vector3.left * (speed * Time.deltaTime);
            }

            // Compute left out-of-bounds limit dynamically using TruckMovement bounds
            float leftLimit = -10f;
            TruckMovement moveComponent = FindFirstObjectByType<TruckMovement>();
            if (moveComponent != null)
            {
                leftLimit = moveComponent.MinPosition.x - 2f;
            }

            if (transform.position.x <= leftLimit)
            {
                if (!isOutOfBounds)
                {
                    isOutOfBounds = true;
                    outOfBoundsTimer = outOfBoundsLifeTime;
                }
                
                outOfBoundsTimer -= Time.deltaTime;
                if (outOfBoundsTimer <= 0f)
                {
                    Debug.Log("Dropped block naturally despawned after sliding out of bounds.");
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                isOutOfBounds = false;
            }
        }
        else
        {
            isOutOfBounds = false;
        }

        // 2. Original keyboard rotate and sell drag-mode operations
        if (!isDragging || Keyboard.current == null) return;

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            RotateClockwise();
            SetColor(CanPlaceAt(GetHoveredAnchorCell()) ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0.3f, 0.3f, 0.5f));
        }

        if (GamePhaseManager.IsMaintenance && BlockInventory.Instance != null)
        {
            if (Keyboard.current.backspaceKey.wasPressedThisFrame || Keyboard.current.deleteKey.wasPressedThisFrame)
            {
                isDragging = false;
                if (DraggedBlock == this) DraggedBlock = null;
                BlockInventory.Instance.ExchangeBlock(this);
                if (BlockInventory.Instance.StoredBlocks.Count == 0)
                {
                    PlacementFocusPanel.Instance?.ClosePanel();
                }
                return;
            }
        }
    }

    public void OnMouseDown()
    {
        // Block manual mouse collection on the field. Player must physically drive into blocks to collect them.
        if (!IsPlaced && !isInInventory) return;

        if (IsPlaced && !GamePhaseManager.CanMovePlacedBlocks) return;

        mainCamera = Camera.main;
        if (mainCamera == null) return;

        isDragging           = true;
        DraggedBlock         = this;
        startPosition        = transform.position;
        dragStartAnchorCell  = anchorCell;
        // Remember whether this drag started from inventory (before we clear inventory state)
        dragStartedFromInventory = isInInventory;
        
        dragStartLocalPositions = new List<Vector2Int>();
        foreach(var inst in instances) dragStartLocalPositions.Add(inst.localPosition);

        // 1. 클릭한 정확한 월드 좌표 캡처
        Vector3 clickWorldPos = GetMouseWorldPosition();

        // 2. 클릭 위치와 블록 root 사이의 "scale-normalized" 비율 저장
        //    (로컬 좌표를 그대로 저장하면 scale 변경 후 다른 월드 위치를 가리키므로
        //     현재 lossyScale로 나눠 cell-ratio 단위로 저장한다)
        Vector3 lossyBefore = transform.lossyScale;
        Vector3 rootOffsetWorld = clickWorldPos - transform.position;
        // cell-ratio: 블록 scale 1단위당 몇 셀인지의 오프셋
        Vector2 clickRatio = new Vector2(
            lossyBefore.x != 0f ? rootOffsetWorld.x / lossyBefore.x : 0f,
            lossyBefore.y != 0f ? rootOffsetWorld.y / lossyBefore.y : 0f
        );

        // 3. 인벤토리에서 먼저 제거 (isInInventory = false 로 변경)
        if (isInInventory && BlockInventory.Instance != null)
        {
            BlockInventory.Instance.RemoveBlock(this);
        }

        // 4. Parent 해제 후 드래그 scale로 재조정 (isInInventory=false이므로 live grid scale 사용)
        transform.SetParent(null);
        AdjustScaleToGrid();

        ClearOccupiedCells();
        ResetVisualSpritesToDefault();
        SetLevelTextsActive(true);

        // 5. 새 scale 기준으로 click ratio를 월드 오프셋으로 복원
        Vector3 lossyAfter = transform.lossyScale;
        Vector3 newRootOffset = new Vector3(
            clickRatio.x * lossyAfter.x,
            clickRatio.y * lossyAfter.y,
            0f
        );

        // 6. 블록 root 위치를 "클릭한 셀 상대 위치"가 마우스와 일치하도록 배치
        transform.position = clickWorldPos - newRootOffset;

        // dragOffset 확정
        dragOffset = transform.position - GetMouseWorldPosition();

        SetColor(new Color(1f, 1f, 1f, 1f));

        // 집중 패널 열기 (트럭 중앙 이동 + 확대)
        PlacementFocusPanel.Instance?.OpenPanel();
    }

    public void AdjustScaleToGrid()
    {
        TruckGrid grid = FindActiveTruckGrid();
        if (grid != null)
        {
            Vector3 gridWorldScale = grid.transform.lossyScale;
            
            if (isInInventory && PlacementFocusPanel.Instance != null)
            {
                gridWorldScale = PlacementFocusPanel.Instance.GetUnzoomedGridLossyScale(grid);
            }
            
            transform.localScale = new Vector3(
                grid.CellSize.x * gridWorldScale.x,
                grid.CellSize.y * gridWorldScale.y,
                1f);
        }
        UpdateVisualPositions();
        UpdateColliderToShape();
    }

    /// <summary>
    /// PlacementFocusPanel이 트럭 확대/축소 애니메이션을 완료한 뒤 호출.
    /// 블록 스케일을 현재 TruckGrid 크기에 맞게 재조정하고,
    /// 블록 중심이 마우스 커서 아래에 오도록 위치를 재보정한다.
    /// </summary>
    public void RecalibrateAfterFocusChange()
    {
        if (!isDragging) return;

        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector3 localMousePos = transform.InverseTransformPoint(mouseWorld);

        // 스케일을 확대된(또는 축소된) Grid 크기에 맞게 재조정
        AdjustScaleToGrid();

        // 재조정 후 이전 마우스 로컬 좌표가 다시 현재 마우스 월드 위치에 오도록 정렬
        Vector3 newMouseWorld = transform.TransformPoint(localMousePos);
        transform.position += (mouseWorld - newMouseWorld);
        dragOffset = transform.position - mouseWorld;
    }

    public void OnMouseDrag()
    {
        if (!isDragging || mainCamera == null) return;

        // 드래그 중인 원위치 (마우스 + 오프셋)
        Vector3 rawPos = GetMouseWorldPosition() + dragOffset;
        transform.position = rawPos; // GetHoveredAnchorCell이 올바른 위치를 찾도록 일단 원위치로 세팅

        GridCell targetCell = GetHoveredAnchorCell();

        // Hover 셀이 변경된 경우에만 미리보기 업데이트를 수행하여 매 프레임 깜빡임 방지
        if (targetCell != lastHoveredCellForPreview)
        {
            RestorePlacedSprites();
            lastHoveredCellForPreview = targetCell;

            if (targetCell != null)
            {
                bool canPlace = CanPlaceAt(targetCell);
                UpdatePreview(targetCell, canPlace);
            }
            else
            {
                ClearPreview();
            }
        }

        if (targetCell != null)
        {
            // Hover 중일 때는 딱딱 그리드에 스냅
            transform.position = targetCell.SnapPosition;
            bool canPlace = CanPlaceAt(targetCell);

            // 색상은 흰색(불투명)으로 두되, 설치 불가면 붉은색 틴트
            SetColor(canPlace ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 0.3f, 0.3f, 1f));
        }
        else
        {
            // 그리드 밖이면 원위치
            SetColor(new Color(1f, 1f, 1f, 1f));
        }
    }

    private void UpdatePreview(GridCell targetAnchorCell, bool canPlace)
    {
        TruckGrid grid = targetAnchorCell.Grid;
        for (int i = 0; i < instances.Count; i++)
        {
            var inst = instances[i];
            GridCell cell = grid.GetCell(targetAnchorCell.GridX + inst.localPosition.x, targetAnchorCell.GridY + inst.localPosition.y);

            bool isOccupied      = (cell != null && cell.IsOccupied);
            bool showMergePreview = canPlace && isOccupied;   // 합칠 수 있는 경우
            bool hideConflict     = !canPlace && isOccupied;  // 합칠 수 없는 셀에 올라간 경우

            // ── 레벨 텍스트 + 스프라이트 미리보기 ─────────────────────────────
            if (levelTextCanvases != null && i < levelTextCanvases.Count)
            {
                GameObject canvasGo = levelTextCanvases[i];
                if (canvasGo != null)
                {
                    if (showMergePreview)
                    {
                        // 합산 레벨 계산
                        int rawSum = inst.level;
                        for (int j = 0; j < cell.PlacedTurrets.Count; j++)
                            rawSum += cell.PlacedTurrets[j].instance.level;
                            
                        int displaySum = Mathf.Min(10, rawSum);

                        // 텍스트: 노란색(또는 빨간색) 합산 레벨
                        canvasGo.SetActive(true);
                        TMPro.TextMeshProUGUI textMesh = canvasGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                        if (textMesh != null)
                        {
                            textMesh.text  = $"{displaySum}";
                            if (rawSum >= 10)
                            {
                                textMesh.color = Color.red;
                            }
                            else
                            {
                                textMesh.color = previewLevelTextColor;
                            }
                        }

                        // 스프라이트: 합산 레벨에 맞는 sprite를 배치된 포탑에 임시 적용
                        // sprite 교체 후 UpdateVisualPositions()로 스케일 재계산 → 크기 일치
                        HashSet<TurretBlock> blocksToUpdate = new HashSet<TurretBlock>();
                        for (int j = 0; j < cell.PlacedTurrets.Count; j++)
                        {
                            var placed = cell.PlacedTurrets[j];
                            if (placed.instance.data == null) continue;
                            Sprite previewSprite = placed.instance.data.GetSpriteForLevel(displaySum);
                            SpriteRenderer placedSr = placed.block.GetSpriteRenderer(placed.instance);
                            if (placedSr != null && previewSprite != null)
                            {
                                placedSr.sprite = previewSprite;
                                blocksToUpdate.Add(placed.block);
                            }
                        }
                        foreach (var b in blocksToUpdate)
                            b.UpdateVisualPositions();
                    }
                    else if (hideConflict)
                    {
                        // 합치기 불가능한 셀 위 → 드래그 블록 텍스트 숨김
                        canvasGo.SetActive(false);
                    }
                    else
                    {
                        // 빈 셀 위 → 일반 레벨 표시
                        canvasGo.SetActive(true);
                        TMPro.TextMeshProUGUI textMesh = canvasGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                        if (textMesh != null)
                        {
                            textMesh.text  = $"{inst.level}";
                            textMesh.color = Color.white;
                        }
                    }
                }
            }
            // ──────────────────────────────────────────────────────────────────

            // 시각적 겹침 방지: 합치기 가능하고 이미 포탑이 있는 셀이면 드래그 모델 숨김
            if (inst.visualObject != null)
                inst.visualObject.SetActive(!showMergePreview);
        }
    }


    private void ClearPreview()
    {
        for (int i = 0; i < instances.Count; i++)
        {
            var inst = instances[i];

            if (levelTextCanvases != null && i < levelTextCanvases.Count)
            {
                GameObject canvasGo = levelTextCanvases[i];
                if (canvasGo != null)
                {
                    canvasGo.SetActive(true);
                    TMPro.TextMeshProUGUI textMesh = canvasGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (textMesh != null)
                    {
                        textMesh.text  = $"{inst.level}";
                        textMesh.color = Color.white;  // 미리보기 색상 초기화
                    }
                }
            }

            if (inst.visualObject != null)
                inst.visualObject.SetActive(true);
        }

        // ── 배치된 포탑 sprite 원상복구 ──────────────────────────────────────
        RestorePlacedSprites();
        // ─────────────────────────────────────────────────────────────────────
    }

    /// <summary>
    /// 드래그 미리보기로 임시 변경한 모든 배치 포탑의 sprite를
    /// 현재 레벨 기준으로 즉시 복원한다. OnMouseDrag 매 프레임 + ClearPreview 양쪽에서 호출.
    /// </summary>
    private void RestorePlacedSprites()
    {
        TruckGrid[] allGrids = FindObjectsByType<TruckGrid>(FindObjectsSortMode.None);
        HashSet<TurretBlock> blocksToUpdate = new HashSet<TurretBlock>();
        foreach (var grid in allGrids)
        {
            if (grid == null) continue;
            foreach (var cell in grid.Cells)
            {
                if (cell == null || !cell.IsOccupied) continue;
                
                int totalLevel = cell.CurrentTotalLevel;
                for (int i = 0; i < cell.PlacedTurrets.Count; i++)
                {
                    var pt = cell.PlacedTurrets[i];
                    if (pt.block == null || pt.instance.data == null) continue;
                    SpriteRenderer sr = pt.block.GetSpriteRenderer(pt.instance);
                    if (sr != null)
                    {
                        if (i == cell.PlacedTurrets.Count - 1)
                        {
                            sr.enabled = true;
                            Sprite correctSprite = pt.instance.data.GetSpriteForLevel(totalLevel);
                            if (sr.sprite != correctSprite)
                            {
                                sr.sprite = correctSprite;
                                blocksToUpdate.Add(pt.block);
                            }
                        }
                        else
                        {
                            sr.enabled = false;
                        }
                    }
                }
            }
        }
        // sprite가 복원된 블록만 스케일 재계산
        foreach (var b in blocksToUpdate)
            b.UpdateVisualPositions();
    }


    public void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;
        if (DraggedBlock == this) DraggedBlock = null;

        lastHoveredCellForPreview = null;
        ClearPreview(); // 드래그 종료 시 프리뷰 원상복구

        // 인벤토리 교환 슬롯 위에 드롭한 경우 즉시 교환
        if (BlockInventory.Instance != null && BlockInventory.Instance.IsOverExchangeSlot(GetMouseWorldPosition()))
        {
            BlockInventory.Instance.ExchangeBlock(this);
            if (BlockInventory.Instance.StoredBlocks.Count == 0)
            {
                PlacementFocusPanel.Instance?.ClosePanel();
            }
            return;
        }

        GridCell targetCell = GetHoveredAnchorCell();
        if (TryPlaceAt(targetCell))
        {
            // Successfully placed on grid – clear drag state and do NOT return to inventory.
            dragStartAnchorCell = null;
            dragStartLocalPositions = null;
            dragStartedFromInventory = false;
            SetColor(new Color(1f, 1f, 1f, 1f));

            // 인벤토리에 남은 블록이 없으면 자동으로 패널 닫기 (공통)
            if (BlockInventory.Instance != null && BlockInventory.Instance.StoredBlocks.Count == 0)
            {
                PlacementFocusPanel.Instance?.ClosePanel();
            }
            return;
        }

        // Placement failed: try to return to inventory if we originally came from there
        // OR if there was no original anchor (fresh from shop/drop).
        if (dragStartedFromInventory || dragStartAnchorCell == null)
        {
            if (BlockInventory.Instance != null && BlockInventory.Instance.TryAddBlock(this))
            {
                dragStartLocalPositions = null;
                dragStartedFromInventory = false;
                SetColor(new Color(1f, 1f, 1f, 1f));

                // 인벤토리에 남은 블록이 없으면 자동으로 패널 닫기 (공통)
                if (BlockInventory.Instance != null && BlockInventory.Instance.StoredBlocks.Count == 0)
                {
                    PlacementFocusPanel.Instance?.ClosePanel();
                }
                return;
            }
        }

        // Last resort: restore to original position/anchor on the grid
        transform.position = startPosition;
        RestoreDragStartShape();
        TryPlaceAt(dragStartAnchorCell);

        if (dragStartAnchorCell == null && BlockInventory.Instance != null && BlockInventory.Instance.HasSpace)
        {
             BlockInventory.Instance.TryAddBlock(this);
        }

        dragStartAnchorCell = null;
        dragStartLocalPositions = null;
        dragStartedFromInventory = false;
        SetColor(new Color(1f, 1f, 1f, 1f));
        SetLevelTextsActive(!IsPlaced);

        // 인벤토리에 남은 블록이 없으면 자동으로 패널 닫기 (공통)
        if (BlockInventory.Instance != null && BlockInventory.Instance.StoredBlocks.Count == 0)
        {
            PlacementFocusPanel.Instance?.ClosePanel();
        }
    }

    public bool TryPlaceAt(GridCell targetAnchorCell)
    {
        if (!CanPlaceAt(targetAnchorCell)) return false;

        TruckGrid grid = targetAnchorCell.Grid;
        for (int i = 0; i < instances.Count; i++)
        {
            var inst = instances[i];
            GridCell cell = grid.GetCell(targetAnchorCell.GridX + inst.localPosition.x, targetAnchorCell.GridY + inst.localPosition.y);
            if (cell.TryPlace(this, inst)) occupiedCells.Add(cell);
        }

        anchorCell         = targetAnchorCell;
        transform.SetParent(grid.transform);
        transform.localScale = new Vector3(grid.CellSize.x, grid.CellSize.y, 1f);
        transform.position = targetAnchorCell.SnapPosition;
        UpdateVisualPositions();
        UpdateColliderToShape();
        SetLevelTextsActive(false);
        return true;
    }

    public void RotateClockwise()
    {
        if (!GamePhaseManager.CanRotateBlocks) return;

        List<Vector2Int> previousPositions = new List<Vector2Int>();
        foreach(var inst in instances) previousPositions.Add(inst.localPosition);

        GridCell previousAnchorCell = anchorCell;
        bool wasPlaced = IsPlaced;

        if (wasPlaced) ClearOccupiedCells();

        // 1. Capture original mouse position in both world and rescaled local space before rotation
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 localMousePos = Vector3.zero;
        if (isDragging)
        {
            localMousePos = transform.InverseTransformPoint(mouseWorldPos);
        }

        // Standard 90 degree rotation
        for (int i = 0; i < instances.Count; i++)
        {
            Vector2Int pos = instances[i].localPosition;
            instances[i].localPosition = new Vector2Int(pos.y, -pos.x);
        }

        Vector2Int shift = NormalizeShapeToPositiveCoordinates();
        UpdateColliderToShape();
        UpdateVisualPositions();

        // 2. If dragging, offset block position so that the hovered cell point aligns perfectly under the mouse cursor post-rotation
        if (isDragging)
        {
            TruckGrid grid = FindActiveTruckGrid();
            float stepRatioX = 1f;
            float stepRatioY = 1f;
            if (grid != null && grid.CellSize.x > 0f && grid.CellSize.y > 0f)
            {
                stepRatioX = grid.CellStep.x / grid.CellSize.x;
                stepRatioY = grid.CellStep.y / grid.CellSize.y;
            }

            // Calculate the mouse position in unscaled cell coordinates before rotation
            float unscaledCellX = localMousePos.x / stepRatioX;
            float unscaledCellY = localMousePos.y / stepRatioY;

            // Rotate the unscaled cell coordinates: (x, y) -> (y, -x)
            float rotatedUnscaledX = unscaledCellY;
            float rotatedUnscaledY = -unscaledCellX;

            // Apply the normalization shift (shift is in integer grid coordinates)
            float finalUnscaledX = rotatedUnscaledX - shift.x;
            float finalUnscaledY = rotatedUnscaledY - shift.y;

            // Scale back to local visual coordinate space
            Vector3 rotatedLocalMousePos = new Vector3(
                finalUnscaledX * stepRatioX,
                finalUnscaledY * stepRatioY,
                localMousePos.z
            );

            // Calculate precise world position offset required to snap the rotated local point back to the mouse world position
            Vector3 targetWorldPos = mouseWorldPos - (transform.TransformPoint(rotatedLocalMousePos) - transform.position);
            transform.position = targetWorldPos;

            // Re-calibrate dragOffset to ensure seamless subsequent dragging
            dragOffset = transform.position - mouseWorldPos;
        }

        if (wasPlaced && !TryPlaceAt(previousAnchorCell))
        {
            // Revert
            for (int i = 0; i < instances.Count; i++)
            {
                instances[i].localPosition = previousPositions[i];
            }
            UpdateColliderToShape();
            UpdateVisualPositions();
            TryPlaceAt(previousAnchorCell);
        }

        SetColor(isDragging ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 1f, 1f, 1f));
    }

    public bool CanPlaceAt(GridCell targetAnchorCell)
    {
        if (targetAnchorCell == null || targetAnchorCell.Grid == null) return false;

        TruckGrid grid = targetAnchorCell.Grid;
        for (int i = 0; i < instances.Count; i++)
        {
            var inst = instances[i];
            GridCell cell = grid.GetCell(targetAnchorCell.GridX + inst.localPosition.x, targetAnchorCell.GridY + inst.localPosition.y);
            if (cell == null || !cell.CanPlace(inst)) return false;
        }

        return true;
    }

    private void ClearOccupiedCells()
    {
        for (int i = 0; i < occupiedCells.Count; i++)
        {
            occupiedCells[i].ClearPlacedBlock(this);
        }
        occupiedCells.Clear();
        anchorCell = null;
    }

    public GridCell GetHoveredAnchorCell()
    {
        TruckGrid[] allGrids = FindObjectsByType<TruckGrid>(FindObjectsSortMode.None);
        if (allGrids == null || allGrids.Length == 0) return null;

        Vector2 pivotPosition = transform.position;
        GridCell closestCell = null;
        float minDistance = float.MaxValue;
        TruckGrid closestGrid = null;

        // Iterate through all grids and all their cells in the scene to find the closest one
        foreach (var grid in allGrids)
        {
            if (grid == null) continue;
            foreach (var cell in grid.Cells)
            {
                if (cell != null)
                {
                    float dist = Vector2.Distance(pivotPosition, cell.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestCell = cell;
                        closestGrid = grid;
                    }
                }
            }
        }

        // If the closest cell is within a generous distance (e.g., within 5.0 * cell size step of that grid), return it.
        // This acts as a magnet snap assist, allowing easy placement even when not perfectly aligned.
        if (closestCell != null && closestGrid != null)
        {
            Vector2 step = closestGrid.CellStep;
            float maxAllowedDistance = Mathf.Max(step.x, step.y) * 5.0f;
            if (minDistance <= maxAllowedDistance)
            {
                return closestCell;
            }
        }

        return null;
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (Mouse.current == null) return transform.position;

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Vector3 mousePosition   = new Vector3(pointerPosition.x, pointerPosition.y, dragDepthFromCamera);
        Vector3 worldPosition   = mainCamera.ScreenToWorldPoint(mousePosition);
        worldPosition.z         = transform.position.z;
        return worldPosition;
    }

    private void UpdateCellColors(Color tint)
    {
        for (int i = 0; i < instances.Count; i++)
        {
            var inst = instances[i];
            SpriteRenderer sr = inst.backgroundCellRenderer;
            if (sr == null) continue;

            if (IsPlaced && !isDragging)
            {
                // 그리드에 배치된 상태: 셀 배경 숨김
                sr.color = new Color(0f, 0f, 0f, 0f);
            }
            else if (isDragging)
            {
                // 드래그 중: 등급 색 숨김, 트인트만 반영 (배경 셀은 투명하게)
                sr.color = new Color(1f * tint.r, 1f * tint.g, 1f * tint.b, 0f);
            }
            else
            {
                // 인벤토리 또는 필드에 떨어진 상태: 등급 색(반투명)
                sr.color = new Color(
                    baseCellColor.r * tint.r,
                    baseCellColor.g * tint.g,
                    baseCellColor.b * tint.b,
                    baseCellColor.a * tint.a
                );
            }
        }
    }

    private void SetColor(Color color)
    {
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i].visualObject != null)
            {
                SpriteRenderer[] srs = instances[i].visualObject.GetComponentsInChildren<SpriteRenderer>();
                foreach(var sr in srs)
                {
                    // Update the color but preserve active/enabled state logic
                    sr.color = color;
                }
            }
        }

        UpdateCellColors(color);
    }

    public SpriteRenderer GetSpriteRenderer(TurretInstance inst)
    {
        if (inst.visualObject == null) return null;
        return inst.visualObject.GetComponentInChildren<SpriteRenderer>();
    }

    public void ResetVisualSpritesToDefault()
    {
        for (int i = 0; i < instances.Count; i++)
        {
            var inst = instances[i];
            SpriteRenderer sr = GetSpriteRenderer(inst);
            if (sr != null)
            {
                sr.enabled = true;
                if (inst.data != null)
                {
                    sr.sprite = inst.data.GetSpriteForLevel(inst.level);
                }
            }
        }
    }

    private void RestoreDragStartShape()
    {
        if (dragStartLocalPositions == null || dragStartLocalPositions.Count == 0) return;
        for(int i = 0; i < instances.Count; i++)
        {
            instances[i].localPosition = dragStartLocalPositions[i];
        }
        UpdateColliderToShape();
        UpdateVisualPositions();
    }

    private Vector2Int NormalizeShapeToPositiveCoordinates()
    {
        if (instances.Count == 0) return Vector2Int.zero;
        
        int minX = instances[0].localPosition.x;
        int minY = instances[0].localPosition.y;

        for (int i = 1; i < instances.Count; i++)
        {
            minX = Mathf.Min(minX, instances[i].localPosition.x);
            minY = Mathf.Min(minY, instances[i].localPosition.y);
        }

        if (minX == 0 && minY == 0) return Vector2Int.zero;

        for (int i = 0; i < instances.Count; i++)
        {
            instances[i].localPosition = new Vector2Int(instances[i].localPosition.x - minX, instances[i].localPosition.y - minY);
        }

        return new Vector2Int(minX, minY);
    }

    private Vector3 GetRelativeScale(Transform child, Transform ancestor)
    {
        Vector3 scale = Vector3.one;
        Transform current = child;
        while (current != null && current != ancestor)
        {
            Vector3 localScale = current.localScale;
            scale.x *= localScale.x;
            scale.y *= localScale.y;
            scale.z *= localScale.z;
            current = current.parent;
        }
        return scale;
    }

    public void UpdateVisualPositions()
    {
        TruckGrid grid = FindActiveTruckGrid();
        float stepRatioX = 1f;
        float stepRatioY = 1f;
        if (grid != null && grid.CellSize.x > 0f && grid.CellSize.y > 0f)
        {
            stepRatioX = grid.CellStep.x / grid.CellSize.x;
            stepRatioY = grid.CellStep.y / grid.CellSize.y;
        }

        for (int i = 0; i < instances.Count; i++)
        {
            var inst = instances[i];
            float posX = inst.localPosition.x * stepRatioX;
            float posY = inst.localPosition.y * stepRatioY;

            if (inst.visualObject != null)
            {
                // 포탑 비주얼 루트의 로컬 격자 중심점 스냅 좌표 설정
                inst.visualObject.transform.localPosition = new Vector3(posX, posY, 0f);

                BoxCollider2D col = inst.visualObject.GetComponentInChildren<BoxCollider2D>();
                if (col != null)
                {
                    // 1. BoxCollider2D가 속한 트랜스폼 기준의 누적 상대 스케일 계산
                    Vector3 relativeScale = GetRelativeScale(col.transform, inst.visualObject.transform);

                    // 2. TurretBlock 로컬 공간에서 이 콜라이더 영역이 정확히 1.0 x 1.0 (즉 셀 한 칸 크기)이 되도록
                    // BoxCollider2D.size 필드를 직접 계산하여 강제 조절!
                    float targetSizeX = relativeScale.x > 0f ? 1.0f / relativeScale.x : 1f;
                    float targetSizeY = relativeScale.y > 0f ? 1.0f / relativeScale.y : 1f;
                    col.size = new Vector2(targetSizeX, targetSizeY);
                    
                    // 3. 콜라이더의 로컬 오프셋도 중심으로 깔끔하게 zero 정렬
                    col.offset = Vector2.zero;

                    // 4. 비주얼 스프라이트의 크기도 이 BoxCollider2D 영역(1.0 x 1.0)에 꽉 차도록 스케일링 설정
                    SpriteRenderer sr = inst.visualObject.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && sr.sprite != null)
                    {
                        float spriteWidth = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
                        float spriteHeight = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;

                        Vector3 srRelativeScale = GetRelativeScale(sr.transform, inst.visualObject.transform);
                        float srWidth = srRelativeScale.x * spriteWidth;
                        float srHeight = srRelativeScale.y * spriteHeight;

                        float scaleFactorX = srWidth > 0f ? 1.0f / srWidth : 1f;
                        float scaleFactorY = srHeight > 0f ? 1.0f / srHeight : 1f;

                        inst.visualObject.transform.localScale = new Vector3(scaleFactorX, scaleFactorY, 1f);
                    }
                    else
                    {
                        inst.visualObject.transform.localScale = Vector3.one;
                    }
                }
                else
                {
                    // Fallback: SpriteRenderer 기준 정밀 보정
                    SpriteRenderer sr = inst.visualObject.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && sr.sprite != null)
                    {
                        Vector3 relativeScale = GetRelativeScale(sr.transform, inst.visualObject.transform);
                        float spriteWidth = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
                        float spriteHeight = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;

                        float relativeWidth = relativeScale.x * spriteWidth;
                        float relativeHeight = relativeScale.y * spriteHeight;

                        float scaleFactorX = relativeWidth > 0f ? 1.0f / relativeWidth : 1f;
                        float scaleFactorY = relativeHeight > 0f ? 1.0f / relativeHeight : 1f;

                        inst.visualObject.transform.localScale = new Vector3(scaleFactorX, scaleFactorY, 1f);
                    }
                }
            }

            // Also update the position of the background cell visual!
            if (inst.backgroundCellRenderer != null)
            {
                inst.backgroundCellRenderer.transform.localPosition = new Vector3(posX, posY, 0.05f);
            }

            // Also update the position of the level text canvas to follow the block rotation/movement!
            if (levelTextCanvases != null && i < levelTextCanvases.Count)
            {
                GameObject canvasGo = levelTextCanvases[i];
                if (canvasGo != null)
                {
                    canvasGo.transform.localPosition = new Vector3(posX, posY, -0.1f);

                    // localScale = (1,1,1) 유지: 블록 lossyScale == GridCell lossyScale 이므로
                    // 텍스트가 확대된 Grid Cell 텍스트와 동일한 화면 크기로 렌더링됨.
                    canvasGo.transform.localScale = Vector3.one;
                }
            }
        }
    }

    private void UpdateColliderToShape()
    {
        BoxCollider2D blockCollider = GetComponent<BoxCollider2D>();
        if (blockCollider == null) return;

        if (!IsPlaced && !isInInventory)
        {
            // 필드에 드롭된 블록: 트럭/트레일러가 밟아서 수거할 수 있도록 trigger 활성화
            blockCollider.enabled = true;
            blockCollider.isTrigger = true;
            blockCollider.size = new Vector2(gridSize, gridSize);
            blockCollider.offset = GetLocalCenter();
        }
        else if (isInInventory)
        {
            // 인벤토리 블록: 루트 BoxCollider2D를 직접 활성화(non-trigger)해서
            // OnMouseDown이 TurretBlock에 바로 호출되도록 한다.
            // 자식 TurretBlockChildCollider 체인을 거치지 않으므로
            // Physics2D 동기화 타이밍 문제로 인한 시각-클릭 불일치가 발생하지 않는다.
            blockCollider.enabled = true;
            blockCollider.isTrigger = false;

            // 블록의 모든 셀을 감싸는 Bounding Box 계산
            if (instances != null && instances.Count > 0)
            {
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                foreach (var inst in instances)
                {
                    minX = Mathf.Min(minX, inst.localPosition.x);
                    minY = Mathf.Min(minY, inst.localPosition.y);
                    maxX = Mathf.Max(maxX, inst.localPosition.x);
                    maxY = Mathf.Max(maxY, inst.localPosition.y);
                }
                TruckGrid grid = FindActiveTruckGrid();
                float stepX = 1f, stepY = 1f;
                if (grid != null && grid.CellSize.x > 0f && grid.CellSize.y > 0f)
                {
                    stepX = grid.CellStep.x / grid.CellSize.x;
                    stepY = grid.CellStep.y / grid.CellSize.y;
                }
                float width  = (maxX - minX) * stepX + 1f;
                float height = (maxY - minY) * stepY + 1f;
                
                Vector3 center = GetBoundingBoxCenter();
                blockCollider.size   = new Vector2(width, height);
                blockCollider.offset = new Vector2(center.x, center.y);
            }
            else
            {
                blockCollider.size   = new Vector2(gridSize, gridSize);
                blockCollider.offset = GetLocalCenter();
            }
        }
        else
        {
            // 그리드에 배치된 블록: 루트 콜라이더 비활성화 (자식 콜라이더로 인터랙션)
            blockCollider.enabled = false;
        }
    }

    public Vector3 GetLocalCenter()
    {
        if (instances == null || instances.Count == 0)
        {
            float centerOffset = (gridSize - 1) * 0.5f;
            return new Vector3(centerOffset, centerOffset, 0f);
        }

        float sumX = 0f;
        float sumY = 0f;
        foreach (var inst in instances)
        {
            sumX += inst.localPosition.x;
            sumY += inst.localPosition.y;
        }

        return new Vector3(sumX / instances.Count, sumY / instances.Count, 0f);
    }

    public Vector3 GetBoundingBoxCenter()
    {
        if (instances == null || instances.Count == 0)
        {
            float centerOffset = (gridSize - 1) * 0.5f;
            return new Vector3(centerOffset, centerOffset, 0f);
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var inst in instances)
        {
            minX = Mathf.Min(minX, inst.localPosition.x);
            minY = Mathf.Min(minY, inst.localPosition.y);
            maxX = Mathf.Max(maxX, inst.localPosition.x);
            maxY = Mathf.Max(maxY, inst.localPosition.y);
        }

        TruckGrid grid = FindActiveTruckGrid();
        float stepX = 1f, stepY = 1f;
        if (grid != null && grid.CellSize.x > 0f && grid.CellSize.y > 0f)
        {
            stepX = grid.CellStep.x / grid.CellSize.x;
            stepY = grid.CellStep.y / grid.CellSize.y;
        }

        float cx = (minX * stepX + maxX * stepX) * 0.5f;
        float cy = (minY * stepY + maxY * stepY) * 0.5f;

        return new Vector3(cx, cy, 0f);
    }

    private void SetLevelTextsActive(bool active)
    {
        foreach (var canvasGo in levelTextCanvases)
        {
            if (canvasGo != null)
            {
                canvasGo.SetActive(active);
            }
        }
    }

    /// <summary>
    /// 특정 TurretInstance에 연결된 레벨 텍스트 캔버스를 활성/비활성화한다.
    /// UpdatePreview에서 다른 배치된 블록의 텍스트가 드래그 미리보기 텍스트와 겹치지 않도록 숨길 때 사용.
    /// </summary>
    public void SetLevelTextForInstance(TurretInstance inst, bool active)
    {
        int idx = instances.IndexOf(inst);
        if (idx < 0 || idx >= levelTextCanvases.Count) return;
        GameObject canvasGo = levelTextCanvases[idx];
        if (canvasGo != null)
        {
            canvasGo.SetActive(active);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore trigger collision if already collected, placed, or being dragged
        if (IsPlaced || isInInventory || isDragging) return;

        // Try detecting player's truck or trailing trailer components
        TruckBody truck = other.GetComponentInParent<TruckBody>();
        TrailerBody trailer = other.GetComponentInParent<TrailerBody>();

        if (truck != null || trailer != null)
        {
            TryCollect();
        }
    }

    private void TryCollect()
    {
        if (BlockInventory.Instance != null)
        {
            if (!BlockInventory.Instance.HasSpace) return;

            if (BlockInventory.Instance.TryAddBlock(this))
            {
                Debug.Log("Loot block successfully collected by vehicle collision!");
                
                // Refresh collider state to disable triggers immediately
                UpdateColliderToShape();
            }
            else
            {
                Debug.LogWarning("Inventory is full! Cannot collect dropped block.");
            }
        }
    }
}

public class TurretBlockChildCollider : MonoBehaviour
{
    private TurretBlock parentBlock;

    public void Initialize(TurretBlock parent)
    {
        parentBlock = parent;
    }

    private void OnMouseDown()
    {
        if (parentBlock != null)
        {
            parentBlock.OnMouseDown();
        }
    }

    private void OnMouseDrag()
    {
        if (parentBlock != null)
        {
            parentBlock.OnMouseDrag();
        }
    }

    private void OnMouseUp()
    {
        if (parentBlock != null)
        {
            parentBlock.OnMouseUp();
        }
    }
}
