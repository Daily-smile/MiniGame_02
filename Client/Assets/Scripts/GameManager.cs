using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : UnitySingleton<GameManager>
{
    public enum GameModel
    {
        None,
        Single,
        Double,
        Team,
        Infinity,
    }

    /// <summary>当前是否连接到服务器</summary>
    public bool connectState
    {
        get
        {
            return CustomNetworkManager.singleton != null && CustomNetworkManager.singleton.IsConnectedToServer();
        }
    }

    /// <summary>Mirror连接状态 (与connectState保持一致)</summary>
    public bool IsMirrorConnected => connectState;

    private string _userName;
    public string userName
    {
        get
        {
            return _userName == null ? string.Empty : _userName;
        }
    }

    /// <summary>玩家唯一ID（服务端分配，离线/单机模式为 -1）</summary>
    public int playerId { get; private set; } = -1;

    public int GetRoomID
    {
        get { return roomData.roomID; }
    }

    private RoomInfo roomData;
    public RoomInfo RoomData { get { return roomData; } }

    public string sessionId { private set; get; }
    public GameModel gameModel { get; private set; } = GameModel.None;
    public bool GameIsOver { get; private set; } = true;
    public bool IsLoginServer { get; private set; }

    /// <summary>当前是否处于游戏运行状态</summary>
    public bool IsGameRuningState { get; private set; }

    private int _infinityModelScore;
    public int InfinityModelScore
    {
        get => _infinityModelScore;
        private set
        {
            if (_infinityModelScore != value)
            {
                _infinityModelScore = value;
                EventDispatcher.PostEvent(MessageEvent.RefreshInfinityScoreUI, this, null);
            }
        }
    }

    private int _infinityModelSaveMaxScore;
    public int InfinityModelSaveMaxScore
    {
        get => _infinityModelSaveMaxScore;
        set
        {
            if (_infinityModelSaveMaxScore != value)
            {
                _infinityModelSaveMaxScore = value;
                EventDispatcher.PostEvent(MessageEvent.RefreshInfinitySaveMaxScoreUI, this, null);
            }
        }
    }

    public override void Awake()
    {
        UIManager.instance.OpenPanel(UIPanelType.Message);
        AddObserverEvents();
        UIManager.instance.LoadCacheUIPanel(UIPanelType.Game, UIPanelType.Disconnect, UIPanelType.Tip);
        GameReferee.instance.Initialize();
        base.Awake();
    }

    private void Start()
    {
#if UNITY_STANDALONE_WIN
        ToggleFullscreen(false);
#else
        ToggleFullscreen(true);
#endif
    }

    private void OnDestroy()
    {
        RemoveObserverEvents();
    }

    private void AddObserverEvents()
    {
        EventDispatcher.AddObserver(this, MessageEvent.OnLoginOK, LoginOK, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnLoginFail, LoginFail, null);
        EventDispatcher.AddObserver(this, MessageEvent.ServerConnectChange, ServerConnect, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnDestroyRoomBack, OnServerDestroyRoom, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnJoinRoomBack, UpdatePlayerRoomPack, null);
        EventDispatcher.AddObserver(this, MessageEvent.UpdatePlayerRoomPack, UpdatePlayerRoomPack, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnReconnectFail, OnReconnectFail, null);
        EventDispatcher.AddObserver(this, MessageEvent.EnterGame, OnEnterGame, null);
        EventDispatcher.AddObserver(this, MessageEvent.GameOver, OnGameEnd, null);
        EventDispatcher.AddObserver(this, MessageEvent.AgainGame, OnAgainGame, null);
        EventDispatcher.AddObserver(this, MessageEvent.GameWin, OnGameEnd, null);
        EventDispatcher.AddObserver(this, MessageEvent.GameEnd, OnGameEnd, null);
        EventDispatcher.AddObserver(this, MessageEvent.WaitingForGameRuning, StartGameRuning, null);
        EventDispatcher.AddObserver(this, MessageEvent.UpdateInfinityScore, UpdateInfinityScore, null);
    }

    private void RemoveObserverEvents()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.OnLoginOK, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnLoginFail, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.ServerConnectChange, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnDestroyRoomBack, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnJoinRoomBack, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.UpdatePlayerRoomPack, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnReconnectFail, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.EnterGame, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.GameOver, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.AgainGame, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.GameWin, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.GameEnd, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.WaitingForGameRuning, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.UpdateInfinityScore, null);
    }

    public void StartGame()
    {
        UIManager.instance.OpenPanel(UIPanelType.Start);
    }

    public void ToggleFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        if (!isFullscreen)
        {
            Screen.SetResolution(1280, 720, false);
        }
    }

    #region 游戏数据

    /// <summary>获取当前游戏模式对应的服务器 gameModel 值 (1=Double, 2=RoomTeam)</summary>
    public int GetCurServerGameModel()
    {
        switch (gameModel)
        {
            case GameModel.Double: return 1;
            case GameModel.Team: return 2;
            default: return 0;
        }
    }

    public bool QueryInRoom(RoomInfo room)
    {
        if (room.playerNames == null) return false;
        foreach (string name in room.playerNames)
        {
            if (name == _userName) return true;
        }
        return false;
    }

    public bool IsInRoom()
    {
        return roomData.roomID != 0;
    }

    public bool IsInRoom(int roomID)
    {
        return roomData.roomID == roomID;
    }
    #endregion

    #region 网络事件回调

    private bool OnEnterGame(params object[] args)
    {
        InfinityModelScore = 0;
        GameIsOver = false;
        IsGameRuningState = false;
        gameModel = (GameModel)args[0];
        InfinityModelSaveMaxScore = PlayerPrefs.GetInt("HistoryMaxScore", 0);
        EventDispatcher.PostEvent(MessageEvent.SetGameModel, this, gameModel != GameModel.Single && gameModel != GameModel.Infinity);
        UIManager.instance.OpenPanel(UIPanelType.GameLoad, (panel) =>
        {
            GameLoadPanel ui = panel as GameLoadPanel;
            ui.OnEnterGameScene();
        });
        return false;
    }

    private bool OnGameEnd(params object[] args)
    {
        if (InfinityModelSaveMaxScore < InfinityModelScore)
        {
            PlayerPrefs.SetInt("HistoryMaxScore", InfinityModelScore);
            InfinityModelSaveMaxScore = InfinityModelScore;
        }
        GameIsOver = true;
        IsGameRuningState = false;
        return false;
    }

    private bool OnAgainGame(params object[] args)
    {
        if (InfinityModelSaveMaxScore < InfinityModelScore)
        {
            PlayerPrefs.SetInt("HistoryMaxScore", InfinityModelScore);
            InfinityModelSaveMaxScore = InfinityModelScore;
        }
        InfinityModelScore = 0;
        GameIsOver = false;
        IsGameRuningState = true;
        return false;
    }

    private bool StartGameRuning(params object[] args)
    {
        IsGameRuningState = true;
        return false;
    }

    private bool UpdateInfinityScore(params object[] args)
    {
        int addNum = (int)args[0];
        InfinityModelScore += addNum;
        return false;
    }

    // 重连失败
    private bool OnReconnectFail(params object[] args)
    {
        sessionId = null;
        EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, "连接失败，请重新登录");
        UIManager.instance.OpenPanel(UIPanelType.Login);
        return false;
    }

    private bool ServerConnect(params object[] args)
    {
        MessageEventType eventType = (MessageEventType)args[0];
        switch (eventType)
        {
            case MessageEventType.ClientOK:
                CommonUtility.ShowUIMessage(MessageEventType.ClientOK);
                break;
            case MessageEventType.ClientFail:
                CommonUtility.ShowUIMessage(MessageEventType.ClientFail);
                break;
            default:
                CommonUtility.ShowUIMessage(MessageEventType.ClientError);
                break;
        }
        return false;
    }

    // 在登录成功时获取SessionID和PlayerID
    private bool LoginOK(params object[] args)
    {
        _userName = (string)args[0];
        sessionId = (string)args[1];
        // args[2] = playerId (服务端分配的唯一ID)
        if (args.Length > 2 && args[2] is int pid)
            playerId = pid;
        IsLoginServer = true;
        IsGameRuningState = false;
        return false;
    }

    private bool LoginFail(params object[] args)
    {
        IsLoginServer = false;
        IsGameRuningState = false;
        return false;
    }

    // -------- Mirror 网络连接回调 --------

    public void OnMirrorConnected()
    {
        IsLoginServer = true;
    }

    public void OnMirrorDisconnected()
    {
        IsLoginServer = false;
        IsGameRuningState = false;
        playerId = -1; // 重置为离线模式
    }

    private bool OnServerDestroyRoom(params object[] args)
    {
        bool isOk = (bool)args[0];
        if (isOk)
        {
            roomData = default;
        }
        return false;
    }

    private bool UpdatePlayerRoomPack(params object[] args)
    {
        if (args == null || args[0] == null)
        {
            roomData = default;
            return false;
        }
        else if (args[0] is RoomInfo info)
        {
            // 仅在自己是房间成员时才更新房间数据（被踢出或退出后忽略广播），优先用 playerId
            if (info.roomID != 0)
            {
                bool isMember = false;
                if (playerId > 0 && info.playerIds != null)
                {
                    for (int i = 0; i < info.playerIds.Length; i++)
                    {
                        if (info.playerIds[i] == playerId) { isMember = true; break; }
                    }
                }
                else if (info.playerNames != null)
                {
                    for (int i = 0; i < info.playerNames.Length; i++)
                    {
                        if (info.playerNames[i].Equals(userName)) { isMember = true; break; }
                    }
                }
                roomData = isMember ? info : default;
            }
            else
            {
                roomData = default;
            }
        }
        else
        {
            roomData = default;
        }
        return false;
    }
    #endregion
}
