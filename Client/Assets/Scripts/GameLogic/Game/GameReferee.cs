using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public struct CleanupTileMap
{
    public Vector3Int pos;
    public Tilemap tilemap;
    public TileBase tile;
    public CleanupTileMap(Tilemap tileMap, Vector3Int pos, TileBase tile)
    {
        this.tilemap = tileMap;
        this.pos = pos;
        this.tile = tile;
    }
}

/// <summary>
/// 游戏裁判
/// </summary>
public class GameReferee : Singleton<GameReferee>
{
    private Dictionary<string, Player> allPlayers;
    private Dictionary<Transform, FSM> enermyCaches;
    // 使用数组而非 List<T>：CleanupTileMap 是热更程序集中的 struct，
    // List<CleanupTileMap> 的泛型方法（Add/get_Item）在 AOT 侧无法提前生成。
    private CleanupTileMap[] cleanupTiles = new CleanupTileMap[0];
    private int cleanupTileCount;
    private List<GameObject> otherDeatroyCaches;
    /// <summary>
    /// 终点
    /// </summary>
    public Transform destinationPoint {  get; private set; }
    public float destinationTotalDistance { get; private set; }
    /// <summary>
    /// 游戏计时
    /// </summary>
    public const int RUN_GAME_TIMER = 1800;
    /// <summary>
    /// 游戏计时器句柄
    /// </summary>
    private int _gameTimerHandle;
    private int _gameTimer;
    /// <summary>
    /// 游戏剩余时间
    /// </summary>
    public int SurplusGameTimer
    {
        get { return RUN_GAME_TIMER - _gameTimer; }
    }

    public GameReferee()
    {
        allPlayers = new Dictionary<string, Player>();
        enermyCaches = new Dictionary<Transform, FSM>();
        otherDeatroyCaches = new List<GameObject>();
    }

    public void Initialize()
    {
        allPlayers.Clear();
        enermyCaches.Clear();
        cleanupTileCount = 0;
        otherDeatroyCaches.Clear();
        RemoveObserverEvents();
        AddObserverEvents();
    }
    private void AddObserverEvents()
    {
        EventDispatcher.AddObserver(this, MessageEvent.EnterGame, GameReady, null);
        EventDispatcher.AddObserver(this, MessageEvent.QuitGame, OnQuitGame, null);
        EventDispatcher.AddObserver(this, MessageEvent.GameOver, OnGameOver, null);
        EventDispatcher.AddObserver(this, MessageEvent.GameWin, OnGameWin, null);
        EventDispatcher.AddObserver(this, MessageEvent.AgainGame, OnGameAgain, null);
        EventDispatcher.AddObserver(this, MessageEvent.WaitingForGameRuning, StartGameRuning, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnMirrorPlayerSpawned, OnMirrorPlayerSpawned, null);
    }
    private void RemoveObserverEvents()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.EnterGame, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.QuitGame, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.GameOver, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.GameWin, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.AgainGame, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.WaitingForGameRuning, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnMirrorPlayerSpawned, null);
    }

    private void UpdatePlayerUI()
    {
        if (Time.frameCount % 10 != 0) return;
        foreach (var kvp in allPlayers)
        {
            kvp.Value.OnUpdateUI();
        }
    }

    private bool GameReady(params object[] args)
    {
        GameManager.GameModel gameModel = (GameManager.GameModel)args[0];
        switch (gameModel)
        {
            case GameManager.GameModel.Single:
            case GameManager.GameModel.Infinity:
                AddPlayerIfNotExists(GameManager.Instance.userName);
                break;
            case GameManager.GameModel.Double:
                string emeryName = (string)args[1];
                AddPlayerIfNotExists(emeryName);
                AddPlayerIfNotExists(GameManager.Instance.userName);
                break;
            case GameManager.GameModel.Team:
                RoomInfo roomInfo = (RoomInfo)args[1];
                for (int i = 0; i < roomInfo.playerNames.Length; i++)
                {
                    AddPlayerIfNotExists(roomInfo.playerNames[i]);
                }
                break;
            default:
                break;
        }
        return false;
    }

    private void AddPlayerIfNotExists(string playerName)
    {
        if (!allPlayers.ContainsKey(playerName))
        {
            allPlayers.Add(playerName, new Player(playerName));
        }
    }

    private bool OnQuitGame(params object[] args)
    {
        _gameTimer = 0;
        TimerMgr.Instance.Unschedule(_gameTimerHandle);
        UpdateManager.Instance.Unregister(UpdatePlayerUI);
        destinationPoint = null;
        destinationTotalDistance = 0;
        foreach (var kvp in allPlayers)
        {
            kvp.Value.OnQuitGame();
            kvp.Value.Dispose();
        }
        allPlayers.Clear();
        enermyCaches.Clear();
        cleanupTileCount = 0;
        otherDeatroyCaches.Clear();
        return false;
    }
    private bool OnGameOver(params object[] args)
    {
        _gameTimer = 0;
        TimerMgr.Instance.Unschedule(_gameTimerHandle);
        if (allPlayers.TryGetValue(GameManager.Instance.userName, out Player player))
        {
            player.OnGameOver();
        }
        UIManager.instance.OpenPanel(UIPanelType.GameOver);
        return false;
    }
    private bool OnGameWin(params object[] args)
    {
        _gameTimer = 0;
        TimerMgr.Instance.Unschedule(_gameTimerHandle);
        string winPlayer = (string)args[0];
        if (allPlayers.TryGetValue(winPlayer, out Player player))
        {
            player.OnGameWin();
        }
        UIManager.instance.OpenPanel(UIPanelType.GameWin);
        return false;
    }
    private bool OnGameAgain(params object[] args)
    {
        StartGameRuning();
        if (GameManager.Instance.gameModel == GameManager.GameModel.None || GameManager.Instance.gameModel == GameManager.GameModel.Single || GameManager.Instance.gameModel == GameManager.GameModel.Infinity)
        {
            foreach (var kvp in allPlayers)
            {
                kvp.Value.OnGameAgainOnSingleModel();
                if (kvp.Value.isSelf)
                {
                    EventDispatcher.PostEvent(MessageEvent.OnRegistSelfPlayer, this, kvp.Value);
                }
            }
        }
        else
        {
            if (allPlayers.TryGetValue(GameManager.Instance.userName, out Player player))
            {
                player.OnGameAgainOnMultipleModel();
            }
        }
        for (int i = 0; i < cleanupTileCount; i++)
        {
            CleanupTileMap tile = cleanupTiles[i];
            tile.tilemap.SetTile(tile.pos, tile.tile);
        }
        cleanupTileCount = 0;
        for (int i = 0; i < otherDeatroyCaches.Count; i++)
        {
            otherDeatroyCaches[i].SetActive(true);
        }
        return false;
    }

    public void EnermyOnHit(Transform tran)
    {
        if (enermyCaches.ContainsKey(tran))
        {
            enermyCaches[tran].OnHit();
        }
    }

    public void RegisterFSM(Transform tran, FSM fsm)
    {
        if (!enermyCaches.ContainsKey(tran))
        {
            enermyCaches.Add(tran, fsm);
        }
        else
        {
            enermyCaches[tran] = fsm;
        }
    }

    public void UnRegisterFSM(Transform tran)
    {
        if (enermyCaches.ContainsKey(tran))
        {
            enermyCaches.Remove(tran);
        }
    }

    public void AddOneCleanupTile(Vector3Int toPos, Tilemap tilemap, TileBase tile)
    {
        CleanupTileMap cleanupTile = new CleanupTileMap(tilemap, toPos, tile);
        if (cleanupTileCount >= cleanupTiles.Length)
            System.Array.Resize(ref cleanupTiles, Mathf.Max(1, cleanupTiles.Length * 2));
        cleanupTiles[cleanupTileCount++] = cleanupTile;
    }

    public void AddOneDeatroyCache(GameObject obj)
    {
        otherDeatroyCaches.Add(obj);
    }

    public void GeneratePlayer()
    {
        GameObject rolePrefab = ResourceManager.Instance.LoadAsset<GameObject>("Roles_Player");
        if (rolePrefab == null)
        {
            Debug.LogError("Generate player fail!");
            return;
        }
        foreach (var kvp in allPlayers)
        {
            bool isSelf = kvp.Value.PlayName.Equals(GameManager.Instance.userName);
            if (isSelf)
            {
                GameObject newPlayer = GameObject.Instantiate(rolePrefab);
                newPlayer.transform.position = Vector3.zero;
                newPlayer.name = "Player";
                newPlayer.transform.Find("Sign").gameObject.SetActive(allPlayers.Count > 1);
                GameLogicEntry.SetupPlayerComponents(newPlayer);
                kvp.Value.PlayObj = newPlayer.transform;
            }
        }
        UpdateManager.Instance.RegisterLateUpdate(UpdatePlayerUI);
    }

    private Vector3 _mirrorSpawnPos;

    public void GenerateGameScene()
    {
        GameObject enPrefab = ResourceManager.Instance.LoadAsset<GameObject>("Maps_Environment");
        if (enPrefab == null)
        {
            Debug.LogError("Generate game environment fail!");
            return;
        }
        GameObject newEn = GameObject.Instantiate(enPrefab);
        newEn.name = enPrefab.name;
        Vector3 drawPos = GameObject.Find($"Environment/DrawRole").transform.position;
        _mirrorSpawnPos = drawPos;
        destinationPoint = newEn.transform.Find("Grid/Mechanism/WinDoor");
        destinationTotalDistance = Vector2.Distance(drawPos, destinationPoint.position);

        bool isMirrorMode = GameManager.Instance.IsLoginServer && GameManager.Instance.connectState
                            && (GameManager.Instance.gameModel == GameManager.GameModel.Double
                                || GameManager.Instance.gameModel == GameManager.GameModel.Team);

        foreach (var kvp in allPlayers)
        {
            bool isSelf = kvp.Value.PlayName.Equals(GameManager.Instance.userName);

            if (isMirrorMode)
            {
                // Mirror多人模式: 玩家由服务端在双方场景就绪后统一生成, 此处仅对已生成的玩家重新定位
                if (kvp.Value.PlayObj != null)
                {
                    kvp.Value.PlayObj.position = drawPos;
                    Transform sign = kvp.Value.PlayObj.Find("Sign");
                    if (sign != null) sign.gameObject.SetActive(isSelf && allPlayers.Count > 1);
                }
            }
            else if (isSelf)
            {
                GameObject rolePrefab = ResourceManager.Instance.LoadAsset<GameObject>("Roles_Player");
                if (rolePrefab == null)
                {
                    Debug.LogError("Generate player fail!");
                    return;
                }
                GameObject newPlayer = GameObject.Instantiate(rolePrefab);
                newPlayer.transform.position = drawPos;
                newPlayer.name = "Player";
                newPlayer.transform.Find("Sign").gameObject.SetActive(allPlayers.Count > 1);
                GameLogicEntry.SetupPlayerComponents(newPlayer);
                kvp.Value.PlayObj = newPlayer.transform;
            }
            else if (kvp.Value.PlayObj != null)
            {
                kvp.Value.PlayObj.position = drawPos;
            }
            else
            {
                GameObject netPlayerPrefab = ResourceManager.Instance.LoadAsset<GameObject>("Roles_NetPlayer");
                if (netPlayerPrefab == null)
                {
                    Debug.LogError("Generate NetPlayer fail!");
                    return;
                }
                GameObject newPlayer = GameObject.Instantiate(netPlayerPrefab);
                newPlayer.transform.position = drawPos;
                newPlayer.name = "NetPlayer";
                newPlayer.transform.Find("Sign").gameObject.SetActive(false);
                MirrorPlayer mp = newPlayer.GetComponent<MirrorPlayer>();
                if (mp != null) mp.playerName = kvp.Value.PlayName;
                kvp.Value.PlayObj = newPlayer.transform;
            }
        }
        UpdateManager.Instance.RegisterLateUpdate(UpdatePlayerUI);
    }

    public void OnGameUIPanelShow(GameUIPanel gameUI, out Player self)
    {
        self = null;
        int index = 0;
        foreach (var kvp in allPlayers)
        {
            if (kvp.Value.isSelf)
            {
                self = kvp.Value;
            }
            gameUI.players[index].Initialize(kvp.Value.PlayName, kvp.Value.isSelf, gameUI);
            if (GameManager.Instance.gameModel != GameManager.GameModel.Infinity)
            {
                kvp.Value.PlayerUI = gameUI.players[index];
                gameUI.players[index].gameObject.SetActive(true);
            }
            else
            {
                gameUI.players[index].gameObject.SetActive(false);
            }
            index++;
        }
        for (int i = index; i < gameUI.players.Length; i++)
        {
            gameUI.players[i].gameObject.SetActive(false);
        }
    }

    public int GetPlayerStarCount(string userName)
    {
        if (allPlayers.TryGetValue(userName, out Player player))
        {
            return player.StarCount;
        }
        return 0;
    }

    private void OnRuningGameTimer(object arg)
    {
        EventDispatcher.PostEvent(MessageEvent.RefreshGameUITimer, this, SurplusGameTimer);
        if (_gameTimer == RUN_GAME_TIMER)
        {// 游戏时间到
            TimerMgr.Instance.Unschedule(_gameTimerHandle);
            _gameTimerHandle = 0;
            EventDispatcher.PostEvent(MessageEvent.GameEnd, this, null);
            UIManager.instance.OpenPanel(UIPanelType.GameOver);
            return;
        }
        _gameTimer++;
    }
    private bool OnMirrorPlayerSpawned(params object[] args)
    {
        string playerName = (string)args[0];
        MirrorPlayer mp = (MirrorPlayer)args[1];

        if (!allPlayers.ContainsKey(playerName))
        {
            Player player = new Player(playerName);
            allPlayers.Add(playerName, player);
            player.PlayObj = mp.transform;
        }
        else
        {
            allPlayers[playerName].PlayObj = mp.transform;
        }

        // 定位到游戏场景的出生点
        if (_mirrorSpawnPos != Vector3.zero)
            mp.transform.position = _mirrorSpawnPos;

        bool isSelf = playerName == GameManager.Instance.userName;
        Transform sign = mp.transform.Find("Sign");
        if (sign != null) sign.gameObject.SetActive(isSelf && allPlayers.Count > 1);

        // Mirror 多人模式下移除本地玩家的 PlayerDeathCheck
        // (本地玩家的死亡由现有 EventDispatcher 流程处理；PlayerDeathCheck 已在别处移除)
        if (isSelf
            && GameManager.Instance.gameModel != GameManager.GameModel.Infinity)
        {
            PlayerDeathCheck pdc = mp.GetComponentInChildren<PlayerDeathCheck>();
            if (pdc != null)
                GameObject.Destroy(pdc);
        }

        return false;
    }

    private bool StartGameRuning(params object[] args)
    {
        _gameTimer = 0;
        TimerMgr.Instance.Unschedule(_gameTimerHandle);
        if (GameManager.Instance.gameModel != GameManager.GameModel.Infinity)
            _gameTimerHandle = TimerMgr.Instance.Schedule(OnRuningGameTimer, -1, 1);
        return false;
    }
}
}