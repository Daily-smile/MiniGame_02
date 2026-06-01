using UnityEngine;

/// <summary>
/// 受伤状态
/// </summary>
public class HurtBossState : BossState
{
    private float hurtDuration = 0.5f;
    private float hurtTimer;

    public HurtBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.Hurt);
        hurtTimer = hurtDuration;

        // 受伤时短暂停顿
        rb.velocity = new Vector2(0, rb.velocity.y);
    }

    public override void OnUpdate()
    {
        hurtTimer -= Time.deltaTime;

        if (hurtTimer <= 0)
        {
            // 检查是否应该进入眩晕状态
            if (boss.dizzyTimer > 0)
            {
                boss.stateMachine.ChangeState(typeof(DizzyBossState));
            }
            else
            {
                boss.stateMachine.ChangeState(typeof(ChaseBossState));
            }
        }
    }

    public override void OnExist() { }
}