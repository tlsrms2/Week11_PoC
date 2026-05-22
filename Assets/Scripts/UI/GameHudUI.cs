using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD UI.
/// Canvas, 모든 Text/Button 참조는 인스펙터에서 직접 연결해야 합니다.
/// </summary>
public class GameHudUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GamePhaseManager phaseManager;
    [SerializeField] private WaveSpawner waveSpawner;
    [SerializeField] private TruckBody truckBody;
    [SerializeField] private CurrencyWallet currencyWallet;
    [SerializeField] private TrailerManager trailerManager;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private TextMeshProUGUI waveText;

    [Header("Buttons")]
    [SerializeField] private Button startDefenseButton;
    //[SerializeField] 
    private Button expandTrailerButton;
    [SerializeField] private Button purchaseBlockButton;

    [Header("Wave Progress UI")]
    [SerializeField] private Vector2 progressBarOffset = new Vector2(0f, -30f);
    [SerializeField] private float progressBarWidth = 800f; // 기존 500f에서 더 길게 기본값 설정
    [SerializeField] private float progressBarHeight = 25f;

    private RectTransform progressBarContainer;
    private Image progressBarFill;
    private TextMeshProUGUI waveLevelText;
    private RectTransform progressTriangleRect;

    private void Awake()
    {
        BindButtons();

        // 씬에 하드코딩된 Maintenance 버튼이 있다면 제거
        GameObject maintenanceBtn = GameObject.Find("Maintenance");
        if (maintenanceBtn != null)
        {
            Destroy(maintenanceBtn);
        }

        // Dynamically customize button text for a premium in-game feel
        if (startDefenseButton != null)
        {
            var btnText = startDefenseButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = "출발! (Start Stage)";
            }
            else
            {
                var legacyText = startDefenseButton.GetComponentInChildren<Text>();
                if (legacyText != null) legacyText.text = "출발! (Start Stage)";
            }
        }

        CreateWaveProgressBar();
    }

    private void CreateWaveProgressBar()
    {
        // 최상위 컨테이너 생성
        GameObject containerObj = new GameObject("WaveProgressBarContainer");
        containerObj.transform.SetParent(this.transform, false);
        progressBarContainer = containerObj.AddComponent<RectTransform>();
        
        // 화면 상단 중앙
        progressBarContainer.anchorMin = new Vector2(0.5f, 1f);
        progressBarContainer.anchorMax = new Vector2(0.5f, 1f);
        progressBarContainer.pivot = new Vector2(0.5f, 1f);
        progressBarContainer.anchoredPosition = progressBarOffset;
        progressBarContainer.sizeDelta = new Vector2(progressBarWidth, progressBarHeight);

        // 배경색 (기본 흰색)
        Image bgImage = containerObj.AddComponent<Image>();
        bgImage.color = Color.white;

        // 채우기용 (Fill)
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(containerObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f); 
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        // 안쪽에 초록색이 깔끔하게 차오르도록 여백(Padding)을 3px 줍니다.
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        progressBarFill = fillObj.AddComponent<Image>();
        
        // 유니티 UI Image의 Type.Filled가 정상 작동하려면 Sprite가 지정되어 있어야 합니다.
        // Sprite가 null인 경우 fillAmount를 무시하고 꽉 찬 단색 사각형으로 그려지는 문제가 발생합니다.
        Texture2D pixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixelTex.SetPixel(0, 0, Color.white);
        pixelTex.Apply();
        progressBarFill.sprite = Sprite.Create(pixelTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

        progressBarFill.color = new Color(0.12f, 0.75f, 0.33f, 1f); // 산뜻하고 선명한 녹색
        progressBarFill.type = Image.Type.Filled;
        progressBarFill.fillMethod = Image.FillMethod.Horizontal;
        progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressBarFill.fillAmount = 0f;

        // 중앙 텍스트
        GameObject textObj = new GameObject("WaveText");
        textObj.transform.SetParent(containerObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        waveLevelText = textObj.AddComponent<TextMeshProUGUI>();
        waveLevelText.alignment = TextAlignmentOptions.Center;
        waveLevelText.fontSize = 18f;
        waveLevelText.color = new Color(0.15f, 0.15f, 0.15f, 1f); // 흰색/초록색 배경에서 잘 보이도록 어두운 회색으로 변경
        waveLevelText.fontStyle = FontStyles.Bold;

        // 삼각형 인디케이터 생성 (아래에서 위를 가리킴)
        GameObject triangleObj = new GameObject("ProgressTriangle");
        triangleObj.transform.SetParent(containerObj.transform, false);
        progressTriangleRect = triangleObj.AddComponent<RectTransform>();
        
        // 부모의 하단 중심 근처에 부착 (위를 가리키도록 모양을 만듦)
        // 우리는 바의 아래쪽에 삼각형 모양을 위치시켜 위를 가리키게 함
        progressTriangleRect.anchorMin = new Vector2(0f, 0f);
        progressTriangleRect.anchorMax = new Vector2(0f, 0f); // 앵커를 0,0(왼쪽 아래)으로 두고 위치 이동
        progressTriangleRect.pivot = new Vector2(0.5f, 1f); // 윗변(삼각형 꼭짓점) 중심이 피벗
        progressTriangleRect.sizeDelta = new Vector2(20f, 15f);

        Image triangleImage = triangleObj.AddComponent<Image>();
        triangleImage.sprite = CreateTriangleSprite();
        triangleImage.color = new Color(1f, 0.9f, 0f, 1f); // 노란색
        
        progressBarContainer.gameObject.SetActive(false);
    }

    private Sprite CreateTriangleSprite()
    {
        // 런타임에 텍스처를 생성하여 삼각형(△) 모양을 그립니다.
        int width = 32;
        int height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);
        Color solid = Color.white;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 삼각형 렌더링 (y가 클수록(꼭짓점으로 갈수록) 좁아짐)
                float ratio = 1f - ((float)y / height);
                float halfWidth = (width / 2f) * ratio;
                float centerX = width / 2f;
                if (x >= centerX - halfWidth && x <= centerX + halfWidth)
                {
                    tex.SetPixel(x, y, solid);
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 1f));
    }

    private void Update()
    {
        RefreshHud();
    }

    private void BindButtons()
    {
        if (startDefenseButton != null)
        {
            startDefenseButton.onClick.RemoveListener(StartDefense);
            startDefenseButton.onClick.AddListener(StartDefense);
        }


        if (expandTrailerButton != null)
        {
            expandTrailerButton.onClick.RemoveListener(ExpandTrailer);
            expandTrailerButton.onClick.AddListener(ExpandTrailer);
        }

        if (purchaseBlockButton != null)
        {
            purchaseBlockButton.onClick.RemoveListener(PurchaseBlock);
            purchaseBlockButton.onClick.AddListener(PurchaseBlock);
        }
    }

    private void RefreshHud()
    {
        bool isMaintenance = GamePhaseManager.IsMaintenance;
        bool isDefense = GamePhaseManager.IsDefense;

        if (progressBarContainer != null)
        {
            progressBarContainer.gameObject.SetActive(isDefense);
            // 인스펙터 변경 사항을 실시간 반영하기 위해
            progressBarContainer.anchoredPosition = progressBarOffset;
            progressBarContainer.sizeDelta = new Vector2(progressBarWidth, progressBarHeight);

            if (isDefense && waveSpawner != null)
            {
                float totalTime = waveSpawner.DefenseDuration;
                if (totalTime > 0)
                {
                    float remaining = waveSpawner.RemainingDefenseTime;
                    float progress = Mathf.Clamp01(1f - (remaining / totalTime));
                    progressBarFill.fillAmount = progress;
                    
                    if (progressTriangleRect != null)
                    {
                        // x축 위치: 0부터 container width까지
                        float barWidth = progressBarContainer.rect.width;
                        progressTriangleRect.anchoredPosition = new Vector2(progress * barWidth, 0f);
                    }
                }
                if (waveLevelText != null)
                {
                    int totalWaves = waveSpawner.WaveScenarios != null ? waveSpawner.WaveScenarios.Count : 0;
                    waveLevelText.text = $"Wave {waveSpawner.CurrentWaveLevel}/{totalWaves}";
                }
            }
        }

        // 버튼 표시/숨김 처리
        if (startDefenseButton != null)
            startDefenseButton.gameObject.SetActive(isMaintenance);

        if (expandTrailerButton != null)
            expandTrailerButton.gameObject.SetActive(isMaintenance);
        if (purchaseBlockButton != null)
            purchaseBlockButton.gameObject.SetActive(isMaintenance);

        // 버튼 상호작용
        if (startDefenseButton != null)
        {
            startDefenseButton.interactable = !GamePhaseManager.IsGameOver && !GamePhaseManager.IsVictory;
        }


        if (currencyText != null)
        {
            int amount = currencyWallet != null ? currencyWallet.CurrentCurrency : 0;
            currencyText.text = $"Money: ${amount}";
        }

        if (waveText != null)
        {
            int waveNum = waveSpawner != null ? waveSpawner.CurrentWaveLevel : 1;
            int totalWaves = waveSpawner != null && waveSpawner.WaveScenarios != null ? waveSpawner.WaveScenarios.Count : 0;
            waveText.text = $"Wave: {waveNum}/{totalWaves}";
        }

        if (expandTrailerButton != null)
        {
            expandTrailerButton.interactable = trailerManager != null
                && trailerManager.CanAddTrailer
                && !GamePhaseManager.IsGameOver
                && !GamePhaseManager.IsVictory;
        }

        if (purchaseBlockButton != null)
        {
            purchaseBlockButton.interactable = ShopManager.Instance != null 
                && ShopManager.Instance.CanPurchaseBlock 
                && !GamePhaseManager.IsGameOver
                && !GamePhaseManager.IsVictory;

            // 버튼 텍스트에 실시간 블록 가격 표시
            if (ShopManager.Instance != null)
            {
                int currentCost = ShopManager.Instance.BlockPurchaseCost;
                var btnText = purchaseBlockButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = $"블록 구매 (${currentCost})";
                }
                else
                {
                    var legacyText = purchaseBlockButton.GetComponentInChildren<Text>();
                    if (legacyText != null) legacyText.text = $"블록 구매 (${currentCost})";
                }
            }
        }
    }

    private void StartDefense()
    {
        // Use high-end cinematic transition flow instead of direct state jumping
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.StartNextStage();
        }
        else if (phaseManager != null)
        {
            phaseManager.SetPhase(GamePhase.Defense);
        }
    }


    private void ExpandTrailer()
    {
        trailerManager?.TryAddTrailer();
    }

    private void PurchaseBlock()
    {
        ShopManager.Instance?.PurchaseBlock();
    }
}
