public class StrikeBossState : BossState
{
    public StrikeBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.Strike);
        boss.strikeTimer = boss.strikeCooldown;

        // 朝向玩家
        FlipTowardsPlayer();
    }

    public override void OnUpdate()
    {
        // 等待动画完成
        // 动画事件会处理状态转换
    }

    public override void OnExist()
    {
        // 清理工作
    }
}