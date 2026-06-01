using UnityEngine;

public enum DamageType
{
    None = 0,
    Bomb,
    Fall,
    Fireball,
    Enermy,
    Boss,
}
public struct DamageData
{
    public int damageAmount;
    public Vector2 damageSource;
    public DamageType damageType;
    public DamageData(int amount, Vector2 source, DamageType type)
    {
        damageAmount = amount;
        damageSource = source;
        damageType = type;
    }
}
public interface IDamageable
{
    void TakeDamage(DamageData damage);
    bool IsAlive();
    GameObject GetGameObject();
}