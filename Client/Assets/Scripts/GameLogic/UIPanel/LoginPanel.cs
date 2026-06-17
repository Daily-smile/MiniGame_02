using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class LoginPanel : BasePanel
{
    private TMP_InputField user, pwd;
    public override void Init()
    {
        panelType = UIPanelType.Login;
        EventDispatcher.AddObserver(this, MessageEvent.OnLoginOKBack, OnServerLoginOKBack, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnLoginFail, OnServerLoginFail, null);
    }

    public override void StartUI()
    {
        user = transform.Find("user").GetComponent<TMP_InputField>();
        pwd = transform.Find("pwd").GetComponent<TMP_InputField>();
        Button loginBtn = transform.Find("Comfirm").GetComponent<Button>();
        Button backBtn = transform.Find("Back").GetComponent<Button>();
        Button logonBtn = transform.Find("Regist").GetComponent<Button>();
        loginBtn.onClick.AddListener(OnLoginClick);
        backBtn.onClick.AddListener(OnBackClick);
        logonBtn.onClick.AddListener(OnLogonClick);
    }

    public override void Dispose()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.OnLoginOKBack, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnLoginFail, null);
    }

    private bool CheckIsConnectState()
    {
        if (!GameManager.Instance.connectState)
        {
            CommonUtility.ShowTipPanel(MessageEventType.DisconnectStatusLoginTip, () => {
                EventDispatcher.PostEvent(MessageEvent.EnterGame, this, GameManager.GameModel.Single);
            });
            return false;
        }
        return true;
    }

    private void OnLogonClick()
    {
        if (CheckIsConnectState())
        {
            UIManager.instance.OpenPanel(UIPanelType.Logon);
        }
    }
    private void OnLoginClick()
    {
        if (!CheckIsConnectState())
        {
            return;
        }
        if (string.IsNullOrEmpty(user.text) || string.IsNullOrEmpty(pwd.text))
        {
            CommonUtility.ShowUIMessage(MessageEventType.NamePwdIsEmpty);
            return;
        }
        CustomNetworkManager.singleton.SendLogin(user.text, pwd.text);
    }

    private void OnBackClick()
    {
        UIManager.instance.OpenPanel(UIPanelType.Start);
    }

    private bool OnServerLoginOKBack(params object[] args)
    {
        string userName = (string)args[0];
        string sessionID = args[1] != null ? (string)args[1] : string.Empty;
        EventDispatcher.PostEvent(MessageEvent.OnLoginOK, this, userName, sessionID);
        UIManager.instance.OpenPanel(UIPanelType.Game);
        EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, MessageEvent.allMessageStr[MessageEventType.LoginOK]);
        return false;
    }

    private bool OnServerLoginFail(params object[] args)
    {
        // 登录失败时留在登录面板允许用户重试 (UI消息由全局MessagePanel显示)
        return false;
    }
}
}
