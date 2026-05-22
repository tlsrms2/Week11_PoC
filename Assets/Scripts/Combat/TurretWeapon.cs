using System.Collections.Generic;
using UnityEngine;

public class TurretWeapon : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private Transform firePoint;

    [Header("Muzzle Flash Effect")]
    [SerializeField] private GameObject muzzleFlashVisual;
    [SerializeField] private float flashDuration = 0.05f;

    [Header("Aura Effect")]
    private AuraVisualController auraVisualCtrl;

    [Header("Debug")]
    [SerializeField] private bool drawRangeGizmo = true;

    private TurretBlock parentBlock;
    private TurretInstance turretInstance;
    
    private Enemy currentTarget;
    private float nextTargetRefreshTime;
    private float nextFireTime;
    private Coroutine flashCoroutine;

    // Aura State Tracker
    private bool isAuraActive;
    private float auraStateTimer;
    private float nextAuraTickTime;

    public void Initialize(TurretBlock block, TurretInstance instance)
    {
        parentBlock = block;
        turretInstance = instance;
    }

    private void Start()
    {
        if (muzzleFlashVisual == null)
        {
            // Try to find a child named MuzzleFlash, Flash, or Effect as fallback
            Transform flashChild = transform.Find("MuzzleFlash") ?? transform.Find("Flash") ?? transform.Find("Effect");
            if (flashChild != null)
            {
                muzzleFlashVisual = flashChild.gameObject;
            }
        }

        if (muzzleFlashVisual != null)
        {
            muzzleFlashVisual.SetActive(false);
        }

        // Initialize dynamic Aura Visual if this is an AuraDamage turret
        if (turretInstance != null && turretInstance.data != null && turretInstance.data.attackType == TurretAttackType.AuraDamage)
        {
            GameObject auraObj = new GameObject("DynamicAuraVisual");
            auraObj.transform.SetParent(transform);
            auraObj.transform.localPosition = Vector3.zero;
            auraVisualCtrl = auraObj.AddComponent<AuraVisualController>();
            auraVisualCtrl.Initialize(turretInstance.data.explosionColor);
        }
    }

    private void DeactivateAura(bool immediate = false)
    {
        isAuraActive = false;
        if (turretInstance != null && turretInstance.data != null)
        {
            auraStateTimer = turretInstance.data.auraRestDuration;
        }
        else
        {
            auraStateTimer = 0f;
        }

        if (auraVisualCtrl != null)
        {
            auraVisualCtrl.Hide(immediate);
        }
        if (muzzleFlashVisual != null)
        {
            muzzleFlashVisual.SetActive(false);
        }
    }

    private void Update()
    {
        if (parentBlock == null || !parentBlock.IsPlaced || parentBlock.AnchorCell == null)
        {
            currentTarget = null;
            transform.localRotation = Quaternion.identity;
            DeactivateAura();
            return;
        }

        if (turretInstance != null && turretInstance.data != null && turretInstance.data.attackOnlyDuringDefense && !GamePhaseManager.IsDefense)
        {
            transform.localRotation = Quaternion.identity;
            DeactivateAura(true);
            return;
        }

        // Find the specific GridCell this instance is on
        TruckGrid grid = parentBlock.AnchorCell.Grid;
        GridCell myCell = grid.GetCell(
            parentBlock.AnchorCell.GridX + turretInstance.localPosition.x, 
            parentBlock.AnchorCell.GridY + turretInstance.localPosition.y
        );

        if (myCell == null) return;

        // Only the top instance on this cell fires
        if (!myCell.IsTopInstance(turretInstance))
        {
             return;
        }

        TurretStats stats = myCell.EffectiveStats;
        if (stats.damage <= 0f || stats.fireRate <= 0f || stats.range <= 0f)
        {
            DeactivateAura(false);
            return;
        }

        // 3. Aura Zone Attack Behavior
        if (turretInstance != null && turretInstance.data != null && turretInstance.data.attackType == TurretAttackType.AuraDamage)
        {
            auraStateTimer -= Time.deltaTime;
            if (auraStateTimer <= 0f)
            {
                isAuraActive = !isAuraActive;
                if (isAuraActive)
                {
                    // Start active phase (N seconds)
                    auraStateTimer = turretInstance.data.auraActiveDuration;
                    if (auraVisualCtrl != null) auraVisualCtrl.Show(stats.range);
                    if (muzzleFlashVisual != null) muzzleFlashVisual.SetActive(true);
                }
                else
                {
                    // Start rest phase (M seconds)
                    auraStateTimer = turretInstance.data.auraRestDuration;
                    if (auraVisualCtrl != null) auraVisualCtrl.Hide();
                    if (muzzleFlashVisual != null) muzzleFlashVisual.SetActive(false);
                }
            }

            if (isAuraActive)
            {
                if (Time.time >= nextAuraTickTime)
                {
                    // Deal tick damage to all enemies in range
                    float range = stats.range;
                    float rangeSqr = range * range;
                    IReadOnlyList<Enemy> activeEnemies = Enemy.ActiveEnemies;
                    
                    for (int i = 0; i < activeEnemies.Count; i++)
                    {
                        Enemy enemy = activeEnemies[i];
                        if (enemy != null && enemy.IsAlive)
                        {
                            if (((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude <= rangeSqr)
                            {
                                enemy.TakeDamage(stats.damage * 0.5f); // Half damage per tick
                                
                                // Spawn explosion effect for visual feedback
                                if (turretInstance.data.explosionSprite != null)
                                {
                                    GameObject effectObj = new GameObject("ExplosionEffect");
                                    effectObj.transform.position = enemy.transform.position;
                                    ExplosionEffect effect = effectObj.AddComponent<ExplosionEffect>();
                                    // Scale 0.5f for aesthetic
                                    effect.Initialize(turretInstance.data.explosionSprite, turretInstance.data.explosionColor, 0.5f);
                                }
                            }
                        }
                    }

                    nextAuraTickTime = Time.time + 0.5f; // tick every 0.5s
                }
            }

            transform.localRotation = Quaternion.identity;
            return;
        }

        // Targeted Attack Types (SingleTarget, AOE, AOESlow, Shotgun)
        float refreshInterval = (turretInstance != null && turretInstance.data != null) ? turretInstance.data.targetRefreshInterval : 0.1f;
        if (Time.time >= nextTargetRefreshTime || !IsValidTarget(currentTarget, stats.range))
        {
            currentTarget = FindNearestTarget(stats.range);
            nextTargetRefreshTime = Time.time + refreshInterval;
        }

        // Aim at the target if active and valid
        if (currentTarget != null)
        {
            Vector3 direction = currentTarget.transform.position - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
        else
        {
            transform.localRotation = Quaternion.identity;
        }

        if (currentTarget == null || Time.time < nextFireTime)
        {
            return;
        }

        FireProjectile(stats);
        nextFireTime = Time.time + 1f / stats.fireRate;
    }

    private int GetCurrentLevel()
    {
        if (parentBlock != null && parentBlock.IsPlaced && parentBlock.AnchorCell != null)
        {
            return parentBlock.AnchorCell.CurrentTotalLevel;
        }
        return turretInstance != null ? turretInstance.level : 1;
    }

    private void FireProjectile(TurretStats stats)
    {
        TriggerMuzzleFlash();

        if (turretInstance == null || turretInstance.data == null) return;

        int currentLevel = GetCurrentLevel();

        bool hasPrefab = turretInstance.data.projectilePrefab != null;
        TurretAttackType type = turretInstance.data.attackType;

        if (type == TurretAttackType.Shotgun)
        {
            // 5. Shotgun Behavior (Spread projectiles in a cone)
            int count = turretInstance.data.GetShotgunPelletCountForLevel(currentLevel);
            float spread = turretInstance.data.shotgunSpreadAngle;

            if (currentTarget != null)
            {
                Vector3 targetDir = (currentTarget.transform.position - transform.position).normalized;
                float baseAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;

                float angleStart = baseAngle - (spread / 2f);
                float angleStep = count > 1 ? spread / (count - 1) : 0f;

                for (int i = 0; i < count; i++)
                {
                    float currentAngle = angleStart + (i * angleStep);
                    Vector3 pelletDir = new Vector3(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad), 0f);

                    if (hasPrefab)
                    {
                        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
                        Projectile proj = Instantiate(turretInstance.data.projectilePrefab, spawnPos, Quaternion.identity);
                        
                        proj.InitializeDirection(
                            pelletDir, 
                            stats.damage, 
                            turretInstance.data.projectileSpeed,
                            isAOE: false, 
                            splashRadius: 0f, 
                            isAOESlow: false, 
                            slowFactor: 1f, 
                            slowDuration: 0f,
                            explosionSprite: turretInstance.data.explosionSprite,
                            explosionColor: turretInstance.data.explosionColor
                        );

                    }
                    else
                    {
                        currentTarget.TakeDamage(stats.damage);
                    }
                }
            }
        }
        else if (type == TurretAttackType.Piercing)
        {
            // 6. Piercing Behavior (Straight line, passes through enemies)
            int pierceCount = turretInstance.data.GetPierceCountForLevel(currentLevel);
            if (currentTarget != null)
            {
                Vector3 targetDir = (currentTarget.transform.position - transform.position).normalized;
                if (hasPrefab)
                {
                    Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
                    Projectile proj = Instantiate(turretInstance.data.projectilePrefab, spawnPos, Quaternion.identity);
                    
                    proj.InitializeDirection(
                        targetDir, 
                        stats.damage, 
                        turretInstance.data.projectileSpeed,
                        isAOE: false, 
                        splashRadius: 0f, 
                        isAOESlow: false, 
                        slowFactor: 1f, 
                        slowDuration: 0f,
                        explosionSprite: turretInstance.data.explosionSprite,
                        explosionColor: turretInstance.data.explosionColor,
                        isPiercing: true,
                        maxPierceCount: pierceCount
                    );
                }
                else
                {
                    currentTarget.TakeDamage(stats.damage);
                }
            }
        }
        else
        {
            // 1, 2, 4. SingleTarget, AOE, or AOESlow Behavior
            bool isAOE = (type == TurretAttackType.AOE);
            bool isAOESlow = (type == TurretAttackType.AOESlow);
            float radius = turretInstance.data.GetSplashRadiusForLevel(currentLevel);
            float slowFact = turretInstance.data.slowFactor;
            float slowDur = turretInstance.data.slowDuration;

            if (hasPrefab)
            {
                Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
                Projectile proj = Instantiate(turretInstance.data.projectilePrefab, spawnPos, Quaternion.identity);
                proj.Initialize(
                    currentTarget, 
                    stats.damage, 
                    turretInstance.data.projectileSpeed, 
                    isAOE: isAOE, 
                    splashRadius: radius, 
                    isAOESlow: isAOESlow, 
                    slowFactor: slowFact, 
                    slowDuration: slowDur,
                    explosionSprite: turretInstance.data.explosionSprite,
                    explosionColor: turretInstance.data.explosionColor
                );

            }
            else
            {
                // Fallback instant-hit damage/slow calculation
                if (isAOE || isAOESlow)
                {
                    float radiusSqr = radius * radius;
                    IReadOnlyList<Enemy> activeEnemies = Enemy.ActiveEnemies;
                    for (int i = 0; i < activeEnemies.Count; i++)
                    {
                        Enemy enemy = activeEnemies[i];
                        if (enemy != null && enemy.IsAlive)
                        {
                            if (((Vector2)enemy.transform.position - (Vector2)currentTarget.transform.position).sqrMagnitude <= radiusSqr)
                            {
                                if (isAOE) enemy.TakeDamage(stats.damage);
                                if (isAOESlow) enemy.ApplySlow(slowFact, slowDur);
                            }
                        }
                    }

                    if (!isAOE && currentTarget != null)
                    {
                        currentTarget.TakeDamage(stats.damage);
                    }
                }
                else
                {
                    if (currentTarget != null)
                    {
                        currentTarget.TakeDamage(stats.damage);
                    }
                }
            }
        }
    }

    private void TriggerMuzzleFlash()
    {
        if (muzzleFlashVisual != null)
        {
            if (turretInstance != null && turretInstance.data != null)
            {
                SpriteRenderer sr = muzzleFlashVisual.GetComponent<SpriteRenderer>();
                if (sr == null)
                {
                    sr = muzzleFlashVisual.GetComponentInChildren<SpriteRenderer>();
                }
                if (sr != null)
                {
                    sr.color = turretInstance.data.explosionColor;
                }
            }

            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(FlashRoutine());
        }
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        muzzleFlashVisual.SetActive(true);
        yield return new WaitForSeconds(flashDuration);
        muzzleFlashVisual.SetActive(false);
        flashCoroutine = null;
    }

    private Enemy FindNearestTarget(float range)
    {
        Enemy nearestTarget = null;
        float rangeSqr = range * range;
        float nearestDistanceSqr = float.MaxValue;

        IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (!IsValidTarget(enemy, range))
            {
                continue;
            }

            float distanceSqr = ((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr && distanceSqr <= rangeSqr)
            {
                nearestTarget = enemy;
                nearestDistanceSqr = distanceSqr;
            }
        }

        return nearestTarget;
    }

    private bool IsValidTarget(Enemy enemy, float range)
    {
        if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsAlive)
        {
            return false;
        }

        return ((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude <= range * range;
    }

    private void OnDrawGizmos()
    {
        if (!drawRangeGizmo || turretInstance == null || turretInstance.data == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        // In edit mode or unplaced, just draw base range
        float r = turretInstance.data.baseStats.range;
        if (parentBlock != null && parentBlock.IsPlaced && parentBlock.AnchorCell != null)
        {
            TruckGrid grid = parentBlock.AnchorCell.Grid;
            if (grid != null)
            {
                GridCell myCell = grid.GetCell(
                    parentBlock.AnchorCell.GridX + turretInstance.localPosition.x, 
                    parentBlock.AnchorCell.GridY + turretInstance.localPosition.y
                );
                if (myCell != null) r = myCell.EffectiveStats.range;
            }
        }

        Gizmos.DrawWireSphere(transform.position, r);
    }
}

public class AuraVisualController : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Coroutine currentAnimation;
    private float currentRadius;

    public void Initialize(Color explosionColor)
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateCircleSprite(512, explosionColor);
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = -6;

        transform.localScale = Vector3.zero;
        transform.localPosition = Vector3.zero;
    }

    public void Show(float radius, float duration = 0.2f)
    {
        currentRadius = radius;
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        
        Vector3 targetScale = new Vector3(radius * 2f, radius * 2f, 1f);
        currentAnimation = StartCoroutine(AnimateScale(transform.localScale, targetScale, duration));
    }

    public void Hide(bool immediate = false)
    {
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        
        if (immediate)
        {
            transform.localScale = Vector3.zero;
        }
        else
        {
            currentAnimation = StartCoroutine(AnimateScale(transform.localScale, Vector3.zero, 0.2f));
        }
    }

    private System.Collections.IEnumerator AnimateScale(Vector3 start, Vector3 target, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }
        transform.localScale = target;
    }

    private Sprite CreateCircleSprite(int resolution, Color color)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        Color[] colors = new Color[resolution * resolution];
        float center = resolution / 2f;
        float radius = resolution / 2f;
        float thickness = 4f; // 픽셀 단위 얇은 테두리 두께

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = 0f;
                if (dist <= radius - thickness)
                {
                    alpha = 0.3f;
                }
                else if (dist <= radius)
                {
                    alpha = 1.0f;
                }
                colors[y * resolution + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }
}

