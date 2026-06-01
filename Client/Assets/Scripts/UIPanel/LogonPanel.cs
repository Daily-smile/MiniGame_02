using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LogonPanel : BasePanel
{
    TMP_InputField userName, password;
    public override void Init()
    {
        panelType = UIPanelType.Logon;
        EventDispatcher.AddObserver(this, MessageEvent.OnLogonBack, OnServerLogonBack, null);
    }

    public override void StartUI()
    {
        userName = transform.Find("user").GetComponent<TMP_InputField>();
        password = transform.Find("pwd").GetComponent<TMP_InputField>();
        Button loginBtn = transform.Find("Login").GetComponent<Button>();
        Button backBtn = transform.Find("Back").GetComponent<Button>();
        Button logonBtn = transform.Find("Comfirm").GetComponent<Button>();
        loginBtn.onClick.AddListener(OnLoginClick);
        backBtn.onClick.AddListener(OnBackClick);
        logonBtn.onClick.AddListener(OnLogonClick);
    }

    public override void Dispose()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.OnLogonBack, null);
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

    private void OnLoginClick()
    {
        UIManager.instance.ClosePanel();
        UIManager.instance.OpenPanel(UIPanelType.Login);
    }
    private void OnLogonClick()
    {
        if (!CheckIsConnectState())
        {
            return;
        }
        string user = userName.text;
        string pwd = password.text;
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pwd)) 
        {
            CommonUtility.ShowUIMessage(MessageEventType.NamePwdIsEmpty);
            return;
        }
        CustomNetworkManager.singleton.SendLogon(user, pwd);
    }
    private void OnBackClick()
    {
        UIManager.instance.OpenPanel(UIPanelType.Login);
    }

    private bool OnServerLogonBack(params object[] args)
    {
        bool isRegistOK = (bool)args[0];
        if (isRegistOK) 
        {
            EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, MessageEvent.allMessageStr[MessageEventType.LogonOK]);
            UIManager.instance.OpenPanel(UIPanelType.Login);
        }
        else
        {
            EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, MessageEvent.allMessageStr[MessageEventType.LogonFail]);
        }
        return false;
    }
}
