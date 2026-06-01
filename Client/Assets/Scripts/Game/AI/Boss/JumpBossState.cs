using UnityEngine;

/// <summary>
/// 跳跃状态
/// </summary>
public class JumpBossState : BossState
{
    public JumpBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.Jump);
        boss.jumpTimer = boss.jumpCooldown;

        // 跳跃逻辑
        rb.AddForce(new Vector2(0, 10f), ForceMode2D.Impulse);
    }

    public override void OnUpdate()
    {
        // 检测是否在空中，如果是则转换为跳跃攻击状态
        if (!boss.IsGrounded())
        {
            boss.stateMachine.ChangeState(typeof(JumpAttackBossState));
        }
    }

    public override void OnExist() { }
}
