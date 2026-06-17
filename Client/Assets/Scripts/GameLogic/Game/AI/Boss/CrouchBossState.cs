using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
/// 蹲伏状态
/// </summary>
public class CrouchBossState : BossState
{
    private float idleTime;
    private float maxIdleTime = 3f;

    public CrouchBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        idleTime = 0f;
        rb.velocity = new Vector2(0, rb.velocity.y);
        //animator.SetInteger("State", (int)BossStateType.Idle);
        animator.Play("boss_idle");
    }

    public override void OnUpdate()
    {
        idleTime += Time.deltaTime;

        // 空闲一段时间后转为巡逻
        if (idleTime >= maxIdleTime)
        {
            boss.stateMachine.ChangeState(typeof(PatrolBossState));
            return;
        }

        // 检测玩家是否进入追逐范围
        if (IsPlayerInRange(boss.chaseRange))
        {
            boss.stateMachine.ChangeState(typeof(ChaseBossState));
        }
    }

    public override void OnExist()
    {
        // 清理工作
    }
}
}