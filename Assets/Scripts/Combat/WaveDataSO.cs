using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public struct EnemySpawnEntry
{
    public EnemyDataSO enemyData;
    [FormerlySerializedAs("count")]
    public int weight;
}

[CreateAssetMenu(fileName = "NewWaveData", menuName = "Combat/Wave Data", order = 2)]
public class WaveDataSO : ScriptableObject
{
    public string waveName = "Wave 1";
    public float defenseDuration = 30f;
    public float spawnInterval = 0.5f;
    public int maxAliveEnemies = 40;

    [Header("Circular Spawn Settings")]
    [Tooltip("트럭 중심으로부터 스폰 원형 링의 최소 반경")]
    public float spawnRadiusMin = 8f;
    [Tooltip("트럭 중심으로부터 스폰 원형 링의 최대 반경")]
    public float spawnRadiusMax = 12f;

    [Header("Surge (Multi-Direction) Spawn Settings")]
    [Tooltip("주기적으로 여러 방향에서 동시에 대규모 적을 소환하는 서지 기능 활성화")]
    public bool useSurgeSpawn = true;
    [Tooltip("서지 발동 주기 (초)")]
    public float surgeInterval = 8f;
    [Tooltip("한 번의 서지에서 몇 방향으로 동시 소환할지 (4=사방, 6=6방향, 8=8방향)")]
    [Range(2, 12)]
    public int surgeDirections = 6;
    [Tooltip("서지 1회 발동 시 방향 하나당 소환되는 적 수")]
    [Range(1, 20)]
    public int surgeSpawnPerDirection = 4;
    [Tooltip("각 방향 클러스터 내에서 적의 랜덤 오프셋 반경")]
    public float surgeClusterRadius = 0.5f;

    [Header("Legacy Burst Spawn (deprecated, use Surge instead)")]
    [Tooltip("Whether to spawn enemies in periodic massive bursts instead of one-by-one.")]
    public bool useBurstSpawn = false;
    [Tooltip("Interval in seconds between each massive burst/wave.")]
    public float burstInterval = 10f;
    [Tooltip("Number of enemies to spawn in each burst.")]
    public int burstCount = 15;

    [Tooltip("The list of enemies and their spawn counts for this wave.")]
    public List<EnemySpawnEntry> enemiesToSpawn = new List<EnemySpawnEntry>();
}
