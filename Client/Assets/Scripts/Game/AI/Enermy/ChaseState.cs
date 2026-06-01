using UnityEngine;

public class ChaseState : IState
{
    private FSM manager;
    private string anim_name;
    private float chaseTimer;
    private const float CHASE_TIMER_IN_ROUND_OUT_VIEW = 1f;

    public ChaseState(FSM manager)
    {
        this.manager = manager;
        this.anim_name = manager.GetStateAnimName(EnermyStateType.Chase);
    }

    public void OnEnter()
    {
        chaseTimer = 0;
        manager.parameter.animator.Play(anim_name);
    }

    public void OnExist()
    {
        
    }

    public void OnUpdate()
    {
        if (manager.parameter.getHit)
        {
            manager.TransitionState(EnermyStateType.Hit);
        }
        chaseTimer += Time.deltaTime;
        if (chaseTimer < CHASE_TIMER_IN_ROUND_OUT_VIEW)
        {
            if (manager.parameter.target == null)
            {
                manager.parameter.target = GameObject.FindWithTag("Player").transform;
            }
        }
        else
        {
            manager.parameter.target = null;
        }
        if (manager.parameter.target != null)
        {
            manager.FlipTo(manager.parameter.target.position);
            manager.parameter.moveFrameInput.x = Mathf.MoveTowards(manager.parameter.moveFrameInput.x, manager.GetPositiveDirect(manager.origin.position.x, manager.parameter.target.position.x) * manager.parameter.moveSpeed, 12 * Time.deltaTime);
        }
        else
        {
            manager.TransitionState(EnermyStateType.Patrol);
        }
        if (Physics2D.OverlapCircle(manager.parameter.attackPoint.position, manager.parameter.attackArea, manager.parameter.targetLayer))
        {
            manager.TransitionState(EnermyStateType.Attack);
        }
    }
}