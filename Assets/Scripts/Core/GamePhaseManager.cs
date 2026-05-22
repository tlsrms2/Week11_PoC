using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GamePhaseManager : MonoBehaviour
{
    private static GamePhase activePhase = GamePhase.Maintenance;

    [Header("Phase")]
    [SerializeField] private GamePhase startingPhase = GamePhase.Maintenance;
    [SerializeField] private bool allowKeyboardToggle = true;

    public static GamePhase ActivePhase => activePhase;
    public static bool IsDefense => activePhase == GamePhase.Defense;
    public static bool IsMaintenance => activePhase == GamePhase.Maintenance;
    public static bool IsGameOver => activePhase == GamePhase.GameOver;
    public static bool IsVictory => activePhase == GamePhase.Victory;
    // Placed blocks are permanent – they can never be dragged off the grid.
    public static bool CanMovePlacedBlocks => false;
    // Rotation is always allowed regardless of game phase.
    public static bool CanRotateBlocks => true;

    private void Awake()
    {
        SetPhase(startingPhase);
    }

    private void Update()
    {
        if (IsGameOver || IsVictory)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartGame();
            }
            return;
        }

        if (!allowKeyboardToggle || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            SetPhase(GamePhase.Defense);
        }
        else if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            SetPhase(GamePhase.Maintenance);
        }
    }

    [ContextMenu("Set Defense Phase")]
    public void SetDefensePhase()
    {
        SetPhase(GamePhase.Defense);
    }

    [ContextMenu("Set Maintenance Phase")]
    public void SetMaintenancePhase()
    {
        SetPhase(GamePhase.Maintenance);
    }

    [ContextMenu("Set Game Over")]
    public void SetGameOverPhase()
    {
        SetPhase(GamePhase.GameOver);
    }

    [ContextMenu("Set Victory")]
    public void SetVictoryPhase()
    {
        SetPhase(GamePhase.Victory);
    }

    public static event System.Action OnGameOver;
    public static event System.Action OnVictory;

    public void SetPhase(GamePhase nextPhase)
    {
        activePhase = nextPhase;
        Debug.Log($"Game phase changed: {activePhase}");
        
        if (activePhase == GamePhase.GameOver)
        {
            Debug.Log("GAME OVER! Press 'R' to Restart.");
            OnGameOver?.Invoke();
        }
        else if (activePhase == GamePhase.Victory)
        {
            Debug.Log("VICTORY! ALL WAVES COMPLETED! Press 'R' to Restart.");
            OnVictory?.Invoke();
        }
    }

    public void RestartGame()
    {
        Debug.Log("Restarting Game...");
        activePhase = startingPhase;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
