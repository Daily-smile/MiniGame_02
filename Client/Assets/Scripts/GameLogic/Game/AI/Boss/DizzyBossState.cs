using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
    /// 眩晕状态
/// </summary>
public class DizzyBossState : BossState
{
    public DizzyBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.Dizzy);
        rb.velocity = new Vector2(0, rb.velocity.y);
    }

    public override void OnUpdate()
    {
        // dizzyTimer 由 BossAI.UpdateTimers() 统一管理,此方法仅检查条件
        if (boss.dizzyTimer <= 0)
        {
            boss.stateMachine.ChangeState(typeof(ChaseBossState));
        }
    }

    public override void OnExist() { }
}
}
