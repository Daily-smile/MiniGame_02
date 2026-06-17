using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class Fireball : MonoBehaviour, IProjectile
{
    public float horizontalSpeed = 13f;
    public float frequency = 30f; // ����Ƶ��
    public float amplitude = 0.12f; // ��������

    private float timeElapsed;
    private Vector2 initialPosition;
    private int rightDirect;
    private Rigidbody2D rb;
    public void Initialized(bool isRightDirect)
    {
        this.rightDirect = isRightDirect ? 1 : -1;
        initialPosition = transform.position;
        timeElapsed = 0;
    }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        rb.simulated = true;
    }

    private void OnDisable()
    {
        rb.simulated = false;
    }

    void Update()
    {
        if (Mathf.Abs(initialPosition.x - transform.position.x) > 10)
        {
            ObjectPool.instance.PutInPool(gameObject);
            return;
        }
        timeElapsed += Time.deltaTime;

        float x = initialPosition.x + horizontalSpeed * timeElapsed * rightDirect;
        float y = initialPosition.y + Mathf.Sin(timeElapsed * frequency) * amplitude;

        Vector2 newPos = new Vector2(x, y);
        if (rb != null) rb.MovePosition(newPos);
        else transform.position = newPos;
    }

    private void OnTriggerEnter2D(Collider2D c)
    {
        if (!c.isTrigger && !c.CompareTag("Player") && !c.CompareTag("NetPlayer"))
        {
            if (LayerMask.Equals(c.gameObject.layer, LayerMask.NameToLayer("Enermy")))
            {
                GameReferee.instance.EnermyOnHit(c.transform);
            }
            ObjectPool.instance.PutInPool(gameObject);
        }
    }
}
}
