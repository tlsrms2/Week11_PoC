using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct ShotgunPelletUpgrade
{
    public int minLevel;
    public int bonusPellets;
}

[System.Serializable]
public struct PierceUpgrade
{
    public int minLevel;
    public int bonusPierce;
}

[CreateAssetMenu(fileName = "NewTurretData", menuName = "Turret/TurretData")]
public class TurretDataSO : ScriptableObject
{
    public string turretName = "Turret";
    public TurretStats baseStats = new TurretStats(10f, 1f, 4f);
    
    [Header("Growth Rates")]
    public float damageGrowthPerLevel = 1.5f;
    public float fireRateGrowthPerLevel = 1.1f;
    public float rangeGrowthPerLevel = 1.05f;
    public float splashRadiusGrowthPerLevel = 1.05f;

    public TurretWeapon weaponPrefab;
    public Sprite icon;

    [Header("Weapon & Targeting Settings")]
    public bool attackOnlyDuringDefense = true;
    public float targetRefreshInterval = 0.1f;
    public Projectile projectilePrefab;
    public float projectileSpeed = 15f;

    [Header("Visual Effect Settings")]
    public Sprite explosionSprite;
    public Color explosionColor = Color.white;

    [Header("Attack Mode Settings")]
    public TurretAttackType attackType = TurretAttackType.SingleTarget;

    [Tooltip("Used for AOE and AOESlow types")]
    public float splashRadius = 1.5f;

    [Tooltip("Used for AOESlow type")]
    public float slowFactor = 0.5f;
    public float slowDuration = 2.0f;

    [Tooltip("Used for AuraDamage type")]
    public float auraActiveDuration = 3.0f;
    public float auraRestDuration = 2.0f;

    [Tooltip("Used for Shotgun type")]
    public int shotgunPelletCount = 5;
    public float shotgunSpreadAngle = 30f;
    public List<ShotgunPelletUpgrade> shotgunPelletUpgrades = new List<ShotgunPelletUpgrade>();

    [Tooltip("Used for Piercing type")]
    public int basePierceCount = 3;
    public List<PierceUpgrade> pierceUpgrades = new List<PierceUpgrade>();

    [System.Serializable]
    public struct VisualUpgrade
    {
        public int minLevel;
        public Sprite sprite;
    }

    [Header("Visual Upgrades")]
    public List<VisualUpgrade> visualUpgrades = new List<VisualUpgrade>();

    public Sprite GetSpriteForLevel(int level)
    {
        Sprite currentSprite = icon;
        int maxFoundLevel = 0;
        
        if (visualUpgrades != null)
        {
            for (int i = 0; i < visualUpgrades.Count; i++)
            {
                var upgrade = visualUpgrades[i];
                if (level >= upgrade.minLevel && upgrade.minLevel > maxFoundLevel && upgrade.sprite != null)
                {
                    currentSprite = upgrade.sprite;
                    maxFoundLevel = upgrade.minLevel;
                }
            }
        }
        
        return currentSprite;
    }

    public TurretStats GetStatsForLevel(int level)
    {
        int bonusLevels = Mathf.Max(0, level - 1);
        return baseStats.Scaled(
            Mathf.Pow(damageGrowthPerLevel, bonusLevels),
            Mathf.Pow(fireRateGrowthPerLevel, bonusLevels),
            Mathf.Pow(rangeGrowthPerLevel, bonusLevels)
        );
    }

    public float GetSplashRadiusForLevel(int level)
    {
        int bonusLevels = Mathf.Max(0, level - 1);
        return splashRadius * Mathf.Pow(splashRadiusGrowthPerLevel, bonusLevels);
    }

    public int GetShotgunPelletCountForLevel(int level)
    {
        int count = shotgunPelletCount;
        if (shotgunPelletUpgrades != null)
        {
            for (int i = 0; i < shotgunPelletUpgrades.Count; i++)
            {
                if (level >= shotgunPelletUpgrades[i].minLevel)
                {
                    count += shotgunPelletUpgrades[i].bonusPellets;
                }
            }
        }
        return count;
    }

    public int GetPierceCountForLevel(int level)
    {
        int count = basePierceCount;
        if (pierceUpgrades != null)
        {
            for (int i = 0; i < pierceUpgrades.Count; i++)
            {
                if (level >= pierceUpgrades[i].minLevel)
                {
                    count += pierceUpgrades[i].bonusPierce;
                }
            }
        }
        return count;
    }
}
