using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverPanel : BasePanel
{
    Button returnBtn, againBtn;
    TextMeshProUGUI againBtnText;
    public override void Init()
    {
        panelType = UIPanelType.GameOver;
        Transform btnRoot = transform.Find("BtnList");
        returnBtn = btnRoot.Find("QuitBtn").GetComponent<Button>();
        returnBtn.onClick.AddListener(OnClickReturnBtn);
        againBtn = btnRoot.Find("AgainBtn").GetComponent<Button>();
        againBtn.onClick.AddListener(OnClickAgainBtn);
        againBtnText = againBtn.transform.Find("text").GetComponent<TextMeshProUGUI>();
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
        if (GameManager.Instance.IsLoginServer && (GameManager.Instance.gameModel == GameManager.GameModel.Double || GameManager.Instance.gameModel == GameManager.GameModel.Team))
        {
            // 联机模式下失败面板不显示"再来一局"按钮
            againBtn.gameObject.SetActive(false);
        }
        else
        {
            againBtnText.text = "再来一局";
        }
    }

    public override void OnRecovery(Action<BasePanel> callback = null)
    {
        base.OnRecovery();
        // 恢复时也需检查联机模式按钮状态
        if (GameManager.Instance.IsLoginServer && (GameManager.Instance.gameModel == GameManager.GameModel.Double || GameManager.Instance.gameModel == GameManager.GameModel.Team))
        {
            againBtn.gameObject.SetActive(false);
        }
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

    private void OnClickAgainBtn()
    {
        EventDispatcher.PostEvent(MessageEvent.AgainGame, this, null);
        UIManager.instance.ClosePanel();
    }
}
