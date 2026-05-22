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
    public float spawnInterval = 1.5f;
    public int maxAliveEnemies = 8;

    [Header("Burst Spawn Settings")]
    [Tooltip("Whether to spawn enemies in periodic massive bursts instead of one-by-one.")]
    public bool useBurstSpawn = false;
    [Tooltip("Interval in seconds between each massive burst/wave.")]
    public float burstInterval = 10f;
    [Tooltip("Number of enemies to spawn in each burst.")]
    public int burstCount = 15;
    
    [Tooltip("The list of enemies and their spawn counts for this wave.")]
    public List<EnemySpawnEntry> enemiesToSpawn = new List<EnemySpawnEntry>();
}
