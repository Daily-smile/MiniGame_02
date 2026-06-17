using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
/// 攻击状态
/// </summary>
public class AttackBossState : BossState
{
    public AttackBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.Attack);
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