using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class PatrolState : IState
{
    private FSM manager;

    private int patrolPosition;
    private string anim_name;

    private RaycastHit2D leftColliderHit;
    private RaycastHit2D rightColliderHit;

    public PatrolState(FSM manager)
    {
        this.manager = manager;
        this.anim_name = manager.GetStateAnimName(EnermyStateType.Patrol);
    }

    public void OnEnter()
    {
        manager.parameter.animator.Play(anim_name);
    }

    public void OnExist()
    {
        patrolPosition++;
        if (patrolPosition >= manager.parameter.patrolPoints.Length)
        {
            patrolPosition = 0;
        }
    }

    public void OnUpdate()
    {
        if (manager.parameter.getHit)
        {
            manager.TransitionState(EnermyStateType.Hit);
        }
        if (manager.parameter.target != null)
        {
            manager.TransitionState(EnermyStateType.Chase);
        }
        else
        {
            leftColliderHit = Physics2D.BoxCast(manager.origin.position, manager.originSize, 0, Vector2.left, .1f, ~manager.layerMask);
            rightColliderHit = Physics2D.BoxCast(manager.origin.position, manager.originSize, 0, Vector2.right, .1f, ~manager.layerMask);
            if (leftColliderHit.collider != null && !leftColliderHit.collider.isTrigger && leftColliderHit.collider.transform != manager.transform && leftColliderHit.collider.transform != manager.origin)
            {
                manager.parameter.chasePoints[1] = new Vector3(manager.origin.position.x + Mathf.Abs(manager.parameter.chasePoints[0].x - manager.parameter.patrolPoints[0].x) + Mathf.Abs(manager.parameter.chasePoints[0].x - manager.parameter.chasePoints[1].x), manager.origin.position.y, 0);
                manager.parameter.chasePoints[0] = new Vector3(manager.origin.position.x + Mathf.Abs(manager.parameter.chasePoints[0].x - manager.parameter.patrolPoints[0].x), manager.origin.position.y, 0);
                manager.parameter.patrolPoints[1] = new Vector3(manager.origin.position.x + Mathf.Abs(manager.parameter.patrolPoints[0].x - manager.parameter.patrolPoints[1].x), manager.origin.position.y, 0);
                manager.parameter.patrolPoints[0] = manager.origin.position;
            }
            else if (rightColliderHit.collider != null && !rightColliderHit.collider.isTrigger && rightColliderHit.collider.transform != manager.transform && rightColliderHit.collider.transform != manager.origin)
            {
                manager.parameter.chasePoints[0] = new Vector3(manager.origin.position.x - Mathf.Abs(manager.parameter.chasePoints[1].x - manager.parameter.patrolPoints[1].x) - Mathf.Abs(manager.parameter.chasePoints[0].x - manager.parameter.chasePoints[1].x), manager.origin.position.y, 0);
                manager.parameter.chasePoints[1] = new Vector3(manager.origin.position.x - Mathf.Abs(manager.parameter.chasePoints[1].x - manager.parameter.patrolPoints[1].x), manager.origin.position.y, 0);
                manager.parameter.patrolPoints[0] = new Vector3(manager.origin.position.x - Mathf.Abs(manager.parameter.patrolPoints[0].x - manager.parameter.patrolPoints[1].x), manager.origin.position.y, 0);
                manager.parameter.patrolPoints[1] = manager.origin.position;
            }
            manager.FlipTo(manager.parameter.patrolPoints[patrolPosition]);
            manager.parameter.moveFrameInput.x = Mathf.MoveTowards(manager.parameter.moveFrameInput.x, manager.GetPositiveDirect(manager.origin.position.x, manager.parameter.patrolPoints[patrolPosition].x) * manager.parameter.moveSpeed, 7 * Time.deltaTime);
        }
        if (Mathf.Abs(manager.origin.position.x - manager.parameter.patrolPoints[patrolPosition].x) < .1f)
        {
            manager.TransitionState(EnermyStateType.Idle);
        }
    }
}
}