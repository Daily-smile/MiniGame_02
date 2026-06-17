using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class Star : BaseMechanism
{
    public override void TriggerEnter(Collider2D c)
    {
        if (c.transform.CompareTag("Player"))
        {
            EventDispatcher.PostEvent(MessageEvent.InGameGetStar, this, GameManager.Instance.userName);
            TriggerHandle();
        }
        else if (c.transform.CompareTag("NetPlayer"))
        {
            EventDispatcher.PostEvent(MessageEvent.InGameGetStar, this, c.transform.GetComponent<MirrorPlayer>().playerName);
            TriggerHandle();
        }
    }

    public override void TriggerReset()
    {
        if (GameManager.Instance.gameModel == GameManager.GameModel.Double || GameManager.Instance.gameModel == GameManager.GameModel.Team)
        {
            return;
        }
        base.TriggerReset();
    }
}
}
