using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class GamePanel : BasePanel
{
    Button doubleBtn, singleBtn, roomBtn;
    public override void Init()
    {
        EventDispatcher.AddObserver(this, MessageEvent.StartGame, OnServerStartGame, null);
        panelType = UIPanelType.Game;
        doubleBtn = transform.Find("DoubleModel").GetComponent<Button>();
        singleBtn = transform.Find("SingleModel").GetComponent<Button>();
        roomBtn = transform.Find("RoomModel").GetComponent<Button>();
    }
    public override void Dispose()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.StartGame, null);
        doubleBtn = null;
        singleBtn = null;
        roomBtn = null;
    }

    public override void StartUI()
    {
        base.StartUI();
        doubleBtn.onClick.AddListener(OnDoubleModelClick);
        singleBtn.onClick.AddListener(OnSingleModelClick);
        roomBtn.onClick.AddListener(OnRoomModelClick);
    }

    private void OnDoubleModelClick()
    {
        CustomNetworkManager.singleton.SendStartGame(1, 0, null);
    }
    private void OnSingleModelClick()
    {
        CustomNetworkManager.singleton.SendStartGame(0, 0, null);
    }
    private void OnRoomModelClick()
    {
        RoomListPanel roomPanel = UIManager.instance.OpenPanel(UIPanelType.RoomList) as RoomListPanel;
        roomPanel.OnEnterPanel();
    }

    private bool OnServerStartGame(params object[] args)
    {
        // 已在游戏中时忽略匹配相关消息,防止重复触发入场流程
        if (!GameManager.Instance.GameIsOver)
            return false;

        StartGameResponse pack = (StartGameResponse)args[0];
        if (pack.gameModel == 0)
        {
            if (!pack.success)
            {
                CommonUtility.ShowUIMessage(MessageEventType.Unknown);
                return false;
            }
            else
            {
                EventDispatcher.PostEvent(MessageEvent.EnterGame, this, GameManager.GameModel.Single);
            }
        }
        else if (pack.gameModel == 1)
        {
            if (!pack.success)
            {
                if (!UIManager.instance.IsOpenPanel(UIPanelType.DoubleModelMatch))
                    UIManager.instance.OpenPanel(UIPanelType.DoubleModelMatch);
            }
            else
            {
                // 第二名玩家被立即匹配时,DoublModelMatchPanel尚未打开
                // 打开面板并重新发送事件,让面板处理匹配成功后的入场流程
                if (!UIManager.instance.IsOpenPanel(UIPanelType.DoubleModelMatch))
                {
                    UIManager.instance.OpenPanel(UIPanelType.DoubleModelMatch);
                    EventDispatcher.PostEvent(MessageEvent.StartGame, this, pack);
                }
                else
                {
                    CommonUtility.ShowUIMessage(MessageEventType.DoubleModelMatchOK);
                }
            }
        }
        else if (pack.gameModel == 2)
        {
            if (pack.success)
            {
                EventDispatcher.PostEvent(MessageEvent.EnterGame, this, GameManager.GameModel.Team, GameManager.Instance.RoomData);
            }
            else
            {
                CommonUtility.ShowUIMessage(MessageEventType.ServerNotFindRoom);
            }
        }
        return false;
    }
}
}
