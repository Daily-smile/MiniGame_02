using UnityEngine;

public class ReactState : IState
{
    private FSM manager;

    private AnimatorStateInfo info;
    private string anim_name;

    public ReactState(FSM manager)
    {
        this.manager = manager;
        this.anim_name = manager.GetStateAnimName(EnermyStateType.React);
    }

    public void OnEnter()
    {
        manager.parameter.moveFrameInput.x = 0;
        manager.parameter.animator.Play(anim_name);
    }

    public void OnExist()
    {

    }

    public void OnUpdate()
    {
        info = manager.parameter.animator.GetCurrentAnimatorStateInfo(0);
        
        if (manager.parameter.getHit)
        {
            manager.TransitionState(EnermyStateType.Hit);
        }
        if (info.normalizedTime >= 0.95f)
        {
            if (manager.parameter.target != null)
                manager.TransitionState(EnermyStateType.Chase);
            else
                manager.TransitionState(EnermyStateType.Patrol);
        }
    }
}