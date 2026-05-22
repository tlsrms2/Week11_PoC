using UnityEngine;

/// <summary>
/// 트럭 본체의 체력 및 파괴 처리.
/// SpriteRenderer와 BoxCollider2D는 인스펙터 또는 프리팹에서 직접 세팅해야 합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class TruckBody : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Visual")]
    [SerializeField] private Color healthyColor = new Color(0.2f, 0.75f, 0.45f, 0.45f);
    [SerializeField] private Color damagedColor = new Color(1f, 0.3f, 0.15f, 0.65f);

    [Header("Anchor")]
    [SerializeField] private Vector2 rearAnchorOffset = new Vector2(-1.5f, 0f);

    [Header("Health Bar UI")]
    [SerializeField] private Vector3 healthBarOffset = new Vector3(1.5f, 0.8f, 0f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(1.2f, 0.15f);

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D bodyCollider;
    private GameObject hpBarObj;
    private SpriteRenderer hpBarBg;
    private SpriteRenderer hpBarFg;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float HealthRatio => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public bool IsDestroyed => currentHealth <= 0f;
    public Vector2 RearAnchorOffset => rearAnchorOffset;
    public BoxCollider2D BodyCollider => bodyCollider;

    public static TruckBody Instance { get; private set; }

    private Color originalSpriteColor = Color.white;

    private void Awake()
    {
        Instance = this;
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalSpriteColor = Color.white; // Force Color.white to preserve original sprite texture colors intact

        bodyCollider   = GetComponent<BoxCollider2D>();
        bodyCollider.isTrigger = false;

        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        
        CreateHealthBar();
        RefreshVisual();
    }

    private void LateUpdate()
    {
        UpdateHealthBarPositionAndSize();
    }

    private void OnValidate()
    {
        maxHealth     = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        
        UpdateHealthBarPositionAndSize();
    }

    public void TakeDamage(float damage)
    {
        if (IsDestroyed || damage <= 0f || GamePhaseManager.IsGameOver) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        RefreshVisual();

        if (currentHealth <= 0f)
        {
            HandleDestroyed();
        }
    }

    public void Repair(float amount)
    {
        if (amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        RefreshVisual();
    }

    [ContextMenu("Repair Full")]
    public void RepairFull()
    {
        currentHealth = maxHealth;
        RefreshVisual();
    }

    private void HandleDestroyed()
    {
        Debug.Log("Truck body destroyed. Game Over.");
        GamePhaseManager phaseManager = FindFirstObjectByType<GamePhaseManager>();
        phaseManager?.SetPhase(GamePhase.GameOver);
    }

    private void CreateHealthBar()
    {
        hpBarObj = new GameObject("HealthBar");
        hpBarObj.transform.SetParent(transform);
        
        // Initial setup for scale-independent position/rotation
        hpBarObj.transform.localRotation = Quaternion.identity;

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite leftPivotSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(hpBarObj.transform);
        bgObj.transform.localPosition = Vector3.zero;
        bgObj.transform.localRotation = Quaternion.identity;
        bgObj.transform.localScale = new Vector3(healthBarSize.x, healthBarSize.y, 1f);
        hpBarBg = bgObj.AddComponent<SpriteRenderer>();
        hpBarBg.sprite = leftPivotSprite;
        hpBarBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        if (spriteRenderer != null)
        {
            hpBarBg.sortingLayerID = spriteRenderer.sortingLayerID;
            hpBarBg.sortingOrder = spriteRenderer.sortingOrder + 10;
        }

        GameObject fgObj = new GameObject("Foreground");
        fgObj.transform.SetParent(hpBarObj.transform);
        fgObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        fgObj.transform.localRotation = Quaternion.identity;
        fgObj.transform.localScale = new Vector3(healthBarSize.x, healthBarSize.y, 1f);
        hpBarFg = fgObj.AddComponent<SpriteRenderer>();
        hpBarFg.sprite = leftPivotSprite;
        hpBarFg.color = new Color(0.2f, 0.82f, 0.38f, 1f);

        if (spriteRenderer != null)
        {
            hpBarFg.sortingLayerID = spriteRenderer.sortingLayerID;
            hpBarFg.sortingOrder = spriteRenderer.sortingOrder + 11;
        }
        
        UpdateHealthBarPositionAndSize();
    }

    private void UpdateHealthBarPositionAndSize()
    {
        if (hpBarObj == null) return;

        // 1. Scale Compensation: Cancels out any scale distortions from the parent vehicle body.
        // This ensures that 1 unit in healthBarOffset or healthBarSize corresponds to exactly 1 Unity World Unit.
        Vector3 parentScale = transform.localScale;
        hpBarObj.transform.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / Mathf.Abs(parentScale.x),
            Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / Mathf.Abs(parentScale.y),
            Mathf.Approximately(parentScale.z, 0f) ? 1f : 1f / Mathf.Abs(parentScale.z)
        );

        // 2. Position Alignment: Uses parent's normalized local axes to place the health bar.
        // This ensures the offset works relative to the vehicle's orientation, ignoring parent's scale.
        Vector3 worldOffset = transform.right * healthBarOffset.x + transform.up * healthBarOffset.y;
        hpBarObj.transform.position = transform.position + worldOffset;

        // 3. Real-time Inspector update for Background and Foreground scale.
        if (hpBarBg != null)
        {
            hpBarBg.transform.localScale = new Vector3(healthBarSize.x, healthBarSize.y, 1f);
        }
        if (hpBarFg != null)
        {
            hpBarFg.transform.localScale = new Vector3(healthBarSize.x * HealthRatio, healthBarSize.y, 1f);
        }
    }

    private void RefreshVisual()
    {
        UpdateHealthBarPositionAndSize();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 worldPos = transform.TransformPoint(rearAnchorOffset);
        Gizmos.DrawWireSphere(worldPos, 0.15f);
        Gizmos.DrawLine(transform.position, worldPos);
    }
}
