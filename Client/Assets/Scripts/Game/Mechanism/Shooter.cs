using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shooter : BaseMechanism
{
    Animator animator;
    private bool playerLaunchState;
    private int ShootKey = Animator.StringToHash("Shoot");

    protected override void Start()
    {
        animator = GetComponent<Animator>();
        playerLaunchState = false;
        base.Start();
    }

    public override void ColliderEnter(Collision2D c)
    {
        if (c.gameObject.CompareTag("Player"))
        {
            animator.ResetTrigger(ShootKey);
            animator.SetTrigger(ShootKey);
        }
    }

    public override void ColliderStay(Collision2D c)
    {
        if (playerLaunchState)
        {
            if (c.gameObject.CompareTag("Player"))
            {
                IPlayerController player = c.transform.GetComponent<IPlayerController>();
                player.HandleShooterBuffer();
            }
            playerLaunchState = false;
        }
    }

    public void SetPlayerLaunch()
    {
        //Debug.Log("Íæ¼Òµ¯Éä<<<");
        playerLaunchState = true;
    }
}
