using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
/// 蹲伏攻击状态
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
        // 等待动画完成
    }

    public override void OnExist() { }
}
}