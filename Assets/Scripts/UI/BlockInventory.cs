using System.Collections.Generic;
using UnityEngine;

public class BlockInventory : MonoBehaviour
{
    public static BlockInventory Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int maxSlots = 5;
    [SerializeField] private Vector3 inventoryOrigin = new Vector3(0f, -4f, 0f);
    [SerializeField] private float slotSpacing = 1.5f;
    [SerializeField] private SpriteRenderer slotSpritePrefab;

    private readonly List<TurretBlock> storedBlocks = new List<TurretBlock>();
    private readonly List<SpriteRenderer> instantiatedSlots = new List<SpriteRenderer>();

    public IReadOnlyList<TurretBlock> StoredBlocks => storedBlocks;
    public bool HasSpace => (storedBlocks.Count + (TurretBlock.DraggedBlock != null ? 1 : 0)) < (maxSlots - 1);
    public bool IsInventoryFull => !HasSpace;
    public int MaxSlots => maxSlots;
    public Vector3 InventoryOrigin => inventoryOrigin;
    public float SlotSpacing => slotSpacing;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        CreateSlots();
    }

    private void CreateSlots()
    {
        if (slotSpritePrefab == null)
        {
            Debug.LogWarning("BlockInventory: slotSpritePrefab is not assigned.");
            return;
        }

        foreach (var slot in instantiatedSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        instantiatedSlots.Clear();

        float totalWidth = (maxSlots - 1) * slotSpacing;
        Vector3 startPos = inventoryOrigin - new Vector3(totalWidth * 0.5f, 0f, 0f);

        for (int i = 0; i < maxSlots; i++)
        {
            Vector3 localSlotPos = startPos + new Vector3(i * slotSpacing, 0f, 0f);
            SpriteRenderer slotInstance = Instantiate(slotSpritePrefab, transform);
            slotInstance.transform.localPosition = localSlotPos;
            slotInstance.sortingOrder = -5;

            // 맨 오른쪽 마지막 칸은 교환(EXCHANGE) 칸으로 설정
            if (i == maxSlots - 1)
            {
                // 세련된 반투명 보라색(Indigo)으로 변경
                slotInstance.color = new Color(0.6f, 0.3f, 0.9f, 0.8f);

                // "EXCHANGE" 텍스트 생성
                GameObject exchangeTextGo = new GameObject("ExchangeText");
                exchangeTextGo.transform.SetParent(slotInstance.transform);
                exchangeTextGo.transform.localPosition = Vector3.zero;

                TMPro.TextMeshPro tmp = exchangeTextGo.AddComponent<TMPro.TextMeshPro>();
                tmp.text = "EXCHANGE";
                tmp.color = new Color(0.95f, 0.9f, 1f, 0.9f);
                tmp.fontSize = 1.8f; // "EXCHANGE"가 긴 단어이므로 폰트 크기 축소
                tmp.alignment = TMPro.TextAlignmentOptions.Center;

                MeshRenderer mr = exchangeTextGo.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sortingOrder = -4; // 슬롯보다는 위, 블록보다는 아래
                }
            }

            instantiatedSlots.Add(slotInstance);
        }
    }

    public bool TryAddBlock(TurretBlock block)
    {
        if (!HasSpace || storedBlocks.Contains(block))
        {
            return false;
        }

        storedBlocks.Add(block);

        // 1. 인벤토리 flag를 먼저 세팅 (isInInventory = true, rotation 리셋)
        block.SetInInventory(true);

        // 2. 블록을 인벤토리 하위로 reparent
        block.transform.SetParent(transform, worldPositionStays: true);

        // 3. 스케일을 그리드 기준으로 조정한 뒤 위치 배치
        RefreshAllBlockPositions();
        return true;
    }

    public void RemoveBlock(TurretBlock block)
    {
        if (storedBlocks.Remove(block))
        {
            block.SetInInventory(false);
            // 나머지 블록들을 앞으로 당김
            RefreshAllBlockPositions();
        }
    }

    /// <summary>인벤토리 내 모든 블록의 스케일과 위치를 현재 TruckGrid 기준으로 재정렬한다.</summary>
    public void RefreshAllBlockPositions()
    {
        float totalWidth = (maxSlots - 1) * slotSpacing;
        Vector3 startPos = inventoryOrigin - new Vector3(totalWidth * 0.5f, 0f, 0f);

        for (int i = 0; i < storedBlocks.Count; i++)
        {
            var block = storedBlocks[i];
            if (block == null) continue;

            // 스케일을 unzoomed 그리드 크기에 맞게 강제 설정
            block.AdjustScaleToGrid();

            // 슬롯 월드 위치 계산
            Vector3 localSlotPos = startPos + new Vector3(i * slotSpacing, 0f, 0f);
            Vector3 slotWorldPos = transform.TransformPoint(localSlotPos);

            // 블록의 visual center를 슬롯 중심에 맞춰 정렬
            AlignBlockToSlot(block, slotWorldPos);
        }

        // Unity 2021+ 에서 Physics2D.autoSyncTransforms 기본값이 false이므로
        // transform 변경 직후 물리 충돌체 위치를 강제로 동기화한다.
        // 이를 호출하지 않으면 다음 FixedUpdate 전까지 클릭 감지가 이전 위치를 사용해
        // 시각적 위치와 클릭 위치가 어긋나는 버그가 발생한다.
        Physics2D.SyncTransforms();
    }

    // ── Sell & Floating Text Helpers ─────────────────────────────────────────

    /// <summary>마우스 월드 위치가 교환 슬롯 영역 내부(반경 1.0f)에 있는지 감지한다.</summary>
    public bool IsOverExchangeSlot(Vector3 mouseWorldPos)
    {
        if (instantiatedSlots.Count < maxSlots) return false;

        SpriteRenderer exchangeSlot = instantiatedSlots[maxSlots - 1];
        if (exchangeSlot == null) return false;

        Vector2 slotPos = exchangeSlot.transform.position;
        Vector2 mousePos = mouseWorldPos;

        float distance = Vector2.Distance(slotPos, mousePos);
        return distance <= 1.0f;
    }

    /// <summary>블록을 즉시 교환하고, 등급에 따른 레벨의 1x1 포탑 블록을 무작위로 생성하여 인벤토리에 넣는다.</summary>
    public void ExchangeBlock(TurretBlock block)
    {
        if (block == null) return;

        // Exchange 슬롯의 한가운데 좌표를 spawnPos로 설정
        Vector3 spawnPos = block.transform.position;
        if (instantiatedSlots.Count >= maxSlots)
        {
            SpriteRenderer exchangeSlot = instantiatedSlots[maxSlots - 1];
            if (exchangeSlot != null)
            {
                spawnPos = exchangeSlot.transform.position;
            }
        }

        // "EXCHANGE" 텍스트 띄우기 (Cyan 색상)
        CreateFloatingText("EXCHANGE", spawnPos, Color.cyan);

        // 등급에 따른 레벨 결정 (Normal: 1, Rare: 2, Unique: 3, Legend: 4)
        int targetLevel = 1;
        string originalGradeName = block.gradeName;
        if (!string.IsNullOrEmpty(originalGradeName))
        {
            string lowerGrade = originalGradeName.ToLower();
            if (lowerGrade.Contains("normal")) targetLevel = 1;
            else if (lowerGrade.Contains("rare")) targetLevel = 2;
            else if (lowerGrade.Contains("unique")) targetLevel = 3;
            else if (lowerGrade.Contains("legend")) targetLevel = 4;
        }

        // 원래 블록은 인벤토리에서 제거하고 즉시 파괴
        RemoveBlock(block);
        Destroy(block.gameObject);

        // 새로운 등급/레벨의 1x1 랜덤 포탑 블록 생성 (위치는 교환 슬롯 좌표)
        if (BlockGenerator.Instance != null)
        {
            TurretBlock newBlock = BlockGenerator.Instance.GenerateLevel1SingleCellBlock(spawnPos, transform, targetLevel, originalGradeName);
            if (newBlock != null)
            {
                // 인벤토리에 넣기
                bool added = TryAddBlock(newBlock);
                if (!added)
                {
                    // 예외 처리: 만약 실패했다면 파괴되지 않게 예방하거나 드롭 상태로 만듦
                    Debug.LogWarning("ExchangeBlock: Failed to add generated block to inventory.");
                }
            }
        }
    }

    private void CreateFloatingText(string text, Vector3 position, Color color)
    {
        GameObject textGo = new GameObject("FloatingText");
        textGo.transform.position = position;

        TMPro.TextMeshPro tmp = textGo.AddComponent<TMPro.TextMeshPro>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = 7f; // 크기를 기존 8f에서 7f로 1px 감소
        tmp.alignment = TMPro.TextAlignmentOptions.Center;

        MeshRenderer mr = textGo.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 20; // 2D 스프라이트 및 기타 요소보다 항상 위로 정렬
        }

        StartCoroutine(AnimateFloatingText(textGo, tmp));
    }

    private System.Collections.IEnumerator AnimateFloatingText(GameObject go, TMPro.TextMeshPro tmp)
    {
        float duration = 1.2f;
        float elapsed = 0f;
        Vector3 startPos = go.transform.position;
        Color startColor = tmp.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Time.deltaTime 대신 unscaledDeltaTime을 사용하여 시간 정지(패널 활성화) 영향 회피
            float percent = elapsed / duration;

            // 1. 천천히 위로 상승
            go.transform.position = startPos + new Vector3(0f, percent * 1.2f, 0f);

            // 2. 부드럽게 페이드 아웃
            Color newColor = startColor;
            newColor.a = Mathf.Lerp(1f, 0f, percent);
            tmp.color = newColor;

            yield return null;
        }

        Destroy(go);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>블록의 시각적 중심(GetLocalCenter)이 slotWorldPos에 오도록 transform.position을 설정한다.</summary>
    private static void AlignBlockToSlot(TurretBlock block, Vector3 slotWorldPos)
    {
        // GetBoundingBoxCenter()는 인벤토리 콜라이더와 일치하는 기하학적 중심
        Vector3 localCenter = block.GetBoundingBoxCenter();
        // 현재 로컬 center의 월드 좌표
        Vector3 currentWorldCenter = block.transform.TransformPoint(localCenter);
        // 그 차이만큼 이동해서 center가 slotWorldPos에 오게 함
        block.transform.position += (slotWorldPos - currentWorldCenter);
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        float totalWidth = (maxSlots - 1) * slotSpacing;
        Vector3 startPos = inventoryOrigin - new Vector3(totalWidth * 0.5f, 0f, 0f);

        Gizmos.color = Color.yellow;
        for (int i = 0; i < maxSlots; i++)
        {
            Gizmos.DrawWireCube(startPos + new Vector3(i * slotSpacing, 0f, 0f), Vector3.one);
        }

        Gizmos.matrix = originalMatrix;
    }
}
