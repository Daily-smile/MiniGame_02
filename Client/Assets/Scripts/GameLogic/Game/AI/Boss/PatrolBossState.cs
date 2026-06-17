using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class PatrolBossState : BossState
{
    private Vector2 patrolCenter;
    private float patrolDirection = 1f;

    public PatrolBossState(BossAI boss) : base(boss)
    {
        patrolCenter = boss.transform.position;
    }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.Patrol);
        patrolDirection = Random.Range(0, 2) == 0 ? -1f : 1f;
    }

    public override void OnUpdate()
    {
        // �������Ƿ����׷��Χ
        if (IsPlayerInRange(boss.chaseRange))
        {
            boss.stateMachine.ChangeState(typeof(ChaseBossState));
            return;
        }

        // Ѳ���߼�
        float targetX = patrolCenter.x + patrolDirection * boss.patrolRange;
        float moveDirection = Mathf.Sign(targetX - boss.transform.position.x);

        rb.velocity = new Vector2(moveDirection * boss.patrolSpeed, rb.velocity.y);

        // ���³���
        if (moveDirection > 0 && !boss.facingRight) boss.Flip();
        else if (moveDirection < 0 && boss.facingRight) boss.Flip();

        // ����Ƿ񵽴�Ѳ�߽߱�
        if (Mathf.Abs(boss.transform.position.x - patrolCenter.x) >= boss.patrolRange)
        {
            patrolDirection *= -1f;
        }
    }

    public override void OnExist()
    {
        // ��������
    }
}
}