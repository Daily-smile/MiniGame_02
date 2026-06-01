using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitState : IState
{
    private FSM manager;
    private string anim_name;

    public HitState(FSM manager)
    {
        this.manager = manager;
        this.anim_name = manager.GetStateAnimName(EnermyStateType.Hit);
    }

    public void OnEnter()
    {
        manager.parameter.moveFrameInput.x = 0;
        manager.parameter.health--;
        manager.parameter.animator.Play(anim_name);
        if (manager.parameter.health <= 0)
        {
            manager.TransitionState(EnermyStateType.Death);
        }
        else
        {
            manager.StartCoroutine(HurtEffect());
        }
    }

    public void OnExist()
    {
        manager.parameter.getHit = false;
    }

    public void OnUpdate()
    {
        manager.parameter.target = GameObject.FindWithTag("Player").transform;
        manager.TransitionState(EnermyStateType.Chase);
    }

    IEnumerator HurtEffect()
    {
        SpriteRenderer spriteRenderer = manager.sprite;
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.red;

        yield return new WaitForSeconds(0.2f);

        spriteRenderer.color = originalColor;
    }
}
