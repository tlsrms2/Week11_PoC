using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 개체. SpriteRenderer와 BoxCollider2D는 프리팹에서 직접 세팅해야 합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    private static readonly List<Enemy> activeEnemies = new List<Enemy>();

    [Header("Enemy Data")]
    [SerializeField] private EnemyDataSO enemyData;

    [Header("Movement")]
    [SerializeField] private Transform target;
    [SerializeField] private bool moveTowardTarget = true;

    [Header("Rewards")]
    [SerializeField] private Transform droppedBlockParent;
    [SerializeField] private Vector3 droppedBlockOffset = new Vector3(0f, 0.7f, 0f);

    private float currentHealth;
    private float nextAttackTime;
    private Rigidbody2D rb;
    private Collider2D targetCollider;
    private SpriteRenderer spriteRenderer;
    private Transform healthBarBg;
    private Transform healthBarFill;

    private bool isStopped = false;

    private float slowMultiplier = 1f;
    private float slowDurationTimer = 0f;

    public void ApplySlow(float factor, float duration)
    {
        slowMultiplier = Mathf.Min(slowMultiplier, factor);
        slowDurationTimer = Mathf.Max(slowDurationTimer, duration);
    }

    public void StopActions()
    {
        isStopped = true;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public static IReadOnlyList<Enemy> ActiveEnemies => activeEnemies;
    public bool IsAlive => currentHealth > 0f;
    public float CurrentHealth => currentHealth;
    public bool IsSlowed => slowMultiplier < 1f && slowDurationTimer > 0f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            var outline = gameObject.AddComponent<SpriteOutlineHelper>();
            outline.outlineColor = Color.black;
        }
        currentHealth = enemyData != null ? enemyData.maxHealth : 30f;
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)

        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Ensure solid collider so enemies physically block and push each other (no overlap)
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = false;
            // Create a frictionless material dynamically so they slide past each other smoothly
            PhysicsMaterial2D mat = new PhysicsMaterial2D("EnemyFrictionlessMaterial")
            {
                friction = 0f,
                bounciness = 0f
            };
            col.sharedMaterial = mat;
        }

        // Create Health Bar Background dynamically
        GameObject bgObj = new GameObject("HealthBarBG");
        bgObj.transform.SetParent(transform);
        bgObj.transform.localPosition = new Vector3(0f, 0.65f, 0f); // above enemy
        SpriteRenderer bgSr = bgObj.AddComponent<SpriteRenderer>();
        Sprite bgSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        bgSr.sprite = bgSprite;
        bgSr.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        bgSr.sortingOrder = 5;
        bgObj.transform.localScale = new Vector3(0.8f, 0.12f, 1f);
        healthBarBg = bgObj.transform;

        // Create Health Bar Fill dynamically
        GameObject fillObj = new GameObject("HealthBarFill");
        fillObj.transform.SetParent(bgObj.transform);
        fillObj.transform.localPosition = new Vector3(-0.5f, 0f, 0f); // Left align inside BG
        SpriteRenderer fillSr = fillObj.AddComponent<SpriteRenderer>();
        Sprite fillSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
        fillSr.sprite = fillSprite;
        fillSr.color = new Color(1f, 0.2f, 0.2f, 1f); // Red
        fillSr.sortingOrder = 6;
        fillObj.transform.localScale = new Vector3(1f, 1f, 1f);
        healthBarFill = fillObj.transform;
    }

    private void OnEnable()
    {
        currentHealth = enemyData != null ? enemyData.maxHealth : 30f;
        slowMultiplier = 1f;
        slowDurationTimer = 0f;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
        if (healthBarFill != null)
        {
            healthBarFill.localScale = new Vector3(1f, 1f, 1f);
        }

        if (!activeEnemies.Contains(this))
        {
            activeEnemies.Add(this);
        }
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        if (isStopped || !GamePhaseManager.IsDefense)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        UpdateDynamicTarget();

        if (target == null || targetCollider == null)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector3 closestPoint = targetCollider.ClosestPoint(transform.position);
        Vector2 direction = (Vector2)closestPoint - (Vector2)transform.position;
        float distanceSqr = direction.sqrMagnitude;



        if (!moveTowardTarget || distanceSqr <= 0.0001f)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        if (slowDurationTimer > 0f)
        {
            slowDurationTimer -= Time.deltaTime;
            if (slowDurationTimer <= 0f)
            {
                slowMultiplier = 1f;
            }
        }

        if (spriteRenderer != null)
        {
            if (slowMultiplier < 1f && slowDurationTimer > 0f)
            {
                spriteRenderer.color = new Color(0.3f, 0.75f, 1f, 1f);
            }
            else
            {
                spriteRenderer.color = Color.white;
            }
        }

        float moveSpeedVal = enemyData != null ? enemyData.moveSpeed : 1.5f;
        moveSpeedVal *= slowMultiplier;
        
        // Calculate Separation Steering to prevent overlapping
        Vector2 separation = Vector2.zero;
        float avoidRadius = 0.6f;
        int overlapCount = 0;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            Enemy other = activeEnemies[i];
            if (other == null || other == this || !other.gameObject.activeInHierarchy || !other.IsAlive) continue;

            float dist = Vector2.Distance(transform.position, other.transform.position);
            if (dist < avoidRadius && dist > 0.001f)
            {
                Vector2 diff = (Vector2)transform.position - (Vector2)other.transform.position;
                // The closer the other enemy is, the stronger the separation force
                separation += diff.normalized * (1.0f - (dist / avoidRadius));
                overlapCount++;
            }
        }

        Vector2 moveDirection = direction.normalized;
        if (overlapCount > 0)
        {
            // Blend target direction with separation force (e.g., 65% target, 35% avoidance)
            moveDirection = (moveDirection * 0.65f + separation.normalized * 0.35f).normalized;
        }

        if (rb != null)
        {
            rb.linearVelocity = moveDirection * moveSpeedVal;
        }
        else
        {
            transform.position += (Vector3)moveDirection * moveSpeedVal * Time.deltaTime;
        }

        if (moveDirection != Vector2.zero)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            // 스프라이트가 기본적으로 위를 보고 있으므로 -90도를 해줍니다.
            transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);

            // 체력바는 회전의 영향을 받지 않고 항상 머리 위에 고정되도록 합니다.
            if (healthBarBg != null)
            {
                healthBarBg.rotation = Quaternion.identity;
                healthBarBg.position = transform.position + new Vector3(0f, 0.65f, 0f);
            }
        }
    }

    private void UpdateDynamicTarget()
    {
        Transform closestTarget = null;
        Collider2D closestCollider = null;
        float closestDistanceSqr = float.MaxValue;

        // Check Truck
        if (TruckBody.Instance != null && !TruckBody.Instance.IsDestroyed)
        {
            Collider2D truckCollider = TruckBody.Instance.BodyCollider;
            if (truckCollider != null)
            {
                Vector2 closestPt = truckCollider.ClosestPoint(transform.position);
                float distanceSqr = (closestPt - (Vector2)transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestTarget = TruckBody.Instance.transform;
                    closestCollider = truckCollider;
                }
            }
        }

        // Check Trailers
        for (int i = 0; i < TrailerBody.ActiveTrailers.Count; i++)
        {
            TrailerBody trailer = TrailerBody.ActiveTrailers[i];
            if (trailer == null || trailer.IsDestroyed || !trailer.gameObject.activeInHierarchy) continue;

            Collider2D trailerCollider = trailer.BodyCollider;
            if (trailerCollider != null)
            {
                Vector2 closestPt = trailerCollider.ClosestPoint(transform.position);
                float distanceSqr = (closestPt - (Vector2)transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestTarget = trailer.transform;
                    closestCollider = trailerCollider;
                }
            }
        }

        target = closestTarget;
        targetCollider = closestCollider;
    }

    public void TakeDamage(float damage, Color popupColor = default)
    {
        if (!IsAlive || damage <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        
        // 동적 데미지 텍스트 팝업 띄우기 (투사체/타워 폭발 색상 반영)
        DamageTextPopup.Create(transform.position, damage, popupColor);

        if (healthBarFill != null)
        {
            float max = enemyData != null ? enemyData.maxHealth : 30f;
            float ratio = Mathf.Clamp01(currentHealth / max);
            healthBarFill.localScale = new Vector3(ratio, 1f, 1f);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void SetTarget(Transform nextTarget)
    {
        target = nextTarget;
    }

    private void Start()
    {
        ApplySprite();
    }

    private void ApplySprite()
    {
        if (enemyData != null)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && enemyData.enemySprite != null)
            {
                sr.sprite = enemyData.enemySprite;
            }
        }
    }

    public void Configure(EnemyDataSO data, Transform nextTarget, Transform blockParent)
    {
        this.enemyData = data;
        this.target = nextTarget;
        this.droppedBlockParent = blockParent;

        if (data != null)
        {
            this.currentHealth = data.maxHealth;
            ApplySprite();
        }
        else
        {
            this.currentHealth = 30f;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isStopped || !GamePhaseManager.IsDefense) return;

        TruckBody truck = collision.gameObject.GetComponent<TruckBody>() ?? collision.gameObject.GetComponentInParent<TruckBody>();
        if (truck != null)
        {
            TryAttackTarget(truck, null);
            return;
        }

        TrailerBody trailer = collision.gameObject.GetComponent<TrailerBody>() ?? collision.gameObject.GetComponentInParent<TrailerBody>();
        if (trailer != null)
        {
            TryAttackTarget(null, trailer);
        }
    }

    private void TryAttackTarget(TruckBody truck, TrailerBody trailer)
    {
        if (Time.time < nextAttackTime) return;

        float damage = enemyData != null ? enemyData.contactDamage : 8f;
        float interval = enemyData != null ? enemyData.attackInterval : 1f;

        truck?.TakeDamage(damage);
        trailer?.TakeDamage(damage);

        nextAttackTime = Time.time + interval;
    }

    private void Die()
    {
        GrantRewards();
        gameObject.SetActive(false);
    }

    private void GrantRewards()
    {
        CurrencyWallet wallet = CurrencyWallet.Instance;
        int reward = enemyData != null ? enemyData.currencyReward : 5;
        wallet?.Add(reward);

        if (BlockInventory.Instance != null && BlockInventory.Instance.IsInventoryFull)
        {
            return;
        }

        float dropChance = enemyData != null ? enemyData.blockDropChance : 0.2f;
        if (Random.value > dropChance) return;
        DropTurretBlock();
    }

    private void DropTurretBlock()
    {
        Vector3 dropPosition = transform.position + droppedBlockOffset;
        if (BlockGenerator.Instance != null)
        {
            BlockGenerator.Instance.GenerateRandomBlock(dropPosition, droppedBlockParent);
        }
        else
        {
            Debug.LogWarning("BlockGenerator instance not found! Cannot drop random turret block.");
        }
    }
}
