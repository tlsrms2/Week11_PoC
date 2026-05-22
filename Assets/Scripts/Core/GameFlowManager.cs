using System.Collections;
using UnityEngine;

/// <summary>
/// 무한 횡스크롤 제어, 스테이지 완료 연출, 정비소 진입/정차 연출, 페이드 연동을 총괄 지휘하는 통합 게임 흐름 컨트롤러.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    private static GameFlowManager instance;
    public static GameFlowManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameFlowManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GameFlowManager");
                    instance = go.AddComponent<GameFlowManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Transition Settings")]
    [SerializeField] private float fadeDuration = 1.2f;

    [Header("Background Settings (Visual Flow)")]
    [SerializeField] private Sprite maintenanceBackgroundSprite;
    public Sprite MaintenanceBackgroundSprite => maintenanceBackgroundSprite;

    [SerializeField] private SpriteRenderer stageClearTransitBackground;
    public SpriteRenderer StageClearTransitBackground => stageClearTransitBackground;

    [Header("Infinite Horizontal Scroll Settings")]
    [SerializeField] private SpriteRenderer[] backgrounds;
    [SerializeField] private float scrollSpeed = 2f;
    [SerializeField] private bool scrollOnlyDuringDefense = true;

    private Sprite[] defaultSprites;
    private float backgroundWidth = 19.2f;
    private bool isManualOverride = false;
    private float manualSpeed = 0f;
    private Vector3[] initialBackgroundPositions; // 게임 시작 시 초기 배경 로컬 위치 저장용

    /// <summary>
    /// 외부 드롭 아이템 등이 공용으로 참조하여 수평 스피드를 연동할 수 있는 글로벌 스크롤 속도
    /// </summary>
    public static float CurrentScrollSpeed { get; private set; }

    private GameObject stationBaseObject;
    private bool isTransitioning = false;

    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 1. Initialize infinite horizontal scroll backgrounds loop configuration
        if (backgrounds == null || backgrounds.Length < 2)
        {
            Debug.LogWarning("GameFlowManager: Please assign at least 2 sequence backgrounds in the Inspector for infinite scrolling!");
        }
        else
        {
            // Cache default background sprites and initial local positions for seamless restoration later
            defaultSprites = new Sprite[backgrounds.Length];
            initialBackgroundPositions = new Vector3[backgrounds.Length];
            for (int i = 0; i < backgrounds.Length; i++)
            {
                if (backgrounds[i] != null)
                {
                    defaultSprites[i] = backgrounds[i].sprite;
                    initialBackgroundPositions[i] = backgrounds[i].transform.localPosition;
                }
            }

            // Calculate precise world space width based on the Sprite renderer bounds and local transform scales.
            SpriteRenderer firstSr = backgrounds[0];
            if (firstSr != null && firstSr.sprite != null)
            {
                float spriteWidth = firstSr.sprite.rect.width / firstSr.sprite.pixelsPerUnit;
                backgroundWidth = spriteWidth * Mathf.Abs(firstSr.transform.localScale.x);
            }
            else
            {
                backgroundWidth = 19.2f; // Fallback to standard 1080p pixel width equivalent unit
            }

            // Sort sequence alignment initially along the local horizontal axis to guarantee seamless follow-up
            if (backgrounds[0] != null && backgrounds[1] != null)
            {
                Vector3 firstPos = backgrounds[0].transform.localPosition;
                backgrounds[1].transform.localPosition = new Vector3(firstPos.x + backgroundWidth, firstPos.y, firstPos.z);
            }
        }

        // 2. If starting in Maintenance Phase, initialize truck in the center and load maintenance backgrounds immediately
        if (GamePhaseManager.IsMaintenance)
        {
            TruckMovement truck = FindFirstObjectByType<TruckMovement>();
            if (truck != null)
            {
                Vector3 targetPos = truck.IsFixedPosition 
                    ? new Vector3(truck.FixedPosition.x, truck.FixedPosition.y, truck.transform.position.z)
                    : new Vector3(0f, 0f, truck.transform.position.z);
                truck.transform.position = targetPos;
            }

            if (maintenanceBackgroundSprite != null)
            {
                SetBackgroundSprite(maintenanceBackgroundSprite);
            }
            SetManualScroll(0f);
        }

        // 3. Initialize Transit Background (make sure it is disabled initially in the scene)
        if (stageClearTransitBackground != null)
        {
            stageClearTransitBackground.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // 컷씬 연출 진행 중에는 Update() 내의 자동 횡스크롤 및 무한 루프 스냅 로직을 완전히 중지합니다.
        // 모든 스크롤 및 포지션 제어는 코루틴 내부에서 정밀 전담합니다.
        if (isTransitioning)
        {
            return;
        }

        // Handle scroll speed determination based on manual overrides or current phase
        if (isManualOverride)
        {
            CurrentScrollSpeed = manualSpeed;
        }
        else
        {
            bool shouldScroll = true;
            if (scrollOnlyDuringDefense)
            {
                shouldScroll = GamePhaseManager.IsDefense;
            }
            CurrentScrollSpeed = shouldScroll ? scrollSpeed : 0f;
        }

        if (CurrentScrollSpeed <= 0f || backgrounds == null || backgrounds.Length < 2)
        {
            return;
        }

        // Translate all backgrounds leftwards to create rightwards moving illusion
        float movement = CurrentScrollSpeed * Time.deltaTime;
        for (int i = 0; i < backgrounds.Length; i++)
        {
            if (backgrounds[i] != null)
            {
                backgrounds[i].transform.localPosition += Vector3.left * movement;
            }
        }

        // Perform seamless loop warp snaps
        for (int i = 0; i < backgrounds.Length; i++)
        {
            if (backgrounds[i] != null)
            {
                if (backgrounds[i].transform.localPosition.x <= -backgroundWidth)
                {
                    int otherIndex = (i == 0) ? 1 : 0;
                    if (backgrounds[otherIndex] != null)
                    {
                        Vector3 otherPos = backgrounds[otherIndex].transform.localPosition;
                        // Precision snapping right next to the other tile with a microscopic overlap
                        backgrounds[i].transform.localPosition = new Vector3(
                            otherPos.x + backgroundWidth - 0.01f, 
                            otherPos.y, 
                            otherPos.z
                        );
                    }
                }
            }
        }
    }

    /// <summary>
    /// 스테이지 클리어 시 스파우너에 의해 호출되어 기지 도착 연출 및 정비 페이즈 전환 구동
    /// </summary>
    public void OnStageCleared()
    {
        StartCoroutine(QueueStageClearCutscene());
    }

    private IEnumerator QueueStageClearCutscene()
    {
        // 이전 컷씬 연출(예: 다음 디펜스 출발 연출)이 완전히 끝날 때까지 스레드 지연 대기
        while (isTransitioning)
        {
            yield return null;
        }
        yield return StartCoroutine(StageClearCutscene());
    }

    /// <summary>
    /// 정비 완료 후 UI 버튼 클릭 시 다음 디펜스 전투 출발 연출 구동
    /// </summary>
    public void StartNextStage()
    {
        if (isTransitioning) return;
        StartCoroutine(StartDefenseCutscene());
    }

    private IEnumerator StageClearCutscene()
    {
        isTransitioning = true;
        Debug.Log("GameFlowManager: Stage Clear Cutscene Started.");

        GameObject mBackgroundObj = null;
        try
        {
            // 1. Clean up active combat elements
            WaveSpawner spawner = FindFirstObjectByType<WaveSpawner>();
            TruckMovement truck = FindFirstObjectByType<TruckMovement>();

            if (spawner != null)
            {
                spawner.StopAllEnemies();
            }

            // 2. Setup the 3rd Transit Background object at the END of the current horizontal backgrounds loop
            float originalY = 0f;
            float originalZ = 0f;

            if (stageClearTransitBackground == null)
            {
                Debug.LogWarning("GameFlowManager: 'Stage Clear Transit Background' (SpriteRenderer) is NOT assigned in the GameFlowManager Inspector! Proceeding with fallback quick transition.");
            }
            else
            {
                mBackgroundObj = stageClearTransitBackground.gameObject;
                mBackgroundObj.SetActive(true);

                // Preserve original Y and Z coordinate values from the scene pre-placed object setup
                originalY = stageClearTransitBackground.transform.position.y;
                originalZ = stageClearTransitBackground.transform.position.z;

                stationBaseObject = mBackgroundObj; // Cached to disable upon departure
            }

            // Find the rightmost background tile currently in the scroll sequence
            SpriteRenderer rightmostBG = null;
            if (backgrounds != null && backgrounds.Length > 0)
            {
                rightmostBG = backgrounds[0];
                for (int i = 1; i < backgrounds.Length; i++)
                {
                    if (backgrounds[i] != null && (rightmostBG == null || backgrounds[i].transform.position.x > rightmostBG.transform.position.x))
                    {
                        rightmostBG = backgrounds[i];
                    }
                }
            }

            // Place the transit background perfectly next to the rightmost tile
            float startBGX = 19.2f;
            if (rightmostBG != null)
            {
                startBGX = rightmostBG.transform.position.x + backgroundWidth;
            }

            if (mBackgroundObj != null)
            {
                mBackgroundObj.transform.position = new Vector3(startBGX, originalY, originalZ);
            }

            // 3. Move all backgrounds and the truck simultaneously so they arrive at the exact center in precisely 2.0 seconds
            Vector3 startTruckPos = truck != null ? truck.transform.position : Vector3.zero;
            Vector3 targetTruckPos = truck != null && truck.IsFixedPosition
                ? new Vector3(truck.FixedPosition.x, truck.FixedPosition.y, startTruckPos.z)
                : new Vector3(0f, 0f, startTruckPos.z); // Center truck

            // Cache starting positions of active scroll backgrounds and transit background for precision Lerp
            Vector3[] startBGPositions = new Vector3[backgrounds != null ? backgrounds.Length : 0];
            if (backgrounds != null)
            {
                for (int i = 0; i < backgrounds.Length; i++)
                {
                    if (backgrounds[i] != null)
                    {
                        startBGPositions[i] = backgrounds[i].transform.position;
                    }
                }
            }

            // Cache active enemies and dropped blocks to move them along with the background
            Enemy[] allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            Vector3[] startEnemyPositions = new Vector3[allEnemies.Length];
            for (int i = 0; i < allEnemies.Length; i++)
            {
                startEnemyPositions[i] = allEnemies[i].transform.position;
            }

            TurretBlock[] allBlocks = FindObjectsByType<TurretBlock>(FindObjectsSortMode.None);
            System.Collections.Generic.List<TurretBlock> droppedBlocks = new System.Collections.Generic.List<TurretBlock>();
            foreach (var b in allBlocks)
            {
                if (!b.IsPlaced && !b.IsInInventory)
                {
                    droppedBlocks.Add(b);
                }
            }
            Vector3[] startBlockPositions = new Vector3[droppedBlocks.Count];
            for (int i = 0; i < droppedBlocks.Count; i++)
            {
                startBlockPositions[i] = droppedBlocks[i].transform.position;
            }
            Vector3 startClearBGPos = mBackgroundObj != null ? mBackgroundObj.transform.position : Vector3.zero;

            float duration = 1.5f; // Total cutscene travel duration (Strictly 1.5 seconds)
            float elapsedTime = 0f;

            if (mBackgroundObj != null || truck != null)
            {
                while (elapsedTime < duration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / duration;
                    float tSmooth = Mathf.SmoothStep(0f, 1f, t); // Elastic natural stop deceleration curve

                    // 1) Translate active horizontal loop backgrounds concurrently
                    if (backgrounds != null)
                    {
                        for (int i = 0; i < backgrounds.Length; i++)
                        {
                            if (backgrounds[i] != null)
                            {
                                Vector3 targetPos = startBGPositions[i] + Vector3.left * startBGX;
                                backgrounds[i].transform.position = Vector3.Lerp(startBGPositions[i], targetPos, tSmooth);
                            }
                        }
                    }

                    // 2) Translate transit background concurrently
                    if (mBackgroundObj != null)
                    {
                        Vector3 targetPos = new Vector3(0f, originalY, originalZ);
                        mBackgroundObj.transform.position = Vector3.Lerp(startClearBGPos, targetPos, tSmooth);
                    }

                    // 3) Smoothly Lerp truck to center at the same pace
                    if (truck != null)
                    {
                        truck.transform.position = Vector3.Lerp(startTruckPos, targetTruckPos, tSmooth);
                    }

                    // 4) Translate Enemies and Dropped Blocks leftwards identically to the backgrounds
                    for (int i = 0; i < allEnemies.Length; i++)
                    {
                        if (allEnemies[i] != null)
                        {
                            Vector3 targetPos = startEnemyPositions[i] + Vector3.left * startBGX;
                            allEnemies[i].transform.position = Vector3.Lerp(startEnemyPositions[i], targetPos, tSmooth);
                        }
                    }
                    for (int i = 0; i < droppedBlocks.Count; i++)
                    {
                        if (droppedBlocks[i] != null)
                        {
                            Vector3 targetPos = startBlockPositions[i] + Vector3.left * startBGX;
                            droppedBlocks[i].transform.position = Vector3.Lerp(startBlockPositions[i], targetPos, tSmooth);
                        }
                    }

                    yield return null;
                }
            }

            // Hard lock precisely at center upon arrival
            if (mBackgroundObj != null)
            {
                mBackgroundObj.transform.position = new Vector3(0f, originalY, originalZ);
            }
            if (backgrounds != null)
            {
                for (int i = 0; i < backgrounds.Length; i++)
                {
                    if (backgrounds[i] != null && i < startBGPositions.Length)
                    {
                        backgrounds[i].transform.position = startBGPositions[i] + Vector3.left * startBGX;
                    }
                }
            }
            if (truck != null)
            {
                truck.transform.position = targetTruckPos;
            }
            SetManualScroll(0f);

            Debug.Log("GameFlowManager: Arrived at Transit Station!");

            // Wait to admire the parked station (Only wait if we actually had a station visual, else proceed quickly)
            float waitDuration = (mBackgroundObj != null) ? 1.5f : 0.2f;
            yield return new WaitForSeconds(waitDuration);

            // 4. Fade out before loading maintenance phase
            if (ScreenFader.Instance != null)
            {
                Coroutine fadeCo = ScreenFader.Instance.FadeOut(fadeDuration);
                if (fadeCo != null)
                {
                    yield return fadeCo;
                }
                else
                {
                    yield return new WaitForSeconds(fadeDuration);
                }
            }
            else
            {
                Debug.LogWarning("GameFlowManager: ScreenFader.Instance is missing! Simulating fade delay.");
                yield return new WaitForSeconds(fadeDuration);
            }

            // 5. Set Phase to Maintenance inside darkness
            GamePhaseManager phaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (phaseManager != null)
            {
                phaseManager.SetPhase(GamePhase.Maintenance);
            }

            // 자동 풀피 회복 (리페어 삭제 반영)
            TruckBody truckBody = TruckBody.Instance != null ? TruckBody.Instance : FindFirstObjectByType<TruckBody>();
            if (truckBody != null)
            {
                truckBody.Repair(9999f);
            }
            foreach (var tr in TrailerBody.ActiveTrailers)
            {
                if (tr != null) tr.Repair(9999f);
            }

            // Explicitly force truck to absolute center (0, 0) like fresh start of the game
            if (truck != null)
            {
                Vector3 targetPos = truck.IsFixedPosition
                    ? new Vector3(truck.FixedPosition.x, truck.FixedPosition.y, truck.transform.position.z)
                    : new Vector3(0f, 0f, truck.transform.position.z);
                truck.transform.position = targetPos;
            }

            // Increment wave level internally
            if (spawner != null)
            {
                spawner.PrepareNextWave();
            }

            // Under the cover of darkness, disable the temporary 3rd transit background (Do NOT destroy, pre-placed!)
            if (stationBaseObject != null)
            {
                stationBaseObject.SetActive(false);
                stationBaseObject = null;
            }

            // Swap the infinite scroll backgrounds to the real Maintenance garage background!
            if (maintenanceBackgroundSprite != null)
            {
                SetBackgroundSprite(maintenanceBackgroundSprite);
            }
            else
            {
                Debug.LogWarning("GameFlowManager: 'Maintenance Background Sprite' is NOT assigned in the GameFlowManager Inspector! Keeping previous background.");
            }
            
            // Explicitly force scroll speed to 0 for maintenance phase, regardless of whether a new background was swapped
            SetManualScroll(0f); 

            yield return new WaitForSeconds(0.4f);

            // 6. Fade in to show the truck safely parked in the maintenance garage
            if (ScreenFader.Instance != null)
            {
                Coroutine fadeCo = ScreenFader.Instance.FadeIn(fadeDuration);
                if (fadeCo != null)
                {
                    yield return fadeCo;
                }
                else
                {
                    yield return new WaitForSeconds(fadeDuration);
                }
            }
            else
            {
                yield return new WaitForSeconds(fadeDuration);
            }

            Debug.Log("GameFlowManager: Stage Clear Cutscene Completed.");
        }
        finally
        {
            isTransitioning = false;
        }
    }

    private IEnumerator StartDefenseCutscene()
    {
        isTransitioning = true;
        Debug.Log("GameFlowManager: Start Next Stage Cutscene Started.");

        try
        {
            // 1. Move truck forward slowly on X-axis ONLY and Fade Out to black simultaneously
            TruckMovement truck = FindFirstObjectByType<TruckMovement>();

            SetManualScroll(0f);

            Vector3 startTruckPos = truck != null ? truck.transform.position : Vector3.zero;
            // Keep original Y position intact, only modify X position!
            Vector3 targetTruckPos = truck != null && truck.IsFixedPosition
                ? new Vector3(truck.FixedPosition.x, truck.FixedPosition.y, startTruckPos.z)
                : new Vector3(6f, startTruckPos.y, startTruckPos.z);

            // Start Fade Out
            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.FadeOut(fadeDuration);
            }
            else
            {
                Debug.LogWarning("GameFlowManager: ScreenFader.Instance is missing during StartDefenseCutscene!");
            }

            float duration = 1.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float tSmooth = Mathf.SmoothStep(0f, 1f, t);

                if (truck != null)
                {
                    truck.transform.position = Vector3.Lerp(startTruckPos, targetTruckPos, tSmooth);
                }

                yield return null;
            }

            // 2. Set Phase to Defense inside darkness (Once faded out completely)
            GamePhaseManager phaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (phaseManager != null)
            {
                phaseManager.SetPhase(GamePhase.Defense);
            }

            // Disable transit base station background if still active
            if (stationBaseObject != null)
            {
                stationBaseObject.SetActive(false);
                stationBaseObject = null;
            }

            // Restore default backgrounds
            RestoreDefaultBackground();

            // Reset truck to defense starting position, keeping Y coordinate fully intact!
            if (truck != null)
            {
                Vector3 defenseStartPos = truck.IsFixedPosition
                    ? new Vector3(truck.FixedPosition.x, truck.FixedPosition.y, truck.transform.position.z)
                    : new Vector3(-4f, startTruckPos.y, startTruckPos.z);
                truck.transform.position = defenseStartPos;
            }

            // Reload wave configuration and set starting state
            WaveSpawner spawner = FindFirstObjectByType<WaveSpawner>();
            if (spawner != null)
            {
                spawner.ResetWave();
            }

            yield return new WaitForSeconds(0.3f);

            // 3. Release manual speed control (starts default scrolling speed)
            ClearManualScroll();

            // 4. Fade in during defense start
            if (ScreenFader.Instance != null)
            {
                Coroutine fadeCo = ScreenFader.Instance.FadeIn(fadeDuration);
                if (fadeCo != null)
                {
                    yield return fadeCo;
                }
                else
                {
                    yield return new WaitForSeconds(fadeDuration);
                }
            }
            else
            {
                yield return new WaitForSeconds(fadeDuration);
            }

            Debug.Log("GameFlowManager: Start Next Stage Cutscene Completed.");
        }
        finally
        {
            isTransitioning = false;
        }
    }

    /// <summary>
    /// 수동 횡스크롤 속도를 강제 강제 제어합니다.
    /// </summary>
    public void SetManualScroll(float speed)
    {
        isManualOverride = true;
        manualSpeed = speed;
    }

    /// <summary>
    /// 수동 속도 제어를 해제하고 일반 스크롤 로직으로 전환합니다.
    /// </summary>
    public void ClearManualScroll()
    {
        isManualOverride = false;
    }

    /// <summary>
    /// 전체 배경 렌더러의 스프라이트를 일괄 교체하고 초기 저장된 로컬 위치로 복원합니다. (정비소 배경일 경우 Y축을 -0.38f 만큼 하향 보정)
    /// </summary>
    public void SetBackgroundSprite(Sprite newSprite)
    {
        if (backgrounds == null || newSprite == null) return;
        for (int i = 0; i < backgrounds.Length; i++)
        {
            if (backgrounds[i] != null)
            {
                backgrounds[i].sprite = newSprite;
                if (initialBackgroundPositions != null && i < initialBackgroundPositions.Length)
                {
                    Vector3 targetLocalPos = initialBackgroundPositions[i];
                    // 정비소 배경(maintenanceBackgroundSprite)일 경우에만 Y축을 -0.38f로 보정
                    if (newSprite == maintenanceBackgroundSprite)
                    {
                        targetLocalPos.y = targetLocalPos.y - 0.38f;
                    }
                    backgrounds[i].transform.localPosition = targetLocalPos;
                }
            }
        }
    }

    /// <summary>
    /// 최초에 캐시해 두었던 기본 전투용 무한 횡스크롤 이미지들로 교체 복구하고 초기 로컬 위치로 복원합니다.
    /// </summary>
    public void RestoreDefaultBackground()
    {
        if (backgrounds == null || defaultSprites == null) return;
        for (int i = 0; i < backgrounds.Length; i++)
        {
            if (backgrounds[i] != null && i < defaultSprites.Length)
            {
                backgrounds[i].sprite = defaultSprites[i];
                if (initialBackgroundPositions != null && i < initialBackgroundPositions.Length)
                {
                    backgrounds[i].transform.localPosition = initialBackgroundPositions[i];
                }
            }
        }
    }
}
