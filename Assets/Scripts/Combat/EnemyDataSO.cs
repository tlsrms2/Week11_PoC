using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Combat/Enemy Data", order = 1)]
public class EnemyDataSO : ScriptableObject
{
    [Header("Visual & Identification")]
    public string enemyName = "Zombie";
    public Sprite enemySprite;
    public Enemy enemyPrefab;

    [Header("Base Stats")]
    public float maxHealth = 30f;
    public float moveSpeed = 1.5f;

    [Header("Combat Stats")]
    public float contactDamage = 8f;
    public float attackInterval = 1f;

    [Header("Drop Rewards")]
    public int currencyReward = 5;
    [Range(0f, 1f)]
    public float blockDropChance = 0.2f;
}
