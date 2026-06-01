using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WinDoor : BaseMechanism
{
    public override void TriggerEnter(Collider2D c)
    {
        if (c.transform.CompareTag("Player"))
        {
            EventDispatcher.PostEvent(MessageEvent.GameWin, this, GameManager.Instance.userName);
        }
        else if (c.transform.CompareTag("NetPlayer"))
        {
            MirrorPlayer mirrorPlayer = c.transform.GetComponent<MirrorPlayer>();
            if (mirrorPlayer != null)
                EventDispatcher.PostEvent(MessageEvent.GameWin, this, mirrorPlayer.playerName);
        }
    }
}
