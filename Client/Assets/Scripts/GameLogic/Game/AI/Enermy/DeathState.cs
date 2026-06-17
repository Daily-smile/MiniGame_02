using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class DeathState : IState
{
    private FSM manager;
    private string anim_name;

    private AnimatorStateInfo animInfo;

    public DeathState(FSM manager)
    {
        this.manager = manager;
        this.anim_name = manager.GetStateAnimName(EnermyStateType.Death);
    }

    public void OnEnter()
    {
        manager.parameter.moveFrameInput.x = 0;
        manager.parameter.animator.Play(anim_name);
        manager.parameter.isDead = true;
    }

    public void OnExist()
    {

    }

    public void OnUpdate()
    {
        animInfo = manager.parameter.animator.GetCurrentAnimatorStateInfo(0);
        if (animInfo.normalizedTime > .95f)
        {
            manager.parameter.rb.simulated = false;
        }
        if (animInfo.normalizedTime > 1.95f)
        {
            manager.gameObject.SetActive(false);
        }
    }
}
}
