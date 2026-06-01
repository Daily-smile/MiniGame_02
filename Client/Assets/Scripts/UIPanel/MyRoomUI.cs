using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MyRoomUI : BaseUI
{
    private struct ChatMsg
    {
        public string playerName;
        public string msg;
        public string timer;
        public DateTime timeSpan;
        public ChatMsg(string player, string content)
        {
            this.playerName = player;
            this.msg = content;
            this.timeSpan = DateTime.Now;
            this.timer = $"{this.timeSpan.ToString("T")}";
        }
    }
    private RoomInfo roomData;
    private TextMeshProUGUI tipText;
    private TMP_InputField inputChatMessage;
    private Button startGameBtn, sendChatMessageBtn;
    private Transform chatTranRoot, chatPrefab, chatScrollRed;
    private RectTransform chatScrolRt;
    private MyScrollRect chatScroll;
    private List<MyRoomItem> all_playerList;
    private List<ChatMsg> chatMsgs;
    private RoomListPanel bindPanel;
    private bool isHasNewMsg;
    private bool isHasDisconnect;
    public bool PlayerIsMaster
    {
        get
        {
            return roomData.roomID != 0 && roomData.roomMasterName.Equals(GameManager.Instance.userName);
        }
    }
    public int RoomID
    {
        get { return roomData.roomID != 0 ? roomData.roomID : -1; }
    }

    public void Init(RoomListPanel panel)
    {
        base.Init();
        bindPanel = panel;
        all_playerList = new List<MyRoomItem>();
        chatMsgs = new List<ChatMsg>();
        Transform rootTran = transform.Find("Viewport/Content");
        for (int i = 0; i < rootTran.childCount; i++)
        {
            MyRoomItem item = rootTran.GetChild(i).GetComponent<MyRoomItem>();
            if (item == null)
            {
                item = rootTran.GetChild(i).gameObject.AddComponent<MyRoomItem>();
                item.Init(bindPanel, this);
            }
            item.gameObject.SetActive(false);
            all_playerList.Add(item);
        }
        tipText = transform.Find("Tip").GetComponent<TextMeshProUGUI>();
        tipText.text = MessageEvent.allMessageStr[MessageEventType.MyRoomIsEmpty];
        tipText.gameObject.SetActive(true);
        startGameBtn = transform.Find("StartGameBtn").GetComponent<Button>();
        startGameBtn.onClick.AddListener(StartGameBtnClick);
        inputChatMessage = transform.Find("InputField").GetComponent<TMP_InputField>();
        inputChatMessage.text = "";
        isHasNewMsg = false;
        chatScroll = transform.Find("ChatScroll").GetComponent<MyScrollRect>();
        chatScrolRt = chatScroll.GetComponent<RectTransform>();
        chatScroll.onValueChanged.AddListener(OnChatScrollDrag);
        chatTranRoot = chatScroll.transform.Find("Viewport/Content");
        chatPrefab = transform.Find("ChatScroll/TeamChat");
        chatPrefab.gameObject.SetActive(false);
        chatScrollRed = chatScrolRt.Find("red");
        chatScrollRed.gameObject.SetActive(false);
        sendChatMessageBtn = inputChatMessage.transform.Find("Send").GetComponent<Button>();
        sendChatMessageBtn.onClick.AddListener(SendChatMessageClick);
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
        EventDispatcher.AddObserver(this, MessageEvent.OnKickoutRoomBack, OnKickoutRoomBack, null);
        EventDispatcher.AddObserver(this, MessageEvent.RoomPlayerRefreshRespond, RoomPlayerRefreshRespond, null);
        EventDispatcher.AddObserver(this, MessageEvent.StartGame, OnServerStartGame, null);
        EventDispatcher.AddObserver(this, MessageEvent.RoomPlayerOnlineStatus, ServerRoomPlayerOnlineStatus, null);
    }
    private void RemoveObserverEvents()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.OnRefreshRoomList, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnCreateRoomBack, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnDestroyRoomBack, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnKickoutRoomBack, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.RoomPlayerRefreshRespond, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.StartGame, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.RoomPlayerOnlineStatus, null);
    }

    private void StartGameBtnClick()
    {
        if (roomData.roomID == 0)
        {
            return;
        }
        if (!PlayerIsMaster)
        {
            CommonUtility.ShowUIMessage(MessageEventType.NoRoomMasterStartGame);
            return;
        }
        if (isHasDisconnect)
        {
            CommonUtility.ShowUIMessage(MessageEventType.RoomHasDisconnectStartGame);
            return;
        }
        CustomNetworkManager.singleton.SendStartGame(2, GameManager.Instance.GetRoomID, roomData.playerNames);
    }

    private string GetSelfPlayerData()
    {
        if (roomData.roomID == 0)
        {
            return null;
        }
        for (int i = 0; i < roomData.playerNames.Length; i++)
        {
            if (roomData.playerNames[i].Equals(GameManager.Instance.userName))
            {
                return roomData.playerNames[i];
            }
        }
        return null;
    }
    private void SendChatMessageClick()
    {
        string username = GetSelfPlayerData();
        if (string.IsNullOrEmpty(username))
        {
            CommonUtility.ShowUIMessage(MessageEventType.ChatRoomSendMsgError);
            return;
        }
        string msg = inputChatMessage.text;
        CustomNetworkManager.singleton.SendChatRoomMsg(RoomID, msg);
    }

    /// <summary>清除所有聊天消息（被踢/退出房间时调用）</summary>
    private void ClearChatMessages()
    {
        chatMsgs.Clear();
        isHasNewMsg = false;
        chatScrollRed.gameObject.SetActive(false);
        // 销毁聊天 UI 子对象（跳过 chatPrefab）
        for (int i = chatTranRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = chatTranRoot.GetChild(i);
            if (child != chatPrefab)
                GameObject.Destroy(child.gameObject);
        }
    }

    public void OnUpdateOneChatRoomMessage(string playerName, string msg)
    {
        if (chatMsgs.Count > 1000)
        {
            chatMsgs.RemoveAt(0);
        }
        ChatMsg chatMsg = new ChatMsg(playerName, msg);
        chatMsgs.Add(chatMsg);
        AddOneRoomChatMessage(chatMsg);
        isHasNewMsg = true;
        chatTranRoot.GetComponent<VerticalLayoutGroup>().CalculateLayoutInputVertical();
        LayoutRebuilder.ForceRebuildLayoutImmediate(chatTranRoot.GetComponent<RectTransform>());
        bool isActive = chatTranRoot.GetChild(chatTranRoot.childCount - 1).transform.position.y < chatScrollRed.position.y;
        chatScrollRed.gameObject.SetActive(isActive);
    }

    private bool CheckRoomIsExistNewChat()
    {
        if (!isHasNewMsg || chatTranRoot.childCount < 1)
        {
            return false;
        }
        //if (!RectTransformUtility.RectangleContainsScreenPoint(chatScrolRt, chatTranRoot.GetChild(chatTranRoot.childCount - 1).transform.position))
        if (chatTranRoot.GetChild(chatTranRoot.childCount - 1).transform.position.y > chatScrollRed.position.y)
        {
            isHasNewMsg = false;
            return true;
        }
        return false;
    }
    private void OnChatScrollDrag(Vector2 v)
    {
        if (CheckRoomIsExistNewChat())
        {
            chatScrollRed.gameObject.SetActive(false);
        }
    }

    private void AddOneRoomChatMessage(ChatMsg chat)
    {
        GameObject newChat = GameObject.Instantiate(chatPrefab.gameObject);
        newChat.gameObject.SetActive(true);
        TextMeshProUGUI timer = newChat.transform.Find("time").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI name = newChat.transform.Find("name").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI content = newChat.transform.Find("content").GetComponent<TextMeshProUGUI>();
        timer.text = "[" + chat.timer + "]";
        name.text = chat.playerName + ":";
        content.text = chat.msg;
        AdvancedTmpSizer advancedTmpSizer = content.GetComponent<AdvancedTmpSizer>();
        advancedTmpSizer.OnInit();
        advancedTmpSizer.UpdateSize();
        float totalHeight = content.GetComponent<RectTransform>().sizeDelta.y + name.GetComponent<RectTransform>().sizeDelta.y;
        RectTransform newRt = newChat.GetComponent<RectTransform>();
        newRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
        newChat.transform.SetParent(chatTranRoot, false);
        newChat.transform.localPosition = Vector2.zero;
        RectTransform chatContentRT = chatTranRoot.GetComponent<RectTransform>();
        if (chatContentRT.rect.height > chatScrolRt.rect.height)
        {
            chatContentRT.anchoredPosition += new Vector2(0, totalHeight + chatTranRoot.GetComponent<VerticalLayoutGroup>().spacing);
        }
    }

    public void SwitchUI()
    {
        string name;
        if (GameManager.Instance.RoomData.roomID == 0)
        {
            name = "";
            roomData = default;
        }
        else
        {
            name = GameManager.Instance.RoomData.roomName;
        }
        CustomNetworkManager.singleton.SendFindRoom(name);
    }

    private void RoomDataNullUI()
    {
        startGameBtn.interactable = false;
        tipText.gameObject.SetActive(true);
        for (int i = 0; i < all_playerList.Count; i++)
        {
            all_playerList[i].gameObject.SetActive(false);
        }
        // 清除聊天消息列表
        ClearChatMessages();
    }

    private void RefreshUI()
    {
        if (roomData.roomID == 0)
        {
            RoomDataNullUI();
            return;
        }
        int count = 0;
        if (roomData.status != 3)
            roomData.status = roomData.playerNames.Length < roomData.maxNum ? 1 : 2;
        for (int i = 0; i < roomData.playerNames.Length; i++)
        {
            string playerName = roomData.playerNames[i];
            int playerId = (roomData.playerIds != null && i < roomData.playerIds.Length)
                ? roomData.playerIds[i] : -1;
            if (count < all_playerList.Count)
            {
                all_playerList[count].RefreshUI(roomData.roomID, roomData.roomMasterName.Equals(playerName), playerName, playerId);
                all_playerList[count].gameObject.SetActive(true);
                count++;
            }
            else
            {
                count++;
                break;
            }
        }
        for (int i = count; i < all_playerList.Count; i++)
        {
            all_playerList[i].gameObject.SetActive(false);
        }
        startGameBtn.interactable = true;
        tipText.gameObject.SetActive(roomData.roomID == 0 || roomData.playerNames.Length == 0);
    }

    private bool RefreshRoomList(params object[] args)
    {
        RoomInfo[] roomList = args[0] as RoomInfo[];
        if (roomList.Length > 1)
        {
            return false;
        }
        if (roomList.Length > 0)
        {
            bool isOwnRoom = false;
            for (int i = 0; i < roomList[0].playerNames.Length; i++)
            {
                if (roomList[0].playerNames[i].Equals(GameManager.Instance.userName))
                {
                    isOwnRoom = true;
                    break;
                }
            }
            roomData = isOwnRoom ? roomList[0] : default;
        }
        else
        {
            roomData = default;
        }
        RefreshUI();
        return false;
    }
    private bool CreateRoomBack(params object[] args)
    {
        if (args == null || args.Length == 0) return false;
        RoomInfo pack = (RoomInfo)args[0];
        if (pack.roomID != 0)
        {
            roomData = pack;
            RefreshUI();
        }
        return false;
    }
    private bool DestroyRoomBack(params object[] args)
    {
        RoomDataNullUI();
        return false;
    }

    private bool OnKickoutRoomBack(params object[] args)
    {
        int roomID = (int)args[0];
        if (roomData.roomID == 0 || roomID != roomData.roomID)
        {
            return false;
        }
        RoomInfo newRoomPack = (RoomInfo)args[1];
        bool isInRoom = false;
        for (int i = 0; i < newRoomPack.playerNames.Length; i++)
        {
            if (!isInRoom && newRoomPack.playerNames[i].Equals(GameManager.Instance.userName))
            {
                isInRoom = true;
                break;
            }
        }
        if (isInRoom)
        {
            // 自己还在房间里 → 是别人被踢了，更新房间数据
            roomData = newRoomPack;
        }
        else
        {
            // 自己不在新房间列表里 → 自己被踢了，清空房间数据
            roomData = default;
        }
        RefreshUI();
        return false;
    }

    /// <summary>
    /// 房间有玩家被移除时
    /// </summary>
    private bool RoomPlayerRefreshRespond(params object[] args)
    {
        if (args == null || roomData.roomID == 0)
        {
            return false;
        }
        RoomInfo newRoom = (RoomInfo)args[0];
        if (newRoom.roomID != roomData.roomID) return false;
        bool isDestroy = args.Length > 1 && args[1] is string s && s == "NoticeInRoomDestroy";
        if (isDestroy)
        {
            roomData = default;
        }
        else
        {
            // 检查自己是否还在房间中（可能已被踢出），优先用 playerId
            bool isStillMember = false;
            int selfPid = GameManager.Instance.playerId;
            if (selfPid > 0 && newRoom.playerIds != null)
            {
                for (int i = 0; i < newRoom.playerIds.Length; i++)
                {
                    if (newRoom.playerIds[i] == selfPid) { isStillMember = true; break; }
                }
            }
            else if (newRoom.playerNames != null)
            {
                for (int i = 0; i < newRoom.playerNames.Length; i++)
                {
                    if (newRoom.playerNames[i].Equals(GameManager.Instance.userName))
                    { isStillMember = true; break; }
                }
            }
            roomData = isStillMember ? newRoom : default;
        }
        RefreshUI();
        return false;
    }

    private bool OnServerStartGame(params  object[] args)
    {
        StartGameResponse pack = (StartGameResponse)args[0];
        if (pack.gameModel == 2)
        {
            if (!pack.success)
            {
                CommonUtility.ShowUIMessage(MessageEventType.ServerNotFindRoom);
                return false;
            }
            roomData.status = 3;
            EventDispatcher.PostEvent(MessageEvent.EnterGame, this, GameManager.GameModel.Team, roomData);
        }
        return false;
    }

    private bool ServerRoomPlayerOnlineStatus(params object[] args)
    {
        PlayerConnectionEvent evt = (PlayerConnectionEvent)args[0];
        bool onlineStatus = evt.isDisconnected;
        if (onlineStatus != isHasDisconnect)
        {
            isHasDisconnect = onlineStatus;
        }
        // 优先用 playerId 匹配，回退到用户名
        foreach (MyRoomItem playerUI in all_playerList)
        {
            if ((evt.playerId > 0 && playerUI.playerId == evt.playerId)
                || playerUI.playerName.Equals(evt.username))
            {
                playerUI.SetPlayerStatus(!onlineStatus);
                break;
            }
        }
        return false;
    }
}

public class MyRoomItem : BaseUI
{
    private RoomListPanel rootPanel;
    private MyRoomUI bindUI;
    public string playerName;
    /// <summary>玩家的唯一ID（服务端分配）</summary>
    public int playerId;
    private Transform masterIcon;
    private TextMeshProUGUI nameText, tagText, status;
    private Button switchBtn, kickoutBtn;
    private int RoomID { get { return bindUI.RoomID; } }
    private bool isSelf;
    private bool isMaster;
    private bool isOnline = true;

    protected override void OnEnable()
    {
        base.OnEnable();
        EventDispatcher.AddObserver(this, MessageEvent.OnSwitchRoomMasterBack, OnServerSwitchRoomMasterBack, null);
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        EventDispatcher.RemoveObserver(this, MessageEvent.OnSwitchRoomMasterBack, null);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventDispatcher.RemoveObserver(this, MessageEvent.OnSwitchRoomMasterBack, null);
    }

    public void Init(RoomListPanel panel, MyRoomUI bindUI)
    {
        base.Init();
        this.rootPanel = panel;
        this.bindUI = bindUI;
        isOnline = true;
        tagText = transform.Find("tag").GetComponent<TextMeshProUGUI>();
        status = transform.Find("status").GetComponent<TextMeshProUGUI>();
        masterIcon = transform.Find("masterIcon");
        nameText = transform.Find("Name/Text").GetComponent<TextMeshProUGUI>();
        switchBtn = transform.Find("switch").GetComponent<Button>();
        switchBtn.onClick.AddListener(SwitchRoomMasterClick);
        kickoutBtn = transform.Find("kickout").GetComponent<Button>();
        kickoutBtn.onClick.AddListener(KickoutRoomClick);
    }

    private void SwitchRoomMasterClick()
    {
        int capturedRoomID = RoomID;
        int capturedPlayerId = playerId;
        string capturedPlayerName = playerName;
        CommonUtility.ShowTipPanel(MessageEventType.IsSwitchRoomMasterTip, () => {
            CustomNetworkManager.singleton.SendSwitchRoomMaster(capturedRoomID, capturedPlayerName, capturedPlayerId);
        });
    }
    private void KickoutRoomClick()
    {
        int capturedRoomID = RoomID;
        int capturedPlayerId = playerId;
        string capturedPlayerName = playerName;
        CommonUtility.ShowTipPanel(MessageEventType.IsKickoutRoomTip, () => {
            CustomNetworkManager.singleton.SendKickoutRoom(capturedRoomID, capturedPlayerName, capturedPlayerId);
        });
    }

    public void SetPlayerStatus(bool online)
    {
        isOnline = online;
        string colorStr = isOnline ? "#57FF7E" : "#FF5875";
        ColorUtility.TryParseHtmlString(colorStr, out Color textColor);
        status.color = textColor;
        status.text = online ? MessageEvent.allMessageStr[MessageEventType.Online] : MessageEvent.allMessageStr[MessageEventType.Offline];
    }

    public void RefreshUI()
    {
        if (string.IsNullOrEmpty(playerName))
        {
            isOnline = true;
            gameObject.SetActive(false);
            return;
        }
        if (isMaster)
        {
            tagText.text = isSelf ? "房主 + 自己" : "房主";
            masterIcon.gameObject.SetActive(true);
            switchBtn.gameObject.SetActive(false);
            kickoutBtn.gameObject.SetActive(false);
        }
        else
        {
            tagText.text = isSelf ? "自己" : "";
            masterIcon.gameObject.SetActive(false);
            switchBtn.gameObject.SetActive(bindUI.PlayerIsMaster);
            kickoutBtn.gameObject.SetActive(bindUI.PlayerIsMaster);
        }
        nameText.text = playerName;
        SetPlayerStatus(isOnline);
    }
    public void RefreshUI(int roomID, bool isToMaster, string playerNameParam, int playerIdParam = -1)
    {
        playerName = playerNameParam;
        playerId = playerIdParam;
        isSelf = playerIdParam > 0
            ? (playerIdParam == GameManager.Instance.playerId)
            : playerNameParam.Equals(GameManager.Instance.userName);
        isMaster = isToMaster;
        RefreshUI();
    }

    private bool OnServerSwitchRoomMasterBack(params object[] args)
    {
        int roomID = (int)args[0];
        if (roomID != RoomID) return false;
        string newMasterName = args[1] as string;
        isMaster = newMasterName.Equals(this.playerName);
        RefreshUI();
        return false;
    }
}
