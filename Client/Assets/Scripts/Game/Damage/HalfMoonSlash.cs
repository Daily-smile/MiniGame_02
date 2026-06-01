using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HalfMoonSlash : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D c)
    {
        if (c.CompareTag("Player") || c.CompareTag("NetPlayer"))
        {
            EventDispatcher.PostEvent(MessageEvent.PlayerOnHit, this, GameManager.Instance.userName, false);
        }
    }
}
