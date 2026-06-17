using System.Collections;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class DeathBossState : BossState
{
    public DeathBossState(BossAI boss) : base(boss) { }

    public override void OnEnter()
    {
        animator.SetInteger("State", (int)BossStateType.Die);
        rb.velocity = Vector2.zero;
        boss.bossCollider.enabled = false;

        // 启动死亡后处理协程
        boss.StartCoroutine(AfterDeath());
    }

    public override void OnUpdate() { }

    public override void OnExist() { }

    private IEnumerator AfterDeath()
    {
        yield return new WaitForSeconds(3f);
        // 游戏胜利或其他处理
        // GameManager.Instance.WinGame();

        // 销毁Boss
        UnityEngine.Object.Destroy(boss.gameObject);
    }
}
}