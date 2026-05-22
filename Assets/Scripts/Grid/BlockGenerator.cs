using System.Collections.Generic;
using UnityEngine;

public class BlockGenerator : MonoBehaviour
{
    public static BlockGenerator Instance { get; private set; }

    [SerializeField] private BlockGradeSettingsSO gradeSettings;

    private void Awake()
    {
        Instance = this;
    }

    public TurretBlock GenerateRandomBlock(Vector3 position, Transform parent = null)
    {
        if (gradeSettings == null) return null;
        return GenerateRandomBlockWithSettings(gradeSettings, gradeSettings.turretDatabase, position, parent);
    }

    public TurretBlock GenerateRandomBlockWithSettings(BlockGradeSettingsSO settings, TurretDatabaseSO database, Vector3 position, Transform parent = null)
    {
        if (settings == null || settings.grades.Count == 0 || database == null || database.allTurrets.Count == 0)
        {
            Debug.LogError("BlockGenerator: Settings or Turret Database not configured.");
            return null;
        }

        // Select random grade based on settings
        float totalProb = 0f;
        foreach (var g in settings.grades) totalProb += g.dropProbability;

        float roll = Random.Range(0f, totalProb);
        float current = 0f;
        GradeSetting selectedGrade = settings.grades[0];

        foreach (var g in settings.grades)
        {
            current += g.dropProbability;
            if (roll <= current)
            {
                selectedGrade = g;
                break;
            }
        }

        return GenerateBlock(selectedGrade, database, position, parent);
    }

    private TurretBlock GenerateBlock(GradeSetting grade, TurretDatabaseSO database, Vector3 position, Transform parent)
    {
        // 1. Create a new root GameObject for the block dynamically
        GameObject blockGo = new GameObject("GeneratedTurretBlock");
        blockGo.transform.position = position;
        if (parent != null)
        {
            blockGo.transform.SetParent(parent);
        }

        // Adding TurretBlock automatically adds BoxCollider2D (due to RequireComponent)
        TurretBlock blockObj = blockGo.AddComponent<TurretBlock>();

        int N = Random.Range(grade.minSize, grade.maxSize + 1);
        int M = Random.Range(grade.minTurrets, grade.maxTurrets + 1);
        
        // Ensure we don't try to place more turrets than available cells
        M = Mathf.Min(M, N * N);

        List<TurretInstance> instances = new List<TurretInstance>();
        HashSet<Vector2Int> selectedCells = new HashSet<Vector2Int>();

        if (M > 0)
        {
            // Polyomino logic: choose initial seed cell at the grid center
            Vector2Int startCell = new Vector2Int(N / 2, N / 2);
            selectedCells.Add(startCell);

            // Spawn subsequent M-1 cells orthogonally adjacent to existing ones
            for (int i = 1; i < M; i++)
            {
                List<Vector2Int> candidates = new List<Vector2Int>();
                foreach (var cell in selectedCells)
                {
                    Vector2Int[] neighbors = {
                        new Vector2Int(cell.x + 1, cell.y),
                        new Vector2Int(cell.x - 1, cell.y),
                        new Vector2Int(cell.x, cell.y + 1),
                        new Vector2Int(cell.x, cell.y - 1)
                    };

                    foreach (var n in neighbors)
                    {
                        if (n.x >= 0 && n.x < N && n.y >= 0 && n.y < N)
                        {
                            if (!selectedCells.Contains(n))
                            {
                                candidates.Add(n);
                            }
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
                    selectedCells.Add(chosen);
                }
                else
                {
                    // Fallback to random unoccupied coordinate if completely blocked
                    List<Vector2Int> fallbackCandidates = new List<Vector2Int>();
                    for (int x = 0; x < N; x++)
                    {
                        for (int y = 0; y < N; y++)
                        {
                            Vector2Int p = new Vector2Int(x, y);
                            if (!selectedCells.Contains(p)) fallbackCandidates.Add(p);
                        }
                    }
                    if (fallbackCandidates.Count > 0)
                    {
                        selectedCells.Add(fallbackCandidates[Random.Range(0, fallbackCandidates.Count)]);
                    }
                }
            }
        }

        // Normalize coordinates to shift the bounding box starting at (0, 0)
        int minX = int.MaxValue, minY = int.MaxValue;
        foreach (var cell in selectedCells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.y < minY) minY = cell.y;
        }

        List<Vector2Int> normalizedCells = new List<Vector2Int>();
        foreach (var cell in selectedCells)
        {
            normalizedCells.Add(new Vector2Int(cell.x - minX, cell.y - minY));
        }

        // Compute actual optimized bounds instead of N * N grid size to eliminate empty padding
        int maxX = 0, maxY = 0;
        foreach (var cell in normalizedCells)
        {
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }
        int actualGridSize = Mathf.Max(maxX, maxY) + 1;

        foreach (var pos in normalizedCells)
        {
            TurretDataSO randomTurretData = database.GetRandomTurret();
            int randomLevel = Random.Range(grade.minLevel, grade.maxLevel + 1);

            TurretInstance inst = new TurretInstance
            {
                localPosition = pos,
                data = randomTurretData,
                level = randomLevel
            };
            instances.Add(inst);
        }

        // 3. Initialize block
        blockObj.Initialize(actualGridSize, instances, grade.gradeColor);

        return blockObj;
    }
}
