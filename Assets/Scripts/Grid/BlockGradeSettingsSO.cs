using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GradeSetting
{
    public string gradeName;
    [Range(0f, 1f)] public float dropProbability;
    
    public int minSize = 2;
    public int maxSize = 3;
    
    public int minTurrets = 1;
    public int maxTurrets = 4;
    
    public int minLevel = 1;
    public int maxLevel = 2;
    
    public Color gradeColor = Color.white;
}

[CreateAssetMenu(fileName = "BlockGradeSettings", menuName = "Turret/BlockGradeSettings")]
public class BlockGradeSettingsSO : ScriptableObject
{
    public List<GradeSetting> grades = new List<GradeSetting>();
    public TurretDatabaseSO turretDatabase;
}
