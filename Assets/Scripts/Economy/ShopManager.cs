using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("Shop Settings")]
    [SerializeField] private int blockPurchaseCost = 15;
    [SerializeField] private int blockSellPrice = 5;
    [SerializeField] private TurretDatabaseSO turretDatabase;
    [SerializeField] private BlockGradeSettingsSO blockGradeSettings;
    [SerializeField] private Transform spawnPoint;

    public int BlockSellPrice => blockSellPrice;

    // 프로퍼티를 통해 웨이브당 비용이 10씩 자동 증가하도록 처리
    public int BlockPurchaseCost
    {
        get
        {
            int extraCost = 0;
            WaveSpawner spawner = FindFirstObjectByType<WaveSpawner>();
            if (spawner != null)
            {
                extraCost = Mathf.Max(0, spawner.CurrentWaveLevel - 1) * 10;
            }
            return blockPurchaseCost + extraCost;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private float lastPurchaseTime = -1f;
    private const float purchaseCooldown = 0.05f;

    public bool CanPurchaseBlock => CurrencyWallet.Instance != null 
        && CurrencyWallet.Instance.CurrentCurrency >= BlockPurchaseCost 
        && BlockInventory.Instance != null 
        && BlockInventory.Instance.HasSpace;

    public void PurchaseBlock()
    {
        // Prevent rapid duplicate clicks (debounce triggers within cooldown window)
        if (Time.time - lastPurchaseTime < purchaseCooldown) return;

        if (!CanPurchaseBlock) return;

        if (CurrencyWallet.Instance.TrySpend(BlockPurchaseCost))
        {
            lastPurchaseTime = Time.time;
            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            TurretBlock newBlock = null;
            
            if (BlockGenerator.Instance != null)
            {
                newBlock = BlockGenerator.Instance.GenerateRandomBlockWithSettings(blockGradeSettings, turretDatabase, pos);
            }

            if (newBlock == null) return;

            // Try to add directly to inventory if space exists
            if (BlockInventory.Instance != null && BlockInventory.Instance.HasSpace)
            {
                BlockInventory.Instance.TryAddBlock(newBlock);
            }
        }
    }

    public void SellBlock(TurretBlock block)
    {
        if (block == null) return;
        
        // Remove from inventory or grid
        if (block.IsInInventory && BlockInventory.Instance != null)
        {
            BlockInventory.Instance.RemoveBlock(block);
        }
        else if (block.IsPlaced)
        {
            // Clear cells?
            // Actually TurretBlock doesn't expose ClearOccupiedCells publicly, 
            // but we can just destroy the block and the GridCell will know. Wait, GridCell needs to be notified.
            for(int i = 0; i < block.OccupiedCells.Count; i++)
            {
                block.OccupiedCells[i].ClearPlacedBlock(block);
            }
        }

        CurrencyWallet.Instance?.Add(blockSellPrice);
        Destroy(block.gameObject);
    }
}
