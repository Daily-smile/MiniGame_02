using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class DisconnectPanel : BasePanel
{
    const int TIMEOUT = 15;
    int timer;
    int timerEvent;
    Transform topUI;
    Button returnBtn;
    TextMeshProUGUI cutdownTimer;
    StringBuilder strLeftBuider;
    StringBuilder strRightBuider;
    public override void Init()
    {
        panelType = UIPanelType.Disconnect;
        strLeftBuider = new StringBuilder();
        strRightBuider = new StringBuilder();
        cutdownTimer = transform.Find("BG/Timer").GetComponent<TextMeshProUGUI>();
        returnBtn = transform.Find("BG/ReturnBtn").GetComponent<Button>();
        returnBtn.onClick.AddListener(ReturnBtnClick);
        cutdownTimer.text = TIMEOUT + "��";
        topUI = UIManager.instance.canvas.transform.Find("TopUI");
    }
    public override void Dispose()
    {
        strLeftBuider = null;
        strRightBuider = null;
        cutdownTimer = null;
    }

    private void ReturnBtnClick()
    {
        TimerMgr.Instance.Unschedule(timerEvent);
        CustomNetworkManager.singleton.SendReturnLogin();
        OnExist();
        UIManager.instance.OpenPanel(UIPanelType.Start);
    }

    private bool IsCanOpen()
    {
        return !UIManager.instance.IsOpenPanel(UIPanelType.Start);
    }
    private void ShowPanel()
    {
        if (!transform.IsChildOf(topUI))
        {
            transform.SetParent(topUI, false);
            transform.GetComponent<RectTransform>().anchoredPosition = Vector2.one;
        }
        transform.SetAsLastSibling();
        OnOpenPanel();
    }

    public override void OnEnter(Action<BasePanel> callback = null)
    {
        if (!IsCanOpen())
        {
            return;
        }
        base.OnEnter();
        ShowPanel();
        callback?.Invoke(this);
    }
    public override void OnRecovery(Action<BasePanel> callback = null)
    {
        if (!IsCanOpen())
        {
            return;
        }
        base.OnRecovery();
        ShowPanel();
        callback?.Invoke(this);
    }

    public override void OnPause(Action<BasePanel> callback = null)
    {
        base.OnPause();
        if (TimerMgr.Instance.IsExistSchedule(timerEvent))
            TimerMgr.Instance.Unschedule(timerEvent);
        callback?.Invoke(this);
    }

    public override void OnExist(Action<BasePanel> callback = null)
    {
        base.OnExist();
        if (TimerMgr.Instance.IsExistSchedule(timerEvent))
            TimerMgr.Instance.Unschedule(timerEvent);
        callback?.Invoke(this);
    }

    private void OnOpenPanel()
    {
        timer = TIMEOUT;
        cutdownTimer.text = TIMEOUT + "��";
        timerEvent = TimerMgr.Instance.Schedule(StartTiming, -1, 1);
    }

    private void StartTiming(object obj)
    {
        strLeftBuider.Clear();
        strLeftBuider.Append(timer).Append(strRightBuider);
        cutdownTimer.text = strLeftBuider.ToString();
        if (timer <= 0)
        {
            TimerMgr.Instance.Unschedule(timerEvent);
            OnExist();
            CustomNetworkManager.singleton.SendReturnLogin();
            UIManager.instance.OpenPanel(UIPanelType.Start);
        }
        timer--;
    }
}
}
