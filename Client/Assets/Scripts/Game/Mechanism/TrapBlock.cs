using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrapBlock : BaseMechanism, IDamageable
{
    private int hp = 1;
    public GameObject GetGameObject()
    {
        return gameObject;
    }

    public bool IsAlive()
    {
        return hp > 0;
    }

    public void TakeDamage(DamageData damage)
    {
        hp = Mathf.Max(hp - damage.damageAmount, 0);
        if (hp <= 0)
        {
            gameObject.SetActive(false);
        }
    }

    public override void TriggerEnter(Collider2D c)
    {
        if (c.gameObject.CompareTag("Player"))
        {
            EventDispatcher.PostEvent(MessageEvent.PlayerOnHit, this, GameManager.Instance.userName);
        }
    }

    public override void TriggerStay(Collider2D c)
    {
        if (c.gameObject.CompareTag("Player"))
        {
            EventDispatcher.PostEvent(MessageEvent.PlayerOnHit, this, GameManager.Instance.userName);
        }
    }
}
