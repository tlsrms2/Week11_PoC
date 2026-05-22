using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Enemy target;
    private float damage;
    private float speed = 15f;
    private bool isInitialized;

    // AOE / Slow settings
    private bool isAOE;
    private float splashRadius;
    private bool isAOESlow;
    private float slowFactor;
    private float slowDuration;

    // Straight-line movement settings
    private bool isTracking = true;
    private Vector3 moveDirection;
    private float lifeTime = 2f;

    // Explosion Effect settings
    private Sprite explosionSprite;
    private Color explosionColor;

    // Piercing Settings
    private bool isPiercing;
    private int maxPierceCount;
    private int currentPierceCount;
    private HashSet<Enemy> hitEnemies;

    public void Initialize(Enemy target, float damage, float speed, 
        bool isAOE = false, float splashRadius = 0f, 
        bool isAOESlow = false, float slowFactor = 1f, float slowDuration = 0f,
        Sprite explosionSprite = null, Color explosionColor = default)
    {
        this.target = target;
        this.damage = damage;
        this.speed = speed;
        this.isAOE = isAOE;
        this.splashRadius = splashRadius;
        this.isAOESlow = isAOESlow;
        this.slowFactor = slowFactor;
        this.slowDuration = slowDuration;
        this.explosionSprite = explosionSprite;
        this.explosionColor = explosionColor;
        this.isTracking = true;
        this.isInitialized = true;

        // Optionally, rotate projectile to face target initially
        if (target != null)
        {
            Vector3 direction = target.transform.position - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    public void InitializeDirection(Vector3 direction, float damage, float speed, 
        bool isAOE = false, float splashRadius = 0f, 
        bool isAOESlow = false, float slowFactor = 1f, float slowDuration = 0f,
        Sprite explosionSprite = null, Color explosionColor = default,
        bool isPiercing = false, int maxPierceCount = 1)
    {
        this.target = null;
        this.damage = damage;
        this.speed = speed;
        this.isAOE = isAOE;
        this.splashRadius = splashRadius;
        this.isAOESlow = isAOESlow;
        this.slowDuration = slowDuration;
        this.explosionSprite = explosionSprite;
        this.explosionColor = explosionColor;
        this.moveDirection = direction.normalized;
        this.isTracking = false;
        this.lifeTime = 2f;

        this.isPiercing = isPiercing;
        this.maxPierceCount = maxPierceCount;
        this.currentPierceCount = 0;
        if (this.hitEnemies == null) this.hitEnemies = new HashSet<Enemy>();
        else this.hitEnemies.Clear();

        this.isInitialized = true;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void Update()
    {
        if (!isInitialized) return;

        if (isTracking)
        {
            // If target dies while projectile is in flight, destroy projectile
            if (target == null || !target.IsAlive || !target.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 direction = target.transform.position - transform.position;
            float distanceThisFrame = speed * Time.deltaTime;

            if (direction.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
            {
                HitTarget(true);
                return;
            }

            transform.position += direction.normalized * distanceThisFrame;
            
            // Update rotation to always face the moving target
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
        else
        {
            // Straight-line movement
            float distanceThisFrame = speed * Time.deltaTime;
            transform.position += moveDirection * distanceThisFrame;

            // Simple OverlapCircle check using ActiveEnemies list
            float hitRadius = 0.25f;
            float hitRadiusSqr = hitRadius * hitRadius;
            IReadOnlyList<Enemy> activeEnemies = Enemy.ActiveEnemies;
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                Enemy enemy = activeEnemies[i];
                if (enemy != null && enemy.IsAlive)
                {
                    if (isPiercing && hitEnemies.Contains(enemy)) continue;

                    if ((enemy.transform.position - transform.position).sqrMagnitude <= hitRadiusSqr)
                    {
                        if (isPiercing)
                        {
                            hitEnemies.Add(enemy);
                            this.target = enemy;
                            HitTarget(false);
                            currentPierceCount++;
                            if (currentPierceCount >= maxPierceCount)
                            {
                                Destroy(gameObject);
                                return;
                            }
                        }
                        else
                        {
                            this.target = enemy;
                            HitTarget(true);
                            return;
                        }
                    }
                }
            }

            lifeTime -= Time.deltaTime;
            if (lifeTime <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }

    private void HitTarget(bool destroyOnHit = true)
    {
        if (explosionSprite != null)
        {
            GameObject effectObj = new GameObject("ExplosionEffect");
            Vector3 hitPos = target != null ? target.transform.position : transform.position;
            effectObj.transform.position = hitPos;
            
            ExplosionEffect effect = effectObj.AddComponent<ExplosionEffect>();
            float scale = (isAOE || isAOESlow) ? splashRadius * 2f : 0.6f;
            effect.Initialize(explosionSprite, explosionColor, scale);
        }

        if (isAOE || isAOESlow)
        {
            float radiusSqr = splashRadius * splashRadius;
            Vector3 hitPos = target != null ? target.transform.position : transform.position;

            IReadOnlyList<Enemy> activeEnemies = Enemy.ActiveEnemies;
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                Enemy enemy = activeEnemies[i];
                if (enemy != null && enemy.IsAlive)
                {
                    if ((enemy.transform.position - hitPos).sqrMagnitude <= radiusSqr)
                    {
                        if (isAOE)
                        {
                            enemy.TakeDamage(damage);
                        }
                        if (isAOESlow)
                        {
                            enemy.ApplySlow(slowFactor, slowDuration);
                        }
                    }
                }
            }

            // Ensure the main target takes base damage if AOE damage wasn't active
            if (!isAOE && target != null)
            {
                target.TakeDamage(damage);
            }
        }
        else
        {
            if (target != null)
            {
                target.TakeDamage(damage);
            }
        }
        
        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }
}