using System.Collections.Generic;
using UnityEngine;

public class BlockGenerator : MonoBehaviour
{
    public static BlockGenerator Instance { get; private set; }

    [SerializeField] private BlockGradeSettingsSO gradeSettings;

    [Header("Smart Block Bias")]
    [Tooltip("생성할 후보 블록 수. 값이 클수록 인접 패턴과 잘 맞는 블록이 선택될 가능성이 높아집니다.")]
    [SerializeField, Range(5, 100)] private int adjacencyCandidateCount = 30;

    [Tooltip("그리드 인접 패턴과 일치할 때마다 추가되는 점수 배수.\n" +
             "1.0 = 약한 보정, 5.0 = 강한 보정, 20.0 = 거의 확정적 유도.")]
    [SerializeField, Range(1f, 20f)] private float adjacencyMatchBiasWeight = 5f;

    private int purchaseCount = 0;
    private bool hasRolledSpecialGradeInFirstFour = false;

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

        // Increment generation count
        purchaseCount++;
        bool forceSpecial = false;
        
        // If only Normal dropped in the first 4 rolls, both 5th and 6th rolls are guaranteed to be special grades
        if ((purchaseCount == 5 || purchaseCount == 6) && !hasRolledSpecialGradeInFirstFour)
        {
            forceSpecial = true;
        }

        GradeSetting selectedGrade = null;

        if (forceSpecial)
        {
            // Filter out "Normal" grade to guarantee a special grade (Rare, Unique, Legend)
            List<GradeSetting> specialGrades = new List<GradeSetting>();
            foreach (var g in settings.grades)
            {
                if (g.gradeName.ToLower() != "normal")
                {
                    specialGrades.Add(g);
                }
            }

            if (specialGrades.Count > 0)
            {
                float totalProb = 0f;
                foreach (var g in specialGrades) totalProb += g.dropProbability;

                float roll = Random.Range(0f, totalProb);
                float current = 0f;
                selectedGrade = specialGrades[0];

                foreach (var g in specialGrades)
                {
                    current += g.dropProbability;
                    if (roll <= current)
                    {
                        selectedGrade = g;
                        break;
                    }
                }
            }
        }

        // Normal path or fallback if no special grades found
        if (selectedGrade == null)
        {
            float totalProb = 0f;
            foreach (var g in settings.grades) totalProb += g.dropProbability;

            float roll = Random.Range(0f, totalProb);
            float current = 0f;
            selectedGrade = settings.grades[0];

            foreach (var g in settings.grades)
            {
                current += g.dropProbability;
                if (roll <= current)
                {
                    selectedGrade = g;
                    break;
                }
            }

            if (selectedGrade.gradeName.ToLower() != "normal")
            {
                if (purchaseCount <= 4)
                {
                    hasRolledSpecialGradeInFirstFour = true;
                }
            }
        }

        return GenerateBlock(selectedGrade, database, position, parent);
    }

    /// <summary>
    /// 트럭 그리드에 배치된 포탑들의 인접 패턴을 수집합니다.
    /// 방향성 있는 쌍 (A,B)와 (B,A)를 각각 기록하며, 등장 횟수를 값으로 저장합니다.
    /// </summary>
    private Dictionary<(TurretDataSO, TurretDataSO), int> GetGridAdjacencyPairs()
    {
        var adjPairs = new Dictionary<(TurretDataSO, TurretDataSO), int>();

        TruckGrid[] grids = FindObjectsByType<TruckGrid>(FindObjectsSortMode.None);
        foreach (var grid in grids)
        {
            if (grid == null) continue;

            // O(1) 이웃 접근을 위한 (x,y) → GridCell 룩업 테이블 구성
            var cellLookup = new Dictionary<Vector2Int, GridCell>();
            foreach (var cell in grid.Cells)
            {
                if (cell == null) continue;
                cellLookup[new Vector2Int(cell.GridX, cell.GridY)] = cell;
            }

            // Right, Up 방향만 스캔 (각 인접 쌍을 물리적으로 1회 발견)
            Vector2Int[] scanOffsets = { new Vector2Int(1, 0), new Vector2Int(0, 1) };

            foreach (var cell in grid.Cells)
            {
                if (cell == null || !cell.IsOccupied) continue;
                TurretDataSO typeA = cell.OccupiedTurretData;
                if (typeA == null) continue;

                foreach (var offset in scanOffsets)
                {
                    var neighborPos = new Vector2Int(cell.GridX + offset.x, cell.GridY + offset.y);
                    if (!cellLookup.TryGetValue(neighborPos, out GridCell neighbor)) continue;
                    if (!neighbor.IsOccupied) continue;

                    TurretDataSO typeB = neighbor.OccupiedTurretData;
                    if (typeB == null) continue;

                    // 양방향 모두 기록
                    var keyAB = (typeA, typeB);
                    if (!adjPairs.ContainsKey(keyAB)) adjPairs[keyAB] = 0;
                    adjPairs[keyAB]++;

                    if (!typeA.Equals(typeB))
                    {
                        var keyBA = (typeB, typeA);
                        if (!adjPairs.ContainsKey(keyBA)) adjPairs[keyBA] = 0;
                        adjPairs[keyBA]++;
                    }
                }
            }
        }

        return adjPairs;
    }

    /// <summary>
    /// 후보 블록의 타입 배정을 점수화합니다.
    /// 블록 내부에서 인접한 타일 쌍이 그리드의 인접 패턴과 일치할수록 높은 점수를 받습니다.
    /// </summary>
    private float ScoreBlockAssignment(
        List<Vector2Int> cellPositions,
        List<TurretDataSO> types,
        Dictionary<(TurretDataSO, TurretDataSO), int> gridAdjPairs)
    {
        if (gridAdjPairs.Count == 0) return 1f;

        // 빠른 위치 → 타입 룩업
        var posToType = new Dictionary<Vector2Int, TurretDataSO>(cellPositions.Count);
        for (int i = 0; i < cellPositions.Count; i++)
            posToType[cellPositions[i]] = types[i];

        float score = 1f; // 기본 점수 (모든 후보 최소 선택 가능)

        Vector2Int[] neighborOffsets =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        for (int i = 0; i < cellPositions.Count; i++)
        {
            TurretDataSO typeA = types[i];
            foreach (var offset in neighborOffsets)
            {
                var neighborPos = cellPositions[i] + offset;
                if (!posToType.TryGetValue(neighborPos, out TurretDataSO typeB)) continue;

                if (gridAdjPairs.TryGetValue((typeA, typeB), out int count))
                {
                    score += count * adjacencyMatchBiasWeight;
                }
            }
        }

        return score;
    }

    private TurretBlock GenerateBlock(GradeSetting grade, TurretDatabaseSO database, Vector3 position, Transform parent)
    {
        // 1. 블록 루트 GameObject 생성
        GameObject blockGo = new GameObject("GeneratedTurretBlock");
        blockGo.transform.position = position;
        if (parent != null)
        {
            blockGo.transform.SetParent(parent);
        }

        TurretBlock blockObj = blockGo.AddComponent<TurretBlock>();

        int N = Random.Range(grade.minSize, grade.maxSize + 1);
        int M = Random.Range(grade.minTurrets, grade.maxTurrets + 1);

        M = Mathf.Min(M, N * N);

        List<TurretInstance> instances = new List<TurretInstance>();
        HashSet<Vector2Int> selectedCells = new HashSet<Vector2Int>();

        if (M > 0)
        {
            // Polyomino 로직: 그리드 중심에서 시작
            Vector2Int startCell = new Vector2Int(N / 2, N / 2);
            selectedCells.Add(startCell);

            for (int i = 1; i < M; i++)
            {
                List<Vector2Int> candidates = new List<Vector2Int>();
                foreach (var cell in selectedCells)
                {
                    Vector2Int[] neighbors =
                    {
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

        // 좌표 정규화 (바운딩 박스가 (0,0)에서 시작하도록)
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

        int maxX = 0, maxY = 0;
        foreach (var cell in normalizedCells)
        {
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }
        int actualGridSize = Mathf.Max(maxX, maxY) + 1;

        // ── 인접 패턴 기반 후보 선택 ─────────────────────────────
        // 1) 그리드에서 인접 패턴 추출
        var gridAdjPairs = GetGridAdjacencyPairs();
        bool hasPattern = gridAdjPairs.Count > 0;

        // 2) 후보 배정 생성 및 점수화
        int candidateCount = hasPattern ? adjacencyCandidateCount : 1;

        List<List<TurretDataSO>> candidateAssignments = new List<List<TurretDataSO>>(candidateCount);
        List<float> candidateScores = new List<float>(candidateCount);

        for (int k = 0; k < candidateCount; k++)
        {
            var types = new List<TurretDataSO>(normalizedCells.Count);
            foreach (var _ in normalizedCells)
            {
                types.Add(database.GetRandomTurret());
            }

            float score = hasPattern
                ? ScoreBlockAssignment(normalizedCells, types, gridAdjPairs)
                : 1f;

            candidateAssignments.Add(types);
            candidateScores.Add(score);
        }

        // 3) 점수 기반 가중 랜덤으로 최종 후보 선택
        float totalScore = 0f;
        foreach (var s in candidateScores) totalScore += s;

        float pick = Random.Range(0f, totalScore);
        float cumulative = 0f;
        List<TurretDataSO> selectedTypes = candidateAssignments[candidateAssignments.Count - 1]; // 안전 폴백

        for (int k = 0; k < candidateAssignments.Count; k++)
        {
            cumulative += candidateScores[k];
            if (pick <= cumulative)
            {
                selectedTypes = candidateAssignments[k];
                break;
            }
        }
        // ─────────────────────────────────────────────────────────

        // 4) 선택된 타입 배정으로 TurretInstance 목록 구성
        for (int i = 0; i < normalizedCells.Count; i++)
        {
            int randomLevel = Random.Range(grade.minLevel, grade.maxLevel + 1);
            TurretInstance inst = new TurretInstance
            {
                localPosition = normalizedCells[i],
                data = selectedTypes[i],
                level = randomLevel
            };
            instances.Add(inst);
        }

        blockObj.Initialize(actualGridSize, instances, grade.gradeColor);

        return blockObj;
    }

    public TurretBlock GenerateLevel1SingleCellBlock(Vector3 position, Transform parent = null)
    {
        if (gradeSettings == null || gradeSettings.grades.Count == 0 || gradeSettings.turretDatabase == null || gradeSettings.turretDatabase.allTurrets.Count == 0)
        {
            Debug.LogError("BlockGenerator: gradeSettings or Database not configured.");
            return null;
        }

        GameObject blockGo = new GameObject("GeneratedExchangeBlock");
        blockGo.transform.position = position;
        if (parent != null)
        {
            blockGo.transform.SetParent(parent);
        }

        TurretBlock blockObj = blockGo.AddComponent<TurretBlock>();

        // 1×1 단일 셀 — 인접 패턴 의미 없으므로 순수 랜덤
        List<TurretInstance> instances = new List<TurretInstance>();
        TurretInstance inst = new TurretInstance
        {
            localPosition = Vector2Int.zero,
            data = gradeSettings.turretDatabase.GetRandomTurret(),
            level = 1
        };
        instances.Add(inst);

        Color gradeColor = gradeSettings.grades[0].gradeColor;
        blockObj.Initialize(1, instances, gradeColor);

        return blockObj;
    }
}
