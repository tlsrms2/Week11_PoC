using System.Collections.Generic;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [Header("Scenarios")]
    [SerializeField] private List<WaveDataSO> waveScenarios = new List<WaveDataSO>();

    [Header("Enemy References")]
    [SerializeField] private Transform enemyParent;
    [SerializeField] private Transform enemyTarget;

    [Header("Rewards & Droppings")]
    [SerializeField] private Transform droppedBlockParent;

    [Header("Spawn Settings")]
    [SerializeField] private bool useAreaSpawn = true;
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(12f, 2f);
    [SerializeField] private Vector2 spawnAreaOffset = Vector2.zero;
    [SerializeField] private bool spawnOnlyDuringDefense = true;
    [SerializeField] private bool resetWaveOnDefenseStart = true;
    [SerializeField] private bool clearEnemiesOnMaintenance = false;

    [Header("Completion")]
    [SerializeField] private bool stopSpawningWhenTimerEnds = true;
    [SerializeField] private GamePhaseManager phaseManager;

    private readonly List<Enemy> spawnedEnemies = new List<Enemy>();
    private readonly List<EnemyDataSO> currentWaveSpawnList = new List<EnemyDataSO>();

    private GamePhase previousPhase;
    private float spawnTimer;
    private float currentRemainingTime;
    private int spawnedCount;
    private bool waveRunning;
    private bool waveCompleted;
    private int currentWaveLevel = 1;

    // Runtime overridden wave stats
    private float spawnInterval = 1.5f;
    private int maxAliveEnemies = 8;
    private float defenseDuration = 30f;

    // Burst spawn runtime variables
    private bool useBurstSpawn = false;
    private float burstInterval = 10f;
    private int burstCount = 15;
    private float burstTimer;

    public int SpawnedCount => spawnedCount;
    public int AliveCount => CountAliveEnemies();
    public bool IsWaveRunning => waveRunning;
    public bool IsWaveComplete => waveCompleted;
    public float RemainingDefenseTime => waveRunning ? Mathf.Max(0f, currentRemainingTime) : 0f;
    public float DefenseDuration => defenseDuration;
    public int CurrentWaveLevel => currentWaveLevel;
    public List<WaveDataSO> WaveScenarios => waveScenarios;

    private void Awake()
    {
        previousPhase = GamePhaseManager.ActivePhase;
        ValidateSettings();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    private void ValidateSettings()
    {
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        maxAliveEnemies = Mathf.Max(1, maxAliveEnemies);
        defenseDuration = Mathf.Max(1f, defenseDuration);
    }

    private void Update()
    {
        HandlePhaseChange();

        if (spawnOnlyDuringDefense && !GamePhaseManager.IsDefense)
        {
            return;
        }

        if (!waveRunning || waveCompleted)
        {
            return;
        }

        if (ShouldCompleteWave())
        {
            CompleteWave();
            return;
        }

        currentRemainingTime -= Time.deltaTime;

        if (ShouldStopSpawning() || CountAliveEnemies() >= maxAliveEnemies)
        {
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnEnemy();
            spawnTimer = spawnInterval;
        }

        if (useBurstSpawn)
        {
            burstTimer -= Time.deltaTime;
            if (burstTimer <= 0f)
            {
                TriggerBurstSpawn();
                burstTimer = burstInterval;
            }
        }
    }

    [ContextMenu("Reset Wave")]
    public void ResetWave()
    {
        spawnedCount = 0;
        waveCompleted = false;

        // Verify if we have valid scenarios
        if (waveScenarios == null || waveScenarios.Count == 0)
        {
            Debug.LogWarning("No Wave Scenarios configured in WaveSpawner!");
            waveRunning = false;
            return;
        }

        // If level exceeded scenario bounds, trigger game clear immediately
        if (currentWaveLevel > waveScenarios.Count)
        {
            TriggerGameClear();
            return;
        }

        // Load current wave settings
        WaveDataSO currentWaveData = waveScenarios[currentWaveLevel - 1];
        if (currentWaveData == null)
        {
            Debug.LogError($"WaveDataSO at index {currentWaveLevel - 1} is null!");
            waveRunning = false;
            return;
        }

        // Apply stats override
        defenseDuration = currentWaveData.defenseDuration;
        spawnInterval = currentWaveData.spawnInterval;
        maxAliveEnemies = currentWaveData.maxAliveEnemies;

        // Apply burst spawn stats
        useBurstSpawn = currentWaveData.useBurstSpawn;
        burstInterval = currentWaveData.burstInterval;
        burstCount = currentWaveData.burstCount;

        // Auto adjust maxAliveEnemies for burst spawning to guarantee massive swarms are not truncated
        if (useBurstSpawn && maxAliveEnemies < burstCount)
        {
            maxAliveEnemies = Mathf.Max(maxAliveEnemies, burstCount);
            Debug.Log($"WaveSpawner: Auto-adjusting maxAliveEnemies to {maxAliveEnemies} to accommodate burst spawning.");
        }

        // Build spawn list (weight based pooling)
        currentWaveSpawnList.Clear();
        foreach (var entry in currentWaveData.enemiesToSpawn)
        {
            if (entry.enemyData == null) continue;
            // weight 만큼 리스트에 집어넣어 뽑기 확률(가중치)를 구현합니다.
            for (int i = 0; i < entry.weight; i++)
            {
                currentWaveSpawnList.Add(entry.enemyData);
            }
        }

        currentRemainingTime = defenseDuration;
        spawnTimer = 0f;
        burstTimer = 0f;
        waveRunning = true;

        Debug.Log($"Starting Wave {currentWaveLevel}: '{currentWaveData.waveName}'. Duration: {defenseDuration}s");
    }

    // 시간 버티기 방식이므로 리스트 셔플 대신 스폰 시 Random 뽑기를 사용합니다.

    [ContextMenu("Stop All Enemies")]
    public void StopAllEnemies()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = spawnedEnemies[i];
            if (enemy != null)
            {
                enemy.StopActions();
            }
        }
    }

    [ContextMenu("Clear Spawned Enemies")]
    public void ClearSpawnedEnemies()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = spawnedEnemies[i];
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }

        spawnedEnemies.Clear();
    }

    public Enemy SpawnEnemy()
    {
        if (ShouldStopSpawning() || currentWaveSpawnList.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, currentWaveSpawnList.Count);
        EnemyDataSO enemyData = currentWaveSpawnList[randomIndex];
        if (enemyData == null)
        {
            return null;
        }

        Vector3 spawnPosition = GetNextSpawnPosition();
        Enemy enemy = CreateEnemy(enemyData, spawnPosition);
        
        // Configure enemy from scriptable object
        enemy.Configure(enemyData, enemyTarget, droppedBlockParent);

        spawnedEnemies.Add(enemy);
        spawnedCount++;
        return enemy;
    }

    private void TriggerBurstSpawn()
    {
        if (ShouldStopSpawning() || currentWaveSpawnList.Count == 0)
        {
            return;
        }

        // Determine how many to spawn in this burst
        int toSpawn = burstCount;
        
        // Check maxAliveEnemies limit
        int aliveCount = CountAliveEnemies();
        if (aliveCount + toSpawn > maxAliveEnemies)
        {
            toSpawn = Mathf.Max(0, maxAliveEnemies - aliveCount);
        }

        if (toSpawn <= 0) return;

        // Choose a single point for this burst
        Vector3 burstPosition = GetNextSpawnPosition();

        Debug.Log($"Triggering burst spawn: Spawning {toSpawn} enemies at {burstPosition}");

        for (int i = 0; i < toSpawn; i++)
        {
            int randomIndex = Random.Range(0, currentWaveSpawnList.Count);
            EnemyDataSO enemyData = currentWaveSpawnList[randomIndex];
            if (enemyData != null)
            {
                // Add a tiny random offset so they don't spawn at the exact same float coordinates,
                // allowing physics collision resolution to disperse them smoothly.
                Vector3 offset = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), 0f);
                Enemy enemy = CreateEnemy(enemyData, burstPosition + offset);
                if (enemy != null)
                {
                    enemy.Configure(enemyData, enemyTarget, droppedBlockParent);
                    spawnedEnemies.Add(enemy);
                }
            }
            spawnedCount++;
        }
    }

    private Enemy CreateEnemy(EnemyDataSO enemyData, Vector3 spawnPosition)
    {
        if (enemyData.enemyPrefab != null)
        {
            return Instantiate(enemyData.enemyPrefab, spawnPosition, Quaternion.identity, enemyParent);
        }

        GameObject enemyObject = new GameObject($"Enemy_{enemyData.enemyName}_{spawnedCount + 1:00}");
        enemyObject.transform.SetParent(enemyParent, false);
        enemyObject.transform.position = spawnPosition;
        
        // Add required components
        SpriteRenderer sr = enemyObject.AddComponent<SpriteRenderer>();
        // Add basic collider
        enemyObject.AddComponent<BoxCollider2D>();
        
        return enemyObject.AddComponent<Enemy>();
    }

    private Vector3 GetNextSpawnPosition()
    {
        if (useAreaSpawn)
        {
            Vector3 center = transform.position + (Vector3)spawnAreaOffset;
            float halfX = spawnAreaSize.x * 0.5f;
            float halfY = spawnAreaSize.y * 0.5f;

            // Pick one of the 4 border lines (0: Top, 1: Bottom, 2: Left, 3: Right)
            int edgeIndex = Random.Range(0, 4);
            float spawnX = 0f;
            float spawnY = 0f;

            switch (edgeIndex)
            {
                case 0: // Top line segment
                    spawnX = Random.Range(-halfX, halfX);
                    spawnY = halfY;
                    break;
                case 1: // Bottom line segment
                    spawnX = Random.Range(-halfX, halfX);
                    spawnY = -halfY;
                    break;
                case 2: // Left line segment
                    spawnX = -halfX;
                    spawnY = Random.Range(-halfY, halfY);
                    break;
                case 3: // Right line segment
                    spawnX = halfX;
                    spawnY = Random.Range(-halfY, halfY);
                    break;
            }

            return new Vector3(center.x + spawnX, center.y + spawnY, transform.position.z);
        }

        return transform.position;
    }

    private void HandlePhaseChange()
    {
        if (previousPhase == GamePhaseManager.ActivePhase)
        {
            return;
        }

        previousPhase = GamePhaseManager.ActivePhase;

        if (GamePhaseManager.IsDefense && resetWaveOnDefenseStart)
        {
            ResetWave();
        }
        else if (GamePhaseManager.IsMaintenance && clearEnemiesOnMaintenance)
        {
            waveRunning = false;
            ClearSpawnedEnemies();
        }
        else if (!GamePhaseManager.IsDefense)
        {
            waveRunning = false;
        }
    }

    private bool ShouldStopSpawning()
    {
        return stopSpawningWhenTimerEnds && currentRemainingTime <= 0f;
    }

    private bool ShouldCompleteWave()
    {
        return currentRemainingTime <= 0f;
    }

    private void CompleteWave()
    {
        if (waveCompleted)
        {
            return;
        }

        waveRunning = false;
        waveCompleted = true;
        
        Debug.Log($"Wave {currentWaveLevel} completed!");

        // 모든 적의 행동 멈춤
        foreach (var enemy in Enemy.ActiveEnemies)
        {
            if (enemy != null && enemy.IsAlive)
            {
                enemy.StopActions();
            }
        }

        // 2초 딜레이 후 클리어 연출
        StartCoroutine(ClearDelayRoutine());
    }

    private System.Collections.IEnumerator ClearDelayRoutine()
    {
        yield return new WaitForSeconds(2.0f);

        // Try transitioning or clear
        if (currentWaveLevel >= waveScenarios.Count)
        {
            TriggerGameClear();
            yield break;
        }

        // Trigger cinematics and automatic phase transition via GameFlowManager
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnStageCleared();
        }
        else
        {
            // Fallback if GameFlowManager is missing
            currentWaveLevel++;
            GamePhaseManager targetPhaseManager = phaseManager != null ? phaseManager : FindFirstObjectByType<GamePhaseManager>();
            if (targetPhaseManager != null)
            {
                targetPhaseManager.SetPhase(GamePhase.Maintenance);
            }
        }
    }

    public void PrepareNextWave()
    {
        if (currentWaveLevel < waveScenarios.Count)
        {
            currentWaveLevel++;
            Debug.Log($"WaveSpawner: Prepared next wave level {currentWaveLevel}");
        }
    }

    private void TriggerGameClear()
    {
        Debug.Log("ALL WAVES COMPLETED! Game Clear!");
        waveRunning = false;
        waveCompleted = true;

        GamePhaseManager targetPhaseManager = phaseManager != null ? phaseManager : FindFirstObjectByType<GamePhaseManager>();
        if (targetPhaseManager != null)
        {
            targetPhaseManager.SetPhase(GamePhase.Victory);
        }
    }

    private int CountAliveEnemies()
    {
        int aliveCount = 0;
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = spawnedEnemies[i];
            if (enemy == null)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }

            if (enemy.isActiveAndEnabled && enemy.IsAlive)
            {
                aliveCount++;
            }
        }

        return aliveCount;
    }

    private void OnDrawGizmos()
    {
        if (useAreaSpawn)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 center = transform.position + (Vector3)spawnAreaOffset;
            Gizmos.DrawWireCube(center, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (useAreaSpawn)
        {
            Gizmos.color = Color.red;
            Vector3 center = transform.position + (Vector3)spawnAreaOffset;
            Gizmos.DrawWireCube(center, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
        }
    }
}

