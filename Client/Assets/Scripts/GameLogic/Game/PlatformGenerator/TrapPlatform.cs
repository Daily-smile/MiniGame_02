using System.Collections;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class TrapPlatform : MonoBehaviour
{
    public enum TrapType { TimedDisappear, Blinking, OneTimeLand }
    public TrapType trapType;
    public float interval = 2f;
    public float duration = 1f;

    private Collider2D col;
    private SpriteRenderer[] rend;
    private bool isActive = true;
    private PlatformController controller;

    public void Init(PlatformController ctrl)
    {
        controller = ctrl;
        col = GetComponent<Collider2D>();
        rend = GetComponentsInChildren<SpriteRenderer>();
        // 闅忔満閫夋嫨闄烽槺绫诲瀷
        trapType = (TrapType)Random.Range(0, 3);
        if (trapType == TrapType.TimedDisappear)
        {
            InvokeRepeating(nameof(Toggle), interval, interval);
        }
        else if (trapType == TrapType.Blinking)
        {
            StartCoroutine(BlinkRoutine());
        }
        // OneTime 绫诲瀷閫氳繃纰版挒瑙﹀彂
    }

    void Toggle()
    {
        isActive = !isActive;
        if (col != null) col.enabled = isActive;
        if (rend != null)
        {
            for (int i = 0; i < rend.Length; i++)
            {
                rend[i].enabled = isActive;
            }
        }
    }

    IEnumerator BlinkRoutine()
    {
        float dur = Random.Range(1f, 3f);
        float timer = 0f;
        while (true)
        {
            if (timer <= dur)
            {
                timer += Time.deltaTime;
                if (rend != null)
                {
                    for (int i = 0; i < rend.Length; i++)
                    {
                        //rend[i].enabled = isActive;
                        if (!isActive)
                            rend[i].color = Color.Lerp(Color.white, new Color(1, 1, 1, 0.2f), timer / dur);
                        else
                            rend[i].color = Color.Lerp(new Color(1, 1, 1, 0.2f), Color.white, timer / dur);
                    }
                }
            }
            else
            {
                dur = Random.Range(1f, 3f);
                timer = 0f;
                isActive = !isActive;
                if (col != null) col.enabled = isActive;
            }
            yield return null;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (trapType == TrapType.OneTimeLand && (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("NetPlayer")))
        {
            StartCoroutine(OneTimeDisappear());
        }
    }

    IEnumerator OneTimeDisappear()
    {
        float dur = 0;
        while (dur <= duration)
        {
            dur += Time.deltaTime;
            if (rend != null)
            {
                for (int i = 0; i < rend.Length; i++)
                {
                    rend[i].color = Color.Lerp(Color.white, Color.red, dur / duration);
                }
            }
            yield return null;
        }
        gameObject.SetActive(false);
    }

    public void Reset()
    {
        StopAllCoroutines();
        isActive = true;
        if (col != null) col.enabled = true;
        if (rend != null)
        {
            for (int i = 0; i < rend.Length; i++)
            {
                rend[i].enabled = true;
                rend[i].color = Color.white;
            }
        }
        gameObject.SetActive(true);
    }
}
}