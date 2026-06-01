using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ����������
/// </summary>
public class DeathDetection : BaseMechanism
{
    private void OnCollisionEnter2D(Collision2D c)
    {
        if (c.gameObject.CompareTag("Player"))
        {
            EventDispatcher.PostEvent(MessageEvent.PlayerOnHit, this, GameManager.Instance.userName, true);
        }
        else
        {
            c.gameObject.SetActive(false);
        }
    }
}
