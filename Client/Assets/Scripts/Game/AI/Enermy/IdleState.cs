using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleState : IState
{
    private FSM manager;

    private float timer;
    private string anim_name;

    public IdleState(FSM manager)
    {
        this.manager = manager;
        this.anim_name = manager.GetStateAnimName(EnermyStateType.Idle);
    }

    public void OnEnter()
    {
        manager.parameter.moveFrameInput.x = 0;
        manager.parameter.animator.Play(anim_name);
    }

    public void OnExist()
    {
        timer = 0;
    }

    public void OnUpdate()
    {
        timer += Time.deltaTime;
        if (manager.parameter.getHit)
        {
            manager.TransitionState(EnermyStateType.Hit);
        }
        if (manager.parameter.target != null && manager.parameter.target.position.x >= manager.parameter.chasePoints[0].x && manager.parameter.target.position.x <= manager.parameter.chasePoints[1].x)
        {
            manager.TransitionState(EnermyStateType.React);
        }
        if (timer >= manager.parameter.idleTime)
        {
            manager.TransitionState(EnermyStateType.Patrol);
        }
    }
}