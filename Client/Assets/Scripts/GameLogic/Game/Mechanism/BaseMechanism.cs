using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public interface IMechanism
{
    void TriggerEnter(Collider2D c);
    void TriggerStay(Collider2D c);
    void TriggerExit(Collider2D c);
    void ColliderEnter(Collision2D c);
    void ColliderStay(Collision2D c);
    void ColliderExit(Collision2D c);
}
public class BaseMechanism : MonoBehaviour, IMechanism
{
    protected virtual void Start()
    {
        EventDispatcher.AddObserver(this, MessageEvent.AgainGame, OnGameReset, null);
    }
    protected virtual void OnDestroy()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.AgainGame, null);
    }

    private bool OnGameReset(params object[] args)
    {
        TriggerReset();
        return false;
    }

    private void OnTriggerEnter2D(Collider2D c)
    {
        TriggerEnter(c);
    }
    private void OnTriggerStay2D(Collider2D c)
    {
        TriggerStay(c);
    }
    private void OnTriggerExit2D(Collider2D c)
    {
        TriggerExit(c);
    }
    private void OnCollisionEnter2D(Collision2D c)
    {
        ColliderEnter(c);
    }
    private void OnCollisionStay2D(Collision2D c)
    {
        ColliderStay(c);
    }
    private void OnCollisionExit2D(Collision2D c)
    {
        ColliderExit(c);
    }

    /// <summary>
    /// 机关触发处理
    /// </summary>
    public virtual void TriggerHandle()
    {
        gameObject.SetActive(false);
    }
    /// <summary>
    /// 机关重置
    /// </summary>
    public virtual void TriggerReset()
    {
        gameObject.SetActive(true);
    }

    public virtual void TriggerEnter(Collider2D c) { }
    public virtual void TriggerStay(Collider2D c) { }
    public virtual void TriggerExit(Collider2D c) { }
    public virtual void ColliderEnter(Collision2D c) { }
    public virtual void ColliderStay(Collision2D c) { }
    public virtual void ColliderExit(Collision2D c) { }
}
}
