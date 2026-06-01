/// <summary>
/// ��Ծ����״̬
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
        // �ȴ�������ɻ����
        if (boss.IsGrounded())
        {
            boss.stateMachine.ChangeState(typeof(ChaseBossState));
        }
    }

    public override void OnExist() { }
}