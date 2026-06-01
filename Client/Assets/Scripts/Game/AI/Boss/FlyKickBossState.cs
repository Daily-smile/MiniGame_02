using UnityEngine;

public class FlyKickBossState : BossState
{
    private float idleTime;
    private float maxIdleTime = 3f;

    public FlyKickBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        idleTime = 0f;
        animator.SetInteger("State", (int)BossStateType.FlyKick);
        FlipTowardsPlayer();
    }

    public override void OnUpdate()
    {
        idleTime += Time.deltaTime;

        // ����һ��ʱ���תΪѲ��
        if (idleTime >= maxIdleTime)
        {
            boss.stateMachine.ChangeState(typeof(PatrolBossState));
            return;
        }

        // �������Ƿ����׷��Χ
        if (IsPlayerInRange(boss.chaseRange))
        {
            boss.stateMachine.ChangeState(typeof(ChaseBossState));
        }
    }

    public override void OnExist()
    {
        // ��������
    }
}