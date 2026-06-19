using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
    /// 跳跃攻击状态
/// </summary>
public class JumpAttackBossState : BossState
{
    public JumpAttackBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.JumpAttack);
        FlipTowardsPlayer();
    }

    public override void OnUpdate()
    {
        // 等待落地后切回追击状态
        if (boss.IsGrounded())
        {
            boss.stateMachine.ChangeState(typeof(ChaseBossState));
        }
    }

    public override void OnExist() { }
}
}