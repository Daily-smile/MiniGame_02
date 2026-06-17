using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class PausePanel : BasePanel
{
    Button returnBtn, againBtn, closeBtn;
    float originTimerScale;
    public override void Init()
    {
        panelType = UIPanelType.PausePanel;
        returnBtn = transform.Find("QuitBtn").GetComponent<Button>();
        returnBtn.onClick.AddListener(OnClickReturnBtn);
        closeBtn = transform.Find("CloseBtn").GetComponent<Button>();
        closeBtn.onClick.AddListener(OnClickCloseBtn);
        againBtn = transform.Find("AgainBtn").GetComponent<Button>();
        againBtn.onClick.AddListener(OnClickAgainBtn);
    }
    public override void Dispose()
    {
        
    }

    public override void Show()
    {
        base.Show();
    }

    public override void OnEnter(Action<BasePanel> callback = null)
    {
        base.OnEnter(callback);
        originTimerScale = Time.timeScale;
        Time.timeScale = 0;
    }

    public override void OnExist(Action<BasePanel> callback = null)
    {
        base.OnExist(callback);
        Time.timeScale = originTimerScale;
    }

    public override void OnRecovery(Action<BasePanel> callback = null)
    {
        base.OnRecovery();
        originTimerScale = Time.timeScale;
        Time.timeScale = 0;
        callback?.Invoke(this);
    }

    private void OnClickReturnBtn()
    {
        UIManager.instance.ClosePanel();
        EventDispatcher.PostEvent(MessageEvent.QuitGame, this, null);
        // 通知服务器：玩家离开游戏，清理旧的玩家对象和匹配状态
        if (GameManager.Instance.IsLoginServer && CustomNetworkManager.singleton != null)
            CustomNetworkManager.singleton.SendReturnLogin();
        UIManager.instance.OpenPanel(UIPanelType.GameLoad, (panel) =>
        {
            GameLoadPanel ui = panel as GameLoadPanel;
            ui.OnQuitGameScene();
        });
    }

    private void OnClickCloseBtn()
    {
        UIManager.instance.ClosePanel();
    }
    private void OnClickAgainBtn()
    {
        EventDispatcher.PostEvent(MessageEvent.AgainGame, this, null);
        UIManager.instance.ClosePanel();
    }
}
}
