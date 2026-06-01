using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HideWhenHit : BaseMechanism
{
    bool isEnterArea;
    Coroutine playAnim;
    Tilemap tilemap;
    private void Awake()
    {
        tilemap = GetComponent<Tilemap>();
    }

    private void OnDisable()
    {
        if (playAnim != null)
            StopCoroutine(playAnim);
        playAnim = null;
        tilemap.color = Color.white;
    }

    public override void TriggerEnter(Collider2D c)
    {
        if (c.CompareTag("Player") || c.CompareTag("NetPlayer"))
        {
            isEnterArea = true;
            playAnim = StartCoroutine(PlayHideAnim());
        }
    }

    public override void TriggerExit(Collider2D c)
    {
        if (c.CompareTag("Player") || c.CompareTag("NetPlayer"))
        {
            isEnterArea = false;
            StopCoroutine(playAnim);
            tilemap.color = Color.white;
        }
    }

    IEnumerator PlayHideAnim()
    {
        float start = 1f;
        float end = 0f;
        float duration = 2f;
        float t = Time.time;
        float alpha;
        while (true)
        {
            if (!isEnterArea)
            {
                break;
            }
            alpha = (Time.time - t) / duration;
            float currentValue = Mathf.Lerp(start, end, alpha);
            if (alpha >= 1)
            {
                float cur = start;
                start = end;
                end = cur;
                t = Time.time;
            }
            tilemap.color = new Color(1f, 1f, 1f, currentValue);
            yield return new WaitForEndOfFrame();
        }
        playAnim = null;
        tilemap.color = Color.white;
    }
}
