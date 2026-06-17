using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class FireBullet : BaseMechanism
{
    Vector3 drowPos;
    bool isDisable = false;
    protected override void Start()
    {
        base.Start();
        drowPos = transform.position;
    }
    private void OnEnable()
    {
        if (isDisable)
            transform.position = drowPos;
        isDisable = false;
    }
    private void OnCollisionEnter2D(Collision2D c)
    {
        if (c.collider.CompareTag("Player") || c.collider.CompareTag("NetPlayer"))
        {
            gameObject.SetActive(false);
            isDisable = true;
            GameReferee.instance.AddOneDeatroyCache(gameObject);
            EventDispatcher.PostEvent(MessageEvent.PlayerGetWeapon, this, GameManager.Instance.userName, PlayerWeaponType.Fireball);
        }
    }
}
}
