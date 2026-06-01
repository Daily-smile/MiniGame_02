using UnityEngine;

/// <summary>
/// Boss状态基类
/// </summary>
public abstract class BossState : IState
{
    protected BossAI boss;
    protected Animator animator;
    protected Rigidbody2D rb;
    protected Transform player;

    public BossState(BossAI boss)
    {
        this.boss = boss;
        this.animator = boss.GetComponent<Animator>();
        this.rb = boss.GetComponent<Rigidbody2D>();
        this.player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    public abstract void OnEnter();
    public abstract void OnUpdate();
    public abstract void OnExist();

    // 通用方法
    protected void FlipTowardsPlayer()
    {
        if (player.position.x > boss.transform.position.x && !boss.facingRight)
        {
            boss.Flip();
        }
        else if (player.position.x < boss.transform.position.x && boss.facingRight)
        {
            boss.Flip();
        }
    }

    protected bool IsPlayerInRange(float range)
    {
        return Vector2.Distance(boss.transform.position, player.position) <= range;
    }
}