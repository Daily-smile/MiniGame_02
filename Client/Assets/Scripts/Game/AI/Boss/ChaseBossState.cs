using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChaseBossState : BossState
{
    public ChaseBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.Chase);
    }

    public override void OnUpdate()
    {
        // �������Ƿ񳬳�׷��Χ
        if (!IsPlayerInRange(boss.chaseRange))
        {
            boss.stateMachine.ChangeState(typeof(PatrolBossState));
            return;
        }

        // ����������ʽ
        if (IsPlayerInRange(boss.attackRange))
        {
            ChooseAttack();
            return;
        }

        // ׷�����
        Vector2 moveDirection = (player.position - boss.transform.position).normalized;
        rb.velocity = new Vector2(moveDirection.x * boss.chaseSpeed, rb.velocity.y);

        // ���³���
        FlipTowardsPlayer();
    }

    private void ChooseAttack()
    {
        // ���ݽ׶β�ͬ��������ʽ���ʲ�ͬ
        List<System.Type> possibleAttacks = new List<System.Type>();

        // ����������ʽ
        if (boss.strikeTimer <= 0) possibleAttacks.Add(typeof(StrikeBossState));
        if (boss.attackTimer <= 0) possibleAttacks.Add(typeof(AttackBossState));

        // �׶�2�����Ĺ�����ʽ
        if (boss.currentPhase >= 2)
        {
            if (boss.jumpTimer <= 0) possibleAttacks.Add(typeof(JumpBossState));
            if (boss.crouchTimer <= 0) possibleAttacks.Add(typeof(CrouchBossState));
        }

        // �׶�3�����Ĺ�����ʽ
        if (boss.currentPhase >= 3)
        {
            if (boss.flyKickTimer <= 0) possibleAttacks.Add(typeof(FlyKickBossState));
        }

        // ���ѡ��һ�ֹ�����ʽ
        if (possibleAttacks.Count > 0)
        {
            int randomIndex = Random.Range(0, possibleAttacks.Count);
            boss.stateMachine.ChangeState(possibleAttacks[randomIndex]);
        }
    }

    public override void OnExist()
    {
        // ��������
    }
}