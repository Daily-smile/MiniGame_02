using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MessagePanel : BasePanel
{
    public class MessageData
    {
        public float showTime;
        public string text;
        public MessageData(string str)
        {
            showTime = 3;
            text = str;
        }
        public void Refresh()
        {
            showTime -= Time.deltaTime;
        }
    }

    private GridLayoutGroup gridLayout;
    private List<Transform> uiTextCache;
    private List<MessageData> msgList;
    private Coroutine showMsgTask;
    private Transform topUI;

    public override void Init()
    {
        panelType = UIPanelType.Message;
        gridLayout = transform.GetComponent<GridLayoutGroup>();
        msgList = new List<MessageData>();
        uiTextCache = new List<Transform>();
        foreach (Transform item in transform)
        {
            item.gameObject.SetActive(false);
            uiTextCache.Add(item);
        }
        topUI = UIManager.instance.canvas.transform.Find("TopUI");
        EventDispatcher.AddObserver(this, MessageEvent.OnShowUIMessage, OnReceiveUIMessage, null);
    }
    public override void Dispose()
    {
        StopCoroutine(showMsgTask);
        showMsgTask = null;
        msgList = null;
        uiTextCache = null;
        EventDispatcher.RemoveObserver(this, MessageEvent.OnShowUIMessage, null);
    }

    public override void StartUI()
    {
        base.StartUI();
        gridLayout.cellSize = new Vector2(UIManager.instance.canvas.GetComponent<RectTransform>().sizeDelta.x, gridLayout.cellSize.y);
        showMsgTask = StartCoroutine(ShowMsg());
    }

    public override void OnEnter(Action<BasePanel> callback = null)
    {
        base.OnEnter();
        if (!transform.IsChildOf(topUI))
        {
            transform.SetParent(topUI, false);
            transform.GetComponent<RectTransform>().anchoredPosition = Vector2.one;
            transform.SetAsLastSibling();
        }
        callback?.Invoke(this);
    }

    public override void OnRecovery(Action<BasePanel> callback = null)
    {
        base.OnRecovery();
        if (!transform.IsChildOf(topUI))
        {
            transform.SetParent(topUI, false);
            transform.GetComponent<RectTransform>().anchoredPosition = Vector2.one;
            transform.SetAsLastSibling();
        }
        callback?.Invoke(this);
    }

    public override void OnPause(Action<BasePanel> callback = null)
    {
        gameObject.SetActive(true);
        callback?.Invoke(this);
    }
    public override void OnExist(Action<BasePanel> callback = null)
    {
        gameObject.SetActive(true);
        callback?.Invoke(this);
    }

    IEnumerator ShowMsg()
    {
        while (true)
        {
            if (msgList.Count > 0)
            {
                for (int i = 0; i < msgList.Count; i++)
                {
                    if (i == uiTextCache.Count) break;
                    msgList[i].Refresh();
                    if (msgList[i].showTime < 0)
                    {
                        msgList.RemoveAt(i);
                        i--;
                    }
                }
                for (int i = 0; i < uiTextCache.Count; i++)
                {
                    if (i < msgList.Count)
                    {
                        ShowOneMessage(msgList[i], uiTextCache[i]);
                    }
                    else
                    {
                        uiTextCache[i].gameObject.SetActive(false);
                    }
                }
            }
            yield return new WaitForEndOfFrame();
        }
    }

    private Transform ShowOneMessage(MessageData msg, Transform ui)
    {
        ui.localRotation = Quaternion.identity;
        ui.localScale = Vector3.one;
        ui.SetAsLastSibling();
        ui.Find("Text").GetComponent<TextMeshProUGUI>().text = msg.text;
        ui.gameObject.SetActive(true);
        transform.SetAsLastSibling();
        return ui;
    }

    private bool OnReceiveUIMessage(params object[] args)
    {
        string str = (string)args[0];
        if (string.IsNullOrEmpty(str))
        {
            return false;
        }
        msgList.Add(new MessageData(str));
        return false;
    }
}
