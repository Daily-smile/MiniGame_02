using System.Collections;
using System.Collections.Generic;

namespace LF.Framework
{
public enum MessageEventType
{
    Unknown,
    ServerReturnNone,
    ServerNotRoom,
    ServerNotFindRoom,
    ServerExistSomeRoom,
    ClientOK,
    ClientFail,
    ClientError,
    NamePwdIsEmpty,
    Online,
    Offline,
    LogonOK,
    LogonFail,
    LoginOK,
    LoginFail,
    OpenPanelRefreshRoom,
    RefreshBtnRefreshRoom,
    CreateRoomNameEmpty,
    FindRoomNameEmpty,
    CreateRoomOK,
    CreateRoomFail,
    DestroyRoomOK, 
    DestroyRoomFail,
    IsQuitRoomPanelOnRoom,
    IsQuitRoom,
    NoJoinRoomQuitRoom,
    MyRoomIsEmpty,
    RoomHallIsEmpty,
    QueryRoomOK,
    JoinRoomOK,
    JoinRoomFail,
    InRoomToJoinRoom,
    InRoomToCreateRoom,
    SwitchRoomMasterToSelf,
    IsSwitchRoomMasterTip,
    IsKickoutRoomTip,
    NoticeKickoutRoom,
    NoticeRoomDestroy,
    NotInRoom,
    DoubleModelMatchStart,
    DoubleModelMatchOK,
    DoubleModelMatchFail,
    RoomFullTip,
    RoomFullTipToJoin,
    RoomGameTip,
    RoomGameTipToJoin,
    NoRoomMasterStartGame,
    RoomHasDisconnectStartGame,
    ChatRoomSendMsgError,
    DoubleModelStartMatchTip,
    DoubleModelMatchOKTip,
    DoubleModelMatchFailTip,
    DoubleModelIsQuitMatchTip,
    DoubleModelQuitMatchTip,
    DisconnectStatusLoginTip,
    LoadingInitializeStr,
    LoadingSceneStr,
    LoadingAssetsStr,
    ServerGameErrorTip,
    WaitingForStartGame,
    StartGameRuningTimer,
    NoSelectPropTip,
}
public static class MessageEvent
{
    public static readonly Dictionary<MessageEventType, string> allMessageStr = new Dictionary<MessageEventType, string>() 
    {
        {MessageEventType.Unknown, "未知错误！"},
        {MessageEventType.ServerReturnNone, "服务器数据错误！"},
        {MessageEventType.ServerNotRoom, "房间为空！"},
        {MessageEventType.ServerNotFindRoom, "查无房间!"},
        {MessageEventType.ServerExistSomeRoom, "已存在同名房间!"},
        {MessageEventType.ClientOK, "连接成功！"},
        {MessageEventType.ClientFail, "服务器连接失败...尝试重连中..."},
        {MessageEventType.ClientError, "服务器出现错误！"},
        {MessageEventType.NamePwdIsEmpty, "用户名和密码不能为空！"},
        {MessageEventType.LogonOK, "注册成功，请登录！"},
        {MessageEventType.LogonFail, "注册失败，该用户名已被注册！"},
        {MessageEventType.LoginOK, "登录成功"},
        {MessageEventType.LoginFail, "登录失败，请检查用户名和密码！"},
        {MessageEventType.Online, "在线"},
        {MessageEventType.Offline, "离线"},
        {MessageEventType.RefreshBtnRefreshRoom, "房间已刷新"},
        {MessageEventType.CreateRoomNameEmpty, "创建房间的名称不能为空！"},
        {MessageEventType.FindRoomNameEmpty, "查找房间的名称不能为空！"},
        {MessageEventType.CreateRoomOK, "房间创建成功！"},
        {MessageEventType.CreateRoomFail, "房间创建失败！"},
        {MessageEventType.DestroyRoomOK, "房间已解散"},
        {MessageEventType.DestroyRoomFail, "解散房间失败"},
        {MessageEventType.QueryRoomOK, "搜索房间完成"},
        {MessageEventType.JoinRoomOK, "成功加入房间"},
        {MessageEventType.JoinRoomFail, "加入房间失败！"},
        {MessageEventType.InRoomToJoinRoom, "是否退出房间，并加入新房间？"},
        {MessageEventType.InRoomToCreateRoom, "是否退出房间，并创建新房间？"},
        {MessageEventType.MyRoomIsEmpty, "房间为空，请回房间大厅加入一个吧！"},
        {MessageEventType.RoomHallIsEmpty, "未检测到房间，请重新刷新一下吧！"},
        {MessageEventType.IsQuitRoom, "是否退出该房间？"},
        {MessageEventType.IsQuitRoomPanelOnRoom, "是否退出房间并返回主界面？"},
        {MessageEventType.NoJoinRoomQuitRoom, "您未加入任何房间，无需退出"},
        {MessageEventType.SwitchRoomMasterToSelf, "您已成为房主"},
        {MessageEventType.IsSwitchRoomMasterTip, "是否确认房主切换？"},
        {MessageEventType.IsKickoutRoomTip, "是否确认踢出该房间？"},
        {MessageEventType.NoticeKickoutRoom, "你已被房主踢出房间"},
        {MessageEventType.NoticeRoomDestroy, "您所在的房间已被销毁！"},
        {MessageEventType.NotInRoom, "您已不在该房间！"},
        {MessageEventType.DoubleModelMatchStart, "正在匹配敌方...请稍后..."},
        {MessageEventType.DoubleModelMatchOK, "匹配成功！"},
        {MessageEventType.DoubleModelMatchFail, "匹配失败！请稍后重试..."},
        {MessageEventType.RoomFullTip, "房间人数已满！"},
        {MessageEventType.RoomFullTipToJoin, "房间人数已满，无法加入该房间！请刷新看看其它房间..."},
        {MessageEventType.RoomGameTip, "房间处于游戏状态！"},
        {MessageEventType.RoomGameTipToJoin, "房间处于游戏状态，无法加入该房间！请刷新看看其它房间..."},
        {MessageEventType.NoRoomMasterStartGame, "您不是房主，无法开始游戏！"},
        {MessageEventType.RoomHasDisconnectStartGame, "房间内有离线玩家，无法开始游戏！"},
        {MessageEventType.ChatRoomSendMsgError, "消息发送失败！"},
        {MessageEventType.DoubleModelStartMatchTip, "开始匹配中..."},
        {MessageEventType.DoubleModelMatchOKTip, "匹配成功"},
        {MessageEventType.DoubleModelMatchFailTip, "匹配失败"},
        {MessageEventType.DoubleModelIsQuitMatchTip, "是否退出匹配？退出后将返回主页。"},
        {MessageEventType.DoubleModelQuitMatchTip, "已退出匹配！"},
        {MessageEventType.DisconnectStatusLoginTip, "离线状态下无法登录，是否开始单机模式？"},
        {MessageEventType.LoadingInitializeStr, "初始化加载..."},
        {MessageEventType.LoadingSceneStr, "场景加载中..."},
        {MessageEventType.LoadingAssetsStr, "资源加载中..."},
        {MessageEventType.ServerGameErrorTip, "服务器检测到游戏异常..."},
        {MessageEventType.WaitingForStartGame, "等待准备开始..."},
        {MessageEventType.StartGameRuningTimer, "游戏开始倒计时"},
        {MessageEventType.NoSelectPropTip, "当前未选中任何道具！"},
    };
    public static readonly string ServerConnectChange = "ServerConnectChange";
    public static readonly string OnLogonBack = "OnLogonBack";
    public static readonly string OnLoginOK = "OnLoginOK";
    public static readonly string OnLoginOKBack = "OnLoginOKBack";
    public static readonly string OnLoginFail = "OnLoginFail";
    public static readonly string OnShowUIMessage = "ShowUIMessage";
    public static readonly string OnRefreshRoomList = "OnRefreshRoomList";
    public static readonly string OnCreateRoomBack = "OnCreateRoomBack";
    public static readonly string OnDestroyRoomBack = "OnDestroyRoomBack";
    public static readonly string OnJoinRoomBack = "OnJoinRoomBack";
    public static readonly string OpenTipPanel = "OpenTipPanel";
    public static readonly string UpdatePlayerRoomPack = "UpdatePlayerRoomPack";
    public static readonly string OnSwitchRoomMasterBack = "OnSwitchRoomMasterBack";
    public static readonly string OnKickoutRoomBack = "OnKickoutRoomBack";
    public static readonly string RoomPlayerRefreshRespond = "RoomPlayerRefreshRespond";
    public static readonly string StartGame = "StartGame";
    public static readonly string OnReciveChatRoomMsgUpdate = "OnReciveChatRoomMsgUpdate";
    public static readonly string OnQuitMatch = "OnQuitMatch";
    public static readonly string OnReconnectFail = "OnReconnectFail";
    public static readonly string RoomPlayerOnlineStatus = "RoomPlayerOnlineStatus";
    public static readonly string EnterGame = "OnEnterGame";
    public static readonly string QuitGame = "OnQuitGame";
    public static readonly string GameOver = "GameOver";
    public static readonly string GameWin = "OnGameWin";
    public static readonly string GameEnd = "OnGameEnd";
    public static readonly string ForceDead = "ForceDead";
    public static readonly string RefreshGameUITimer = "RefreshGameUITimer";
    public static readonly string SetGameModel = "SetGameModel";
    public static readonly string AgainGame = "AgainGame";
    public static readonly string UpdateNetPlayer = "UpdateNetPlayer";
    public static readonly string InGameGetStar = "InGameGetStar";
    public static readonly string OnPlayerDead = "OnPlayerDead";
    public static readonly string OnNetPlayerDead = "OnNetPlayerDead";
    public static readonly string StartGameRuning = "StartGameRuning";
    public static readonly string WaitingForGameRuning = "WaitingForGameRuning";
    public static readonly string PlayerOnHit = "PlayerOnHit";
    public static readonly string OnPlayerFullRebirth = "OnPlayerFullRebirth";
    public static readonly string PlayerGetWeapon = "PlayerGetWeapon";
    public static readonly string PlayerGetProp = "PlayerGetProp";
    public static readonly string PlayerUseProp = "PlayerUseProp";
    public static readonly string OnRegistSelfPlayer = "OnRegistSelfPlayer";
    public static readonly string OnGameUILoad = "OnGameUILoad";
    public static readonly string UpdateInfinityScore = "UpdateInfinityScore";
    public static readonly string RefreshInfinityScoreUI = "RefreshInfinityScoreUI";
    public static readonly string RefreshInfinitySaveMaxScoreUI = "RefreshInfinitySaveMaxScoreUI";
    public static readonly string InfinityModelSetGroundHitPoint = "InfinityModelSetGroundHitPoint";
    public static readonly string BackPlatformRebirth = "BackPlatformRebirth";
    /// <summary>Mirror网络玩家生成事件 (桥接Mirror→GameReferee)</summary>
    public static readonly string OnMirrorPlayerSpawned = "OnMirrorPlayerSpawned";
    /// <summary>Mirror SyncVar变更事件：HP变化 (args: oldHp, newHp)</summary>
    public static readonly string MirrorHpChanged = "MirrorHpChanged";
    /// <summary>Mirror SyncVar变更事件：死亡状态变化 (args: oldDead, newDead)</summary>
    public static readonly string MirrorDeadChanged = "MirrorDeadChanged";
    /// <summary>Mirror SyncVar变更事件：星星数量变化 (args: oldCount, newCount)</summary>
    public static readonly string MirrorStarCountChanged = "MirrorStarCountChanged";
    /// <summary>Mirror SyncVar变更事件：翻转方向变化 (args: oldVal, newVal)</summary>
    public static readonly string MirrorFlipXChanged = "MirrorFlipXChanged";

    // ──────────── 资源热更新事件 ────────────
    /// <summary>开始版本检查</summary>
    public static readonly string OnPatchCheckStart = "OnPatchCheckStart";
    /// <summary>获取到版本号 (args: string remoteVersion, string localVersion)</summary>
    public static readonly string OnPatchVersionGet = "OnPatchVersionGet";
    /// <summary>开始校验资源完整性（版本一致后触发）</summary>
    public static readonly string OnPatchVerifyStart = "OnPatchVerifyStart";
    /// <summary>开始下载更新 (args: int totalCount, long totalSizeBytes)</summary>
    public static readonly string OnPatchDownloadStart = "OnPatchDownloadStart";
    /// <summary>下载进度更新 (args: int currentCount, int totalCount, long currentSizeBytes, long totalSizeBytes)</summary>
    public static readonly string OnPatchDownloadProgress = "OnPatchDownloadProgress";
    /// <summary>下载完成</summary>
    public static readonly string OnPatchDownloadComplete = "OnPatchDownloadComplete";
    /// <summary>下载失败 (args: string errorMsg, int retryCount)</summary>
    public static readonly string OnPatchDownloadFailed = "OnPatchDownloadFailed";
    /// <summary>热更新流程结束（无论成功/失败/跳过） (args: bool success, string message)</summary>
    public static readonly string OnPatchFinish = "OnPatchFinish";
}
}
