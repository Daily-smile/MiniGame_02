using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using LF.Framework;

namespace LF.Network
{
/// <summary>
/// Mirror 游戏管理器 (替代部分 GameReferee 的多人生成逻辑)
///
/// 职责：
///   - 管理所有在线玩家 (MirrorPlayer 列表)
///   - 服务器端玩家加入/离开处理
///   - 游戏开始/结束计时器
///   - 场景内玩家生成点管理
///
/// 对照：
///   GameReferee.GenerateGameScene()     → ServerSpawnAllPlayers()
///   GameReferee.allPlayers 字典          → serverPlayers 字典
///   GameReferee.OnQuitGame              → OnServerGameEnd
///   RoomPack/PlayerData                 → MirrorPlayer SyncVars
/// </summary>
public class MirrorGameManager : NetworkBehaviour
{
    public static MirrorGameManager singleton { get; private set; }

    [Header("Spawn Settings")]
    [Tooltip("玩家出生点")]
    public Transform[] spawnPoints;

    [Tooltip("游戏场景名称")]
    public string gameSceneName = "GameScene";

    [Header("Game Timer")]
    [Tooltip("游戏最大时长(秒)")]
    [SyncVar]
    public int maxGameTime = 1800;

    [SyncVar]
    private int _elapsedTime;

    /// <summary>剩余游戏时间</summary>
    public int SurplusTime => maxGameTime - _elapsedTime;

    /// <summary>服务器端所有玩家</summary>
    private readonly Dictionary<string, MirrorPlayer> serverPlayers = new Dictionary<string, MirrorPlayer>();

    /// <summary>游戏是否正在运行</summary>
    [SyncVar]
    public bool isGameRunning;

    private void Awake()
    {
        if (singleton != null && singleton != this)
        {
            Destroy(gameObject);
            return;
        }
        singleton = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[Mirror] 游戏管理器 (服务器) 已启动");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[Mirror] 游戏管理器 (客户端) 已启动, isGameRunning={isGameRunning}");
    }

    // ==================== 玩家管理 (Server) ====================

    /// <summary>
    /// 注册玩家 (由 MirrorPlayer.CmdRegisterPlayer 调用)
    /// </summary>
    [Server]
    public void RegisterPlayer(MirrorPlayer player)
    {
        if (string.IsNullOrEmpty(player.playerName)) return;

        if (serverPlayers.ContainsKey(player.playerName))
        {
            serverPlayers[player.playerName] = player;
        }
        else
        {
            serverPlayers.Add(player.playerName, player);
        }

        Debug.Log($"[Mirror] 服务器注册玩家: {player.playerName}, 当前玩家数: {serverPlayers.Count}");
    }

    /// <summary>
    /// 注销玩家
    /// </summary>
    [Server]
    public void UnregisterPlayer(string playerName)
    {
        if (serverPlayers.Remove(playerName))
        {
            Debug.Log($"[Mirror] 服务器注销玩家: {playerName}");
        }
    }

    /// <summary>
    /// 获取指定玩家
    /// </summary>
    public bool TryGetPlayer(string name, out MirrorPlayer player)
    {
        return serverPlayers.TryGetValue(name, out player);
    }

    /// <summary>
    /// 在服务器端生成所有玩家的游戏对象
    /// 替代 GameReferee.GenerateGameScene() 的 NetPlayer 生成逻辑
    /// </summary>
    [Server]
    public void ServerSpawnAllPlayers(GameObject playerPrefab)
    {
        int index = 0;
        foreach (var kvp in serverPlayers)
        {
            MirrorPlayer existingPlayer = kvp.Value;
            if (existingPlayer == null) continue;

            Vector3 spawnPos = (spawnPoints != null && index < spawnPoints.Length)
                ? spawnPoints[index].position
                : new Vector3(index * 2, 2, 0);

            // 如果玩家已有 GameObject (connectionToClient 关联的), 则移动它
            if (existingPlayer.netIdentity != null)
            {
                existingPlayer.transform.position = spawnPos;
            }

            index++;
        }
    }

    // ==================== 游戏流程 (替代 GameReferee 的事件驱动 State) ====================

    /// <summary>
    /// 开始游戏 (由 CustomNetworkManager 或 StartGameMessage 触发)
    /// </summary>
    [Server]
    public void ServerStartGame()
    {
        if (isGameRunning) return;

        isGameRunning = true;
        _elapsedTime = 0;

        RpcOnGameStart();

        Debug.Log($"[Mirror] 游戏开始, {serverPlayers.Count} 名玩家");
    }

    /// <summary>
    /// 结束游戏
    /// </summary>
    [Server]
    public void ServerEndGame()
    {
        if (!isGameRunning) return;

        isGameRunning = false;
        RpcOnGameEnd();

        Debug.Log("[Mirror] 游戏结束");
    }

    private float _lastTickTime;

    [Server]
    private void Update()
    {
        if (!isGameRunning) return;

        // 按实际时间秒级计时(替代帧率依赖的 _elapsedTime++)
        if (Time.time - _lastTickTime >= 1f)
        {
            _lastTickTime = Time.time;
            _elapsedTime++;
            if (_elapsedTime >= maxGameTime)
            {
                ServerEndGame();
                return;
            }
            RpcTimerTick(SurplusTime);
        }
    }

    // ==================== RPCs ====================

    [ClientRpc]
    private void RpcOnGameStart()
    {
        Debug.Log("[Mirror] 游戏开始 RPC");
        EventDispatcher.PostEvent(MessageEvent.WaitingForGameRuning, this, null);
        EventDispatcher.PostEvent(MessageEvent.StartGameRuning, this, null);
    }

    [ClientRpc]
    private void RpcOnGameEnd()
    {
        Debug.Log("[Mirror] 游戏结束 RPC");
        EventDispatcher.PostEvent(MessageEvent.GameEnd, this, null);
    }

    [ClientRpc]
    private void RpcTimerTick(int surplusSeconds)
    {
        EventDispatcher.PostEvent(MessageEvent.RefreshGameUITimer, this, surplusSeconds);
    }

    // ==================== 工具 ====================

    private void OnDestroy()
    {
        if (singleton == this)
            singleton = null;
    }
}
}
