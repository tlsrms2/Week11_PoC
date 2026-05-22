using UnityEngine;

[System.Serializable]
public struct TurretStats
{
    [Min(0f)] public float damage;
    [Min(0f)] public float fireRate;
    [Min(0f)] public float range;

    public TurretStats(float damage, float fireRate, float range)
    {
        this.damage = damage;
        this.fireRate = fireRate;
        this.range = range;
    }

    public static TurretStats Zero => new TurretStats(0f, 0f, 0f);

    public TurretStats Scaled(float damageScale, float fireRateScale, float rangeScale)
    {
        return new TurretStats(
            damage * damageScale,
            fireRate * fireRateScale,
            range * rangeScale);
    }
}
