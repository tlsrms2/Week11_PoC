using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임의 도움말, 게임 오버, 엔딩 패널을 관리하는 UI 스크립트입니다.
/// </summary>
public class GameOverlayUI : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("도움말 내용을 담고 있는 패널")]
    [SerializeField] private GameObject helpPanel;
    
    [Tooltip("트럭이 파괴되었을 때 나타나는 패널")]
    [SerializeField] private GameObject gameOverPanel;
    
    [Tooltip("모든 웨이브를 클리어했을 때 나타나는 패널")]
    [SerializeField] private GameObject endingPanel;

    private void Awake()
    {
        // 초기에는 모든 패널을 비활성화
        if (helpPanel != null) helpPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (endingPanel != null) endingPanel.SetActive(false);
    }

    private void OnEnable()
    {
        GamePhaseManager.OnGameOver += ShowGameOverPanel;
        GamePhaseManager.OnVictory += ShowEndingPanel;
    }

    private void OnDisable()
    {
        GamePhaseManager.OnGameOver -= ShowGameOverPanel;
        GamePhaseManager.OnVictory -= ShowEndingPanel;
    }

    /// <summary>
    /// 도움말 패널의 표시/숨김 상태를 전환합니다.
    /// UI 버튼의 OnClick 이벤트에 연결해서 사용하세요.
    /// </summary>
    public void ToggleHelpPanel()
    {
        if (helpPanel != null)
        {
            bool isActive = helpPanel.activeSelf;
            helpPanel.SetActive(!isActive);
        }
    }

    /// <summary>
    /// 강제로 도움말 패널을 닫습니다.
    /// </summary>
    public void CloseHelpPanel()
    {
        if (helpPanel != null)
        {
            helpPanel.SetActive(false);
        }
    }

    private void ShowGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    private void ShowEndingPanel()
    {
        if (endingPanel != null)
        {
            endingPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 게임을 처음부터 다시 시작합니다.
    /// 게임오버 패널이나 엔딩 패널에 있는 '재시작' 버튼의 OnClick 이벤트에 연결하세요.
    /// </summary>
    public void RestartGame()
    {
        // GamePhaseManager.RestartGame() 또는 직접 Scene 리로드
        GamePhaseManager phaseManager = FindFirstObjectByType<GamePhaseManager>();
        if (phaseManager != null)
        {
            phaseManager.RestartGame();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
