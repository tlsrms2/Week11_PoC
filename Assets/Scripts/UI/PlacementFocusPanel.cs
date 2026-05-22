using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 블록 배치 집중 패널.
/// 인벤토리에서 블록을 집으면 게임을 일시정지하고 트럭+트레일러를 화면 중앙으로 당기고 확대한다.
/// Defense: 블록 드롭/배치 시 닫힌다.
/// Maintenance: 패널 바깥 클릭 시 닫힌다.
/// </summary>
public class PlacementFocusPanel : MonoBehaviour
{
    public static PlacementFocusPanel Instance { get; private set; }

    [Header("Truck References")]
    [Tooltip("트럭 루트 Transform (TruckMovement/TruckBody가 있는 오브젝트)")]
    [SerializeField] private Transform truckRoot;

    [Header("Focus Settings")]
    [Tooltip("확대 배율 (기본 2.0)")]
    [SerializeField] private float focusScale = 2.0f;
    [Tooltip("확대된 트럭의 화면 내 월드 좌표 (기본 중앙)")]
    [SerializeField] private Vector3 focusTargetPosition = Vector3.zero;
    [Tooltip("이동/확대 애니메이션 시간 (초)")]
    [SerializeField] private float animationDuration = 0.35f;

    public float FocusScale => focusScale;

    [Header("Overlay UI")]
    [Tooltip("어두운 오버레이 UI Image (비워두면 캔버스와 함께 자동 생성)")]
    [SerializeField] private Image overlayImage;
    [Tooltip("오버레이 색상")]
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.6f);

    // ── 상태 ────────────────────────────────────────────────────────────────────
    private Vector3 savedTruckPosition;
    private Vector3 savedTruckLocalScale;

    // 트레일러들: TrailerFollow로 트럭을 따라가므로 트럭 이동 시 자동 추적.
    // 단, 트레일러는 트럭 루트와 별도 오브젝트이므로 같이 이동/스케일 처리한다.
    private readonly List<TrailerBody> cachedTrailers = new List<TrailerBody>();
    private readonly List<Vector3> savedTrailerPositions = new List<Vector3>();
    private readonly List<Vector3> savedTrailerLocalScales = new List<Vector3>();

    private bool isOpen;
    private bool isAnimating;
    private bool openedThisFrame;
    private Coroutine currentAnimation;

    public bool IsOpen => isOpen;

    // ── 생명주기 ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 자동으로 트럭 루트를 찾는다
        if (truckRoot == null)
        {
            TruckMovement truck = FindFirstObjectByType<TruckMovement>();
            if (truck != null) truckRoot = truck.transform;
        }

        // 오버레이 자동 생성
        if (overlayImage == null)
        {
            CreateOverlay();
        }
        else
        {
            overlayImage.gameObject.SetActive(false);
            overlayImage.raycastTarget = false;
        }
    }

    private void CreateOverlay()
    {
        // 씬에서 기존 Canvas가 있다면 그 하위에 오버레이를 배치하도록 시도하고, 없다면 새로 생성
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("FocusOverlayCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        GameObject overlayGo = new GameObject("PlacementFocusOverlay");
        overlayGo.transform.SetParent(canvas.transform, false);

        overlayImage = overlayGo.AddComponent<Image>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = false; // Add this line so UI overlay doesn't block OnMouseDown


        // RectTransform을 화면 꽉 차게 설정 (Stretch-Stretch)
        RectTransform rect = overlayImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        overlayGo.SetActive(false);
    }

    private void Update()
    {
        // 패널이 열려 있고 애니메이션이 끝났을 때 바깥 클릭 감지 (Defense/Maintenance 공통)
        if (!isOpen || isAnimating) return;
        
        // OpenPanel()이 호출된 바로 그 프레임에는 검사를 건너뜁니다.
        // (OnMouseDown에서 패널을 열자마자 Update의 클릭 감지가 같은 프레임에 실행되어 즉시 닫히는 버그 방지)
        if (openedThisFrame)
        {
            openedThisFrame = false;
            return;
        }
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        float depth = Mathf.Abs(cam.transform.position.z);
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));

        // [1] 인벤토리 영역 내부를 클릭한 경우 패널 유지 (바깥 클릭으로 간주하지 않음)
        if (BlockInventory.Instance != null)
        {
            // 슬롯은 inventoryOrigin을 중심으로 좌우 대칭 배치됨
            // startPos = origin - (maxSlots-1)*spacing*0.5
            // endPos   = origin + (maxSlots-1)*spacing*0.5
            // → bounds 중심도 inventoryOrigin, 너비는 (maxSlots-1)*spacing + slotSpacing(여유) 로 계산
            Vector3 invWorldOrigin = BlockInventory.Instance.transform.TransformPoint(
                BlockInventory.Instance.InventoryOrigin);
            float totalSlotSpan = (BlockInventory.Instance.MaxSlots - 1) * BlockInventory.Instance.SlotSpacing;
            float boundsWidth   = totalSlotSpan + BlockInventory.Instance.SlotSpacing * 2f; // 양쪽 여유 포함

            Bounds invBounds = new Bounds(invWorldOrigin, new Vector3(boundsWidth, 3.5f, 10f));
            if (invBounds.Contains(worldPos))
            {
                return;
            }

            // 개별 슬롯 SpriteRenderer 바운더리 검사
            foreach (var slot in BlockInventory.Instance.GetComponentsInChildren<SpriteRenderer>())
            {
                if (slot != null && slot.bounds.Contains(worldPos))
                {
                    return;
                }
            }
        }


        // [2] 클릭이 GridCell 또는 TurretBlock(ChildCollider 포함) 위에 있으면 패널 유지
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hit.GetComponentInParent<GridCell>() != null) return;
            if (hit.GetComponentInParent<TurretBlock>() != null) return;
            if (hit.GetComponent<TurretBlockChildCollider>() != null) return;
        }

        // 바깥 클릭: 패널 닫기
        ClosePanel();
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public Vector3 GetUnzoomedGridLossyScale(TruckGrid grid)
    {
        if (truckRoot == null || grid == null || savedTruckLocalScale == Vector3.zero) 
            return grid != null ? grid.transform.lossyScale : Vector3.one;

        Vector3 currentTruckScale = truckRoot.localScale;
        Vector3 currentGridLossy = grid.transform.lossyScale;

        return new Vector3(
            currentTruckScale.x == 0 ? 0 : currentGridLossy.x / currentTruckScale.x * savedTruckLocalScale.x,
            currentTruckScale.y == 0 ? 0 : currentGridLossy.y / currentTruckScale.y * savedTruckLocalScale.y,
            currentTruckScale.z == 0 ? 0 : currentGridLossy.z / currentTruckScale.z * savedTruckLocalScale.z
        );
    }

    /// <summary>블록을 집을 때 호출. 패널을 열고 트럭을 중앙으로 당겨 확대한다.</summary>
    public void OpenPanel()
    {
        if (isOpen || truckRoot == null) return;

        // 현재 상태 저장
        savedTruckPosition   = truckRoot.position;
        savedTruckLocalScale = truckRoot.localScale;

        // 트레일러 상태도 저장
        CacheAndSaveTrailers();

        isOpen = true;
        openedThisFrame = true;

        // 오버레이 활성화
        if (overlayImage != null)
            overlayImage.gameObject.SetActive(true);

        // 게임 일시정지
        Time.timeScale = 0f;

        // 열기 애니메이션 시작
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(AnimateFocus(opening: true));
    }

    /// <summary>블록을 내려놓거나 바깥을 클릭할 때 호출. 패널을 닫고 트럭을 원위치로 복원한다.</summary>
    public void ClosePanel()
    {
        if (!isOpen) return;
        isOpen = false;

        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(AnimateFocus(opening: false));
    }

    // ── 내부 로직 ────────────────────────────────────────────────────────────────

    private void CacheAndSaveTrailers()
    {
        cachedTrailers.Clear();
        savedTrailerPositions.Clear();
        savedTrailerLocalScales.Clear();

        TrailerBody[] trailers = FindObjectsByType<TrailerBody>(FindObjectsSortMode.None);
        foreach (var t in trailers)
        {
            if (t == null) continue;
            cachedTrailers.Add(t);
            savedTrailerPositions.Add(t.transform.position);
            savedTrailerLocalScales.Add(t.transform.localScale);
        }
    }

    private IEnumerator AnimateFocus(bool opening)
    {
        isAnimating = true;

        // 트럭
        Vector3 truckStartPos   = truckRoot.position;
        Vector3 truckTargetPos  = opening ? focusTargetPosition : savedTruckPosition;
        Vector3 truckStartScale = truckRoot.localScale;
        Vector3 truckTargetScale = opening
            ? savedTruckLocalScale * focusScale
            : savedTruckLocalScale;

        // 트레일러: 트럭과의 상대적 오프셋을 유지하며 함께 이동/스케일
        int trailerCount = cachedTrailers.Count;
        Vector3[] trailerStartPositions = new Vector3[trailerCount];
        Vector3[] trailerStartScales    = new Vector3[trailerCount];
        Vector3[] trailerTargetPositions = new Vector3[trailerCount];
        Vector3[] trailerTargetScales    = new Vector3[trailerCount];

        for (int i = 0; i < trailerCount; i++)
        {
            if (cachedTrailers[i] == null) continue;

            trailerStartPositions[i] = cachedTrailers[i].transform.position;
            trailerStartScales[i]    = cachedTrailers[i].transform.localScale;

            if (opening)
            {
                // 트럭 중심으로부터의 오프셋을 focusScale 배율로 재계산한 뒤 targetPosition 기준 배치
                Vector3 offsetFromTruck = cachedTrailers[i].transform.position - savedTruckPosition;
                trailerTargetPositions[i] = focusTargetPosition + offsetFromTruck * focusScale;
                trailerTargetScales[i]    = savedTrailerLocalScales[i] * focusScale;
            }
            else
            {
                trailerTargetPositions[i] = savedTrailerPositions[i];
                trailerTargetScales[i]    = savedTrailerLocalScales[i];
            }
        }

        // SmoothStep 보간 (unscaledDeltaTime: timeScale=0 에서도 동작)
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animationDuration));

            truckRoot.position   = Vector3.Lerp(truckStartPos,   truckTargetPos,   t);
            truckRoot.localScale = Vector3.Lerp(truckStartScale, truckTargetScale, t);

            for (int i = 0; i < trailerCount; i++)
            {
                if (cachedTrailers[i] == null) continue;
                cachedTrailers[i].transform.position   = Vector3.Lerp(trailerStartPositions[i], trailerTargetPositions[i], t);
                cachedTrailers[i].transform.localScale = Vector3.Lerp(trailerStartScales[i],    trailerTargetScales[i],    t);
            }

            yield return null;
        }

        // 최종 스냅
        truckRoot.position   = truckTargetPos;
        truckRoot.localScale = truckTargetScale;
        for (int i = 0; i < trailerCount; i++)
        {
            if (cachedTrailers[i] == null) continue;
            cachedTrailers[i].transform.position   = trailerTargetPositions[i];
            cachedTrailers[i].transform.localScale = trailerTargetScales[i];
        }

        if (opening)
        {
            // 열리고 난 뒤: 드래그 중인 블록의 스케일을 확대된 Grid에 맞게 재조정
            if (TurretBlock.DraggedBlock != null)
            {
                TurretBlock.DraggedBlock.RecalibrateAfterFocusChange();
            }

            // 인벤토리에 남은 블록들의 크기/위치도 재정렬
            BlockInventory.Instance?.RefreshAllBlockPositions();
        }
        else
        {
            // 닫히고 난 뒤: 타임스케일 복원 → 오버레이 비활성화 → 블록 스케일 재조정
            Time.timeScale = 1f;

            if (overlayImage != null)
                overlayImage.gameObject.SetActive(false);

            if (TurretBlock.DraggedBlock != null)
            {
                TurretBlock.DraggedBlock.RecalibrateAfterFocusChange();
            }

            // 인벤토리에 남은 블록들의 크기/위치도 재정렬
            BlockInventory.Instance?.RefreshAllBlockPositions();

            // 트레일러 TrailerFollow virtualPosition 재동기화 (스냅 튐 방지)
            TrailerFollow[] follows = FindObjectsByType<TrailerFollow>(FindObjectsSortMode.None);
            foreach (var f in follows)
            {
                f.AlignInstantly();
            }
        }

        isAnimating = false;
    }
}
