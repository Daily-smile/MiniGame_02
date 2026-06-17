using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class StartPanel : BasePanel
{
    public override void Init()
    {
        panelType = UIPanelType.Start;
        Button loginBtn = transform.Find("Button"). GetComponent<Button>();
        loginBtn.onClick.AddListener(OnClickLogin);
        Button singleBtn = transform.Find("Single").GetComponent<Button>();
        singleBtn.onClick.AddListener(OnStartSingleGameClick);
        Button infinityBtn = transform.Find("Infinity").GetComponent<Button>();
        infinityBtn.onClick.AddListener(OnStartInfinityGameClick);
    }

    public override void Dispose() { }

    public override void OnEnter(Action<BasePanel> callback = null)
    {
        base.OnEnter();
        EventDispatcher.PostEvent(MessageEvent.SetGameModel, this, false);
        callback?.Invoke(this);
    }
    public override void OnRecovery(Action<BasePanel> callback = null)
    {
        base.OnRecovery();
        EventDispatcher.PostEvent(MessageEvent.SetGameModel, this, false);
        callback?.Invoke(this);
    }

    private void OnClickLogin()
    {
        //EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, "该模式测试中，敬请期待......");
        EventDispatcher.PostEvent(MessageEvent.SetGameModel, this, true);
        UIManager.instance.OpenPanel(UIPanelType.Login);
    }
    private void OnStartSingleGameClick()
    {
        EventDispatcher.PostEvent(MessageEvent.EnterGame, this, GameManager.GameModel.Single);
    }
    private void OnStartInfinityGameClick()
    {
        EventDispatcher.PostEvent(MessageEvent.EnterGame, this, GameManager.GameModel.Infinity);
    }
}
}
