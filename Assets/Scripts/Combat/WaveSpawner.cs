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

    // Burst spawn runtime variables (Legacy)
    private bool useBurstSpawn = false;
    private float burstInterval = 10f;
    private int burstCount = 15;
    private float burstTimer;

    // Surge spawn runtime variables (New Swarm System)
    private bool useSurgeSpawn = false;
    private float surgeInterval = 8f;
    private int surgeDirections = 6;
    private int surgeSpawnPerDirection = 4;
    private float surgeClusterRadius = 0.5f;
    private float surgeTimer;

    private float spawnRadiusMin = 8f;
    private float spawnRadiusMax = 12f;

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

        // Real-time synchronization of wave balance parameters from WaveDataSO for instant balancing feedback
        if (waveScenarios != null && currentWaveLevel >= 1 && currentWaveLevel <= waveScenarios.Count)
        {
            WaveDataSO currentWaveData = waveScenarios[currentWaveLevel - 1];
            if (currentWaveData != null)
            {
                spawnRadiusMin = currentWaveData.spawnRadiusMin;
                spawnRadiusMax = currentWaveData.spawnRadiusMax;
                useSurgeSpawn = currentWaveData.useSurgeSpawn;
                surgeInterval = currentWaveData.surgeInterval;
                surgeDirections = currentWaveData.surgeDirections;
                surgeSpawnPerDirection = currentWaveData.surgeSpawnPerDirection;
                surgeClusterRadius = currentWaveData.surgeClusterRadius;
                
                useBurstSpawn = currentWaveData.useBurstSpawn;
                burstInterval = currentWaveData.burstInterval;
                burstCount = currentWaveData.burstCount;
                
                spawnInterval = currentWaveData.spawnInterval;
            }
        }

        // 맵 스크롤(트랜지션)이 완전히 끝나야 웨이브 타이머 및 스폰 시작
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.IsTransitioning)
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

        if (useSurgeSpawn)
        {
            surgeTimer -= Time.deltaTime;
            if (surgeTimer <= 0f)
            {
                TriggerSurgeSpawn();
                surgeTimer = surgeInterval;
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

        // Apply surge spawn stats
        useSurgeSpawn = currentWaveData.useSurgeSpawn;
        surgeInterval = currentWaveData.surgeInterval;
        surgeDirections = currentWaveData.surgeDirections;
        surgeSpawnPerDirection = currentWaveData.surgeSpawnPerDirection;
        surgeClusterRadius = currentWaveData.surgeClusterRadius;
        spawnRadiusMin = currentWaveData.spawnRadiusMin;
        spawnRadiusMax = currentWaveData.spawnRadiusMax;

        // Auto adjust maxAliveEnemies to guarantee massive swarms are not truncated
        int requiredMax = 0;
        if (useSurgeSpawn) requiredMax = surgeDirections * surgeSpawnPerDirection * 2;
        else if (useBurstSpawn) requiredMax = burstCount * 2;
        
        if (requiredMax > maxAliveEnemies)
        {
            maxAliveEnemies = requiredMax;
            Debug.Log($"WaveSpawner: Auto-adjusting maxAliveEnemies to {maxAliveEnemies} to accommodate massive spawning.");
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
        surgeTimer = 0f;
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

    private void TriggerSurgeSpawn()
    {
        if (ShouldStopSpawning() || currentWaveSpawnList.Count == 0) return;

        int toSpawn = surgeDirections * surgeSpawnPerDirection;
        int aliveCount = CountAliveEnemies();
        if (aliveCount + toSpawn > maxAliveEnemies)
        {
            toSpawn = Mathf.Max(0, maxAliveEnemies - aliveCount);
        }
        if (toSpawn <= 0) return;

        Vector3 centerPos = TruckBody.Instance != null ? TruckBody.Instance.transform.position : transform.position;
        float angleStep = 360f / surgeDirections;
        float randomAngleOffset = Random.Range(0f, 360f); // 매 서지마다 각도를 회전시켜 줌

        Debug.Log($"Triggering SURGE spawn: {toSpawn} enemies from {surgeDirections} directions.");

        // Fix surge radius to a single random distance within circular range for this exact surge event
        // to ensure it perfectly aligns with the unified circular boundary.
        float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);

        int spawnedThisSurge = 0;
        for (int dir = 0; dir < surgeDirections; dir++)
        {
            if (spawnedThisSurge >= toSpawn) break;

            float currentAngle = (dir * angleStep + randomAngleOffset) * Mathf.Deg2Rad;
            Vector3 baseDirPos = centerPos + new Vector3(Mathf.Cos(currentAngle) * radius, Mathf.Sin(currentAngle) * radius, 0f);

            for (int i = 0; i < surgeSpawnPerDirection; i++)
            {
                if (spawnedThisSurge >= toSpawn) break;

                int randomIndex = Random.Range(0, currentWaveSpawnList.Count);
                EnemyDataSO enemyData = currentWaveSpawnList[randomIndex];
                if (enemyData != null)
                {
                    // 클러스터 오프셋
                    Vector2 randomOffset = Random.insideUnitCircle * surgeClusterRadius;
                    Vector3 spawnPos = baseDirPos + new Vector3(randomOffset.x, randomOffset.y, 0f);
                    
                    Enemy enemy = CreateEnemy(enemyData, spawnPos);
                    if (enemy != null)
                    {
                        enemy.Configure(enemyData, enemyTarget, droppedBlockParent);
                        spawnedEnemies.Add(enemy);
                    }
                }
                spawnedCount++;
                spawnedThisSurge++;
            }
        }
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
        Vector3 centerPos = TruckBody.Instance != null ? TruckBody.Instance.transform.position : transform.position;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);

        return centerPos + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
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
        if (stopSpawningWhenTimerEnds)
        {
            // 웨이브 종료 조건: 시간이 다 끝났고, 남은 살아있는 적이 모두 죽었을 때 클리어
            return currentRemainingTime <= 0f && CountAliveEnemies() <= 0;
        }
        
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
        DrawWaveGizmos(new Color(1f, 0f, 0f, 0.3f));
    }

    private void OnDrawGizmosSelected()
    {
        DrawWaveGizmos(Color.red);
    }

    private void DrawWaveGizmos(Color color)
    {
        Vector3 centerPos = transform.position;
        if (Application.isPlaying && TruckBody.Instance != null)
        {
            centerPos = TruckBody.Instance.transform.position;
        }
        else
        {
            // Try to find truck in editor time if available for better visualization
            TruckBody editorTruck = FindFirstObjectByType<TruckBody>();
            if (editorTruck != null) centerPos = editorTruck.transform.position;
        }

        float rMin = spawnRadiusMin;
        float rMax = spawnRadiusMax;

        // 에디터 모드일 경우 WaveDataSO에서 값을 실시간으로 읽어오기 위함
        if (!Application.isPlaying && waveScenarios != null && waveScenarios.Count > 0)
        {
            int index = Mathf.Clamp(currentWaveLevel - 1, 0, waveScenarios.Count - 1);
            if (waveScenarios[index] != null)
            {
                rMin = waveScenarios[index].spawnRadiusMin;
                rMax = waveScenarios[index].spawnRadiusMax;
            }
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(centerPos, rMin);
        Gizmos.DrawWireSphere(centerPos, rMax);
    }
}

