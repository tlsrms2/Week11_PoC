using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TurretDatabase", menuName = "Turret/TurretDatabase")]
public class TurretDatabaseSO : ScriptableObject
{
    public List<TurretDataSO> allTurrets = new List<TurretDataSO>();

    public TurretDataSO GetRandomTurret()
    {
        if (allTurrets == null || allTurrets.Count == 0) return null;
        return allTurrets[Random.Range(0, allTurrets.Count)];
    }
}
