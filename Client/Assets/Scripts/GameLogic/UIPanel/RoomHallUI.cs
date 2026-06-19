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
public class RoomHallUI : BaseUI
{
    private Dictionary<int, RoomInfo> roomPackList;
    private RoomListPanel rootPanel;
    private List<RoomHallItem> all_roomHallItems;

    private TextMeshProUGUI tipText;
    private Button refreshBtn;
    private bool _pendingRefreshFeedback; // 是否等待刷新反馈（仅手动点击刷新按钮时为 true）

    public void Init(RoomListPanel rootPanel)
    {
        base.Init();
        this.rootPanel = rootPanel;
        roomPackList = new Dictionary<int, RoomInfo>();
        Transform rootParent = transform.Find("Viewport/Content");
        all_roomHallItems = new List<RoomHallItem>();
        for (int i = 0; i < rootParent.childCount; i++)
        {
            RoomHallItem item = rootParent.GetChild(i).GetComponent<RoomHallItem>();
            if (item == null)
            {
                item = rootParent.GetChild(i).gameObject.AddComponent<RoomHallItem>();
                item.Init(rootPanel, this);
            }
            item.gameObject.SetActive(false);
            all_roomHallItems.Add(item);
        }

        refreshBtn = transform.Find("RefreshBtn").GetComponent<Button>();
        refreshBtn.onClick.AddListener(RefreshRoomPanelClick);
        tipText = transform.Find("Tip").GetComponent<TextMeshProUGUI>();
        tipText.gameObject.SetActive(true);
        tipText.text = MessageEvent.allMessageStr[MessageEventType.RoomHallIsEmpty];
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AddObserverEvents();
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        RemoveObserverEvents();
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        RemoveObserverEvents();
    }

    private void AddObserverEvents()
    {
        EventDispatcher.AddObserver(this, MessageEvent.OnRefreshRoomList, RefreshRoomList, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnCreateRoomBack, CreateRoomBack, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnDestroyRoomBack, DestroyRoomBack, null);
    }
    private void RemoveObserverEvents()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.OnRefreshRoomList, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnCreateRoomBack, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnDestroyRoomBack, null);
    }

    private void RefreshRoomPanelClick()
    {
        _pendingRefreshFeedback = true;
        string name = GameManager.Instance.RoomData.roomID != 0 ? GameManager.Instance.RoomData.roomName : "";
        CustomNetworkManager.singleton.SendFindRoom(name);
    }

    public void SwitchUI()
    {
        CustomNetworkManager.singleton.SendFindRoom();
    }

    public void RefreshUI()
    {
        int count = 0;
        foreach (var kvp in roomPackList)
        {
            RoomInfo room = kvp.Value;
            if (count < all_roomHallItems.Count)
            {
                all_roomHallItems[count].RefreshUI(room);
                all_roomHallItems[count].gameObject.SetActive(true);
                count++;
            }
            else
            {
                count++;
                break;
            }
        }
        for (int i = count; i < all_roomHallItems.Count; i++)
        {
            all_roomHallItems[i].gameObject.SetActive(false);
        }
        tipText.gameObject.SetActive(count == 0);
    }
    public void RefreshUI(int romoveRoomID)
    {
        if (roomPackList.ContainsKey(romoveRoomID))
        {
            roomPackList.Remove(romoveRoomID);
        }
        RefreshUI();
    }

    private bool CreateRoomBack(params object[] args)
    {
        rootPanel.SwitchRoomModel(RoomListPanel.RoomModel.MyRoom);
        return false;
    }
    private bool RefreshRoomList(params object[] args)
    {
        RoomInfo[] roomList = args[0] as RoomInfo[];
        bool success = args.Length > 1 && args[1] is bool b && b;
        roomPackList.Clear();
        for (int i = 0; i < roomList.Length; i++)
        {
            roomPackList.Add(roomList[i].roomID, roomList[i]);
        }
        RefreshUI();

        // 仅手动点击刷新按钮时显示反馈
        if (_pendingRefreshFeedback)
        {
            _pendingRefreshFeedback = false;
            if (success)
                EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, "刷新成功");
            else
                EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, "刷新失败");
        }
        return false;
    }
    private bool DestroyRoomBack(params object[] args)
    {
        CustomNetworkManager.singleton.SendFindRoom();
        return false;
    }
}

public class RoomHallItem : BaseUI
{
    private TextMeshProUGUI roomName, roomNum;
    private GameObject waitStatus, gameStatus, fullStatus;
    private Button join, destroy, quit;
    private bool isMaster;
    private bool isInitTran;
    private RoomInfo roomData;
    private RoomListPanel bindPanel;
    private RoomHallUI bindUI;
    public int RoomID
    {
        get
        {
            return roomData.roomID != 0 ? roomData.roomID : -1;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EventDispatcher.AddObserver(this, MessageEvent.RoomPlayerRefreshRespond, RoomPlayerRefreshRespond, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnKickoutRoomBack, OnKickoutRoomBack, null);
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        EventDispatcher.RemoveObserver(this, MessageEvent.RoomPlayerRefreshRespond, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnKickoutRoomBack, null);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventDispatcher.RemoveObserver(this, MessageEvent.RoomPlayerRefreshRespond, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnKickoutRoomBack, null);
    }

    public void Init(RoomListPanel rootPanel, RoomHallUI tobindUI)
    {
        base.Init();
        bindPanel = rootPanel;
        bindUI = tobindUI;
        isMaster = false;
        InitTran();
    }

    private void InitTran()
    {
        if (!isInitTran)
        {
            this.roomName = transform.Find("Name/Text").GetComponent<TextMeshProUGUI>();
            this.roomNum = transform.Find("Num/Text").GetComponent<TextMeshProUGUI>();
            this.waitStatus = transform.Find("WaitStatus").gameObject;
            this.gameStatus = transform.Find("GameStatus").gameObject;
            this.fullStatus = transform.Find("FullStatus").gameObject;
            this.join = transform.Find("Join").GetComponent<Button>();
            this.join.onClick.AddListener(JoinRoomClick);
            this.quit = transform.Find("Quit").GetComponent<Button>();
            this.quit.onClick.AddListener(QuitRoomClick);
            this.destroy = transform.Find("Destroy").GetComponent<Button>();
            this.destroy.onClick.AddListener(DestroyRoomClick);
        }
        isInitTran = true;
    }

    private void JoinRoomClick()
    {
        int capturedRoomID = roomData.roomID;
        if (capturedRoomID != 0)
        {
            if (roomData.status == 2)
            {
                CommonUtility.ShowUIMessage(MessageEventType.RoomFullTipToJoin);
                return;
            }
            else if (roomData.status == 3)
            {
                CommonUtility.ShowUIMessage(MessageEventType.RoomGameTipToJoin);
                return;
            }
        }
        if (!GameManager.Instance.IsInRoom())
        {
            CustomNetworkManager.singleton.SendJoinRoom(capturedRoomID);
        }
        else
        {
            CommonUtility.ShowTipPanel(MessageEvent.allMessageStr[MessageEventType.InRoomToJoinRoom], () => {
                CustomNetworkManager.singleton.SendQuitRoom(GameManager.Instance.GetRoomID);
                CustomNetworkManager.singleton.SendJoinRoom(capturedRoomID);
            });
        }
    }
    private void DestroyRoomClick()
    {
        int capturedRoomID = roomData.roomID;
        CustomNetworkManager.singleton.SendDestroyRoom(capturedRoomID);
    }
    private void QuitRoomClick()
    {
        int capturedRoomID = roomData.roomID;
        if (GameManager.Instance.QueryInRoom(roomData))
        {
            CommonUtility.ShowTipPanel(MessageEvent.allMessageStr[MessageEventType.IsQuitRoom], () => {
                CustomNetworkManager.singleton.SendQuitRoom(capturedRoomID);
            });
        }
        else
        {
            CommonUtility.ShowUIMessage(MessageEventType.NotInRoom);
        }
    }

    public void RefreshUI(int roomID = -1)
    {
        if (roomData.roomID == 0)
        {
            gameObject.SetActive(false);
            bindUI.RefreshUI(roomID);
            return;
        }
        if (roomData.status != 3)
        {
            if (roomData.playerNames.Length < roomData.maxNum)
            {
                roomData.status = 1;
                waitStatus.SetActive(true);
                fullStatus.SetActive(false);
                gameStatus.SetActive(false);
            }
            else
            {
                roomData.status = 2;
                waitStatus.SetActive(false);
                fullStatus.SetActive(true);
                gameStatus.SetActive(false);
            }
        }
        else
        {
            waitStatus.SetActive(false);
            fullStatus.SetActive(false);
            gameStatus.SetActive(true);
        }
        roomName.text = roomData.roomName;
        roomNum.text = roomData.playerNames.Length + "/" + roomData.maxNum;
        isMaster = roomData.roomMasterName.Equals(GameManager.Instance.userName);
        bool isHaveRoom = isMaster || GameManager.Instance.QueryInRoom(roomData);
        quit.gameObject.SetActive(isHaveRoom);
        join.gameObject.SetActive(!isHaveRoom);
        destroy.gameObject.SetActive(isMaster);
    }
    public void RefreshUI(RoomInfo roomPack)
    {
        roomData = roomPack;
        RefreshUI();
    }

    /// <summary>
    /// �����������Ƴ����
    /// </summary>
    private bool RoomPlayerRefreshRespond(params object[] args)
    {
        if (args == null)
        {
            return false;
        }
        RoomInfo newRoom = (RoomInfo)args[0];
        if (newRoom.roomID != RoomID) return false;
        bool isDestroy = args.Length > 1 && args[1] is string s && s == "NoticeInRoomDestroy";
        roomData = isDestroy ? default : newRoom;
        RefreshUI(newRoom.roomID);
        return false;
    }

    private bool OnKickoutRoomBack(params object[] args)
    {
        int roomID = (int)args[0];
        if (roomData.roomID == 0 || roomID != RoomID)
        {
            return false;
        }
        RoomInfo newRoomPack = (RoomInfo)args[1];
        // 直接使用服务端返回的权威玩家列表，无需本地计算差异
        roomData.playerNames = newRoomPack.playerNames;
        roomData.status = newRoomPack.status;
        RefreshUI();
        return false;
    }
}
}
