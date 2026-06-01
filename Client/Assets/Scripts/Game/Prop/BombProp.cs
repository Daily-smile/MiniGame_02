using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombProp : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D c)
    {
        if (c.CompareTag("Player") || c.CompareTag("NetPlayer"))
        {
            gameObject.SetActive(false);
            GameReferee.instance.AddOneDeatroyCache(gameObject);
            EventDispatcher.PostEvent(MessageEvent.PlayerGetProp, this, GameManager.Instance.userName, PropType.bomb);
        }
    }
}
