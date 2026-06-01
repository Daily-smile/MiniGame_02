/// <summary>
/// ¶×·ü¹¥»÷×´Ì¬
/// </summary>
public class CrouchAttackBossState : BossState
{
    public CrouchAttackBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.CrouchAttack);
        boss.attackTimer = boss.attackCooldown;
        FlipTowardsPlayer();
    }

    public override void OnUpdate()
    {
        // µÈ´ý¶¯»­Íê³É
    }

    public override void OnExist() { }
}