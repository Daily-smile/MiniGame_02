using Mirror;
using UnityEngine;
using LF.Framework;

namespace LF.Network
{
/// <summary>
/// Mirror 网络玩家组件 —— 纯 SyncVar 数据层 + [Command]/[ClientRpc] 定义。
///
/// 组件分离架构 (Component Split):
///   - MirrorPlayer (本类):      仅持有 SyncVar 数据与 RPC 定义，不做 authority 判断
///   - LocalPlayerDriver:        仅本地玩家启用 → 输入控制、SyncVar 写入、调用 Command
///   - RemotePlayerView:         仅远程玩家启用 → 响应 SyncVar 事件 → 视觉表现
///
/// 同步对照：
///   原 UDP 手动同步                  → Mirror 替代方案
///   PlayerAnimator 60Hz 发送位置      → NetworkTransform (自动)
///   动画名 clip.name 字符串           → NetworkAnimator (自动)
///   flipX                            → [SyncVar] isFlipX
///   HP                               → [SyncVar] hp
///   死亡状态 isDead                   → [SyncVar] isDead
///   StarCount                        → [SyncVar] starCount
///   伤害事件 PlayerOnHit              → [Command] CmdTakeDamage → [ClientRpc] RpcOnHit
///   星星收集                          → [Command] CmdCollectStar
///   开火                              → [Command] CmdFire → [ClientRpc] RpcOnFire
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(NetworkTransformUnreliable))]
[RequireComponent(typeof(NetworkAnimator))]
public class MirrorPlayer : NetworkBehaviour
{
    [Header("Sync Vars - 替代原先通过 PlayerInfoPack UDP 同步的字段")]
    [SyncVar(hook = nameof(OnHpChanged))]
    public int hp = 3;

    [SyncVar(hook = nameof(OnDeadChanged))]
    public bool isDead;

    [SyncVar(hook = nameof(OnStarCountChanged))]
    public int starCount;

    [SyncVar(hook = nameof(OnFlipXChanged))]
    public bool isFlipX;

    [SyncVar]
    public string playerName = "";

    /// <summary>NetworkTransform 的同步精度配置</summary>
    [Header("NetworkTransform Config")]
    [Tooltip("位置同步灵敏度 (越小越精确，带宽越高)")]
    [Range(0.01f, 1f)]
    public float positionSensitivity = 0.01f;

    [Tooltip("客户端每秒钟向服务器发送位置的次数")]
    [Range(10, 60)]
    public int clientSendRate = 30;

    // ==================== Mirror 生命周期 ====================

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();

        // 通过钩子动态添加热更程序集中的驱动组件（避免预制体直接引用热更脚本导致序列化不匹配）
        MirrorPlayerComponentSetup.OnMirrorPlayerAuthority?.Invoke(gameObject);

        var localDriver = GetComponent<ILocalPlayerDriver>();
        if (localDriver != null)
            localDriver.Initialize();
        else
            Debug.LogError($"[Mirror] ILocalPlayerDriver 组件缺失于 {gameObject.name}!");

        EventDispatcher.PostEvent(MessageEvent.OnMirrorPlayerSpawned, this, playerName, this);
        Debug.Log($"[Mirror] 本地玩家获取权限: {playerName}");
    }

    public override void OnStopAuthority()
    {
        base.OnStopAuthority();

        var localDriver = GetComponent<ILocalPlayerDriver>();
        if (localDriver != null)
            localDriver.Cleanup();

        Debug.Log($"[Mirror] 本地玩家失去权限: {playerName}");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 通过钩子动态添加热更程序集中的视图/控制器组件（避免预制体直接引用热更脚本导致序列化不匹配）
        MirrorPlayerComponentSetup.OnMirrorPlayerCreated?.Invoke(gameObject);

        Debug.Log($"[MirrorPlayer] OnStartClient: playerName={playerName}, gameObject={gameObject.name}, "
                + $"authority={authority}");

        // 尝试驱动组件初始化：LocalPlayerDriver 和 RemotePlayerView 各自内部判断是否应该激活
        var remoteView = GetComponent<IRemotePlayerView>();
        if (remoteView != null)
            remoteView.Initialize();
        else
            Debug.LogError($"[Mirror] IRemotePlayerView 组件缺失于 {gameObject.name}!");

        // 始终触发，GameReferee 需要此事件注册本地/远程玩家
        EventDispatcher.PostEvent(MessageEvent.OnMirrorPlayerSpawned, this, playerName, this);
        Debug.Log($"[Mirror] 玩家已初始化: {playerName} ({(isLocalPlayer ? "本地" : "远程")})");
    }

    // ==================== SyncVar Hooks ====================

    private void OnHpChanged(int oldHp, int newHp)
    {
        // 无条件广播 SyncVar 变更事件，由 LocalPlayerDriver / RemotePlayerView 各自响应
        EventDispatcher.PostEvent(MessageEvent.MirrorHpChanged, this, oldHp, newHp);
    }

    private void OnDeadChanged(bool oldDead, bool newDead)
    {
        EventDispatcher.PostEvent(MessageEvent.MirrorDeadChanged, this, oldDead, newDead);
    }

    private void OnStarCountChanged(int oldCount, int newCount)
    {
        EventDispatcher.PostEvent(MessageEvent.MirrorStarCountChanged, this, oldCount, newCount);
    }

    private void OnFlipXChanged(bool oldVal, bool newVal)
    {
        Debug.Log($"[MirrorPlayer] OnFlipXChanged: playerName={playerName}, gameObject={gameObject.name}, authority={authority}, oldVal={oldVal}, newVal={newVal}");
        EventDispatcher.PostEvent(MessageEvent.MirrorFlipXChanged, this, oldVal, newVal);
    }

    // ==================== Commands (客户端 → 服务器) ====================

    /// <summary>
    /// 注册玩家到游戏 (替代原先通过 TCP 发送的加入/初始化逻辑)
    /// </summary>
    [Command]
    public void CmdRegisterPlayer(string name)
    {
        playerName = name;
        // 在服务器端的 MirrorGameManager 中注册
        if (MirrorGameManager.singleton != null)
        {
            MirrorGameManager.singleton.RegisterPlayer(this);
        }
    }

    /// <summary>
    /// 受伤害。HP=0 进入"最后一搏"复活（isDead=false），HP&lt;0 才真正死亡。
    /// </summary>
    [Command]
    public void CmdTakeDamage(string attackerName, int damage)
    {
        hp = Mathf.Max(0, hp - damage);
        isDead = hp == 0;

        // 通知其他客户端 (includeOwner=false 避免与权威端本地处理重复)
        RpcOnHit(playerName, isDead, hp);
    }

    /// <summary>
    /// 收集星星
    /// </summary>
    [Command]
    public void CmdCollectStar(int count)
    {
        starCount += count;
    }

    /// <summary>
    /// 设置 flipX (用于其他客户端显示)
    /// </summary>
    [Command]
    public void CmdSetFlipX(bool flip)
    {
        isFlipX = flip;
    }

    /// <summary>
    /// 开火 (替代原先 PlayerAnimator.OnFire → ObjectPool → Fireball)
    /// </summary>
    [Command]
    public void CmdFire(Vector3 firePosition, bool facingRight)
    {
        RpcOnFire(firePosition, facingRight);
    }

    /// <summary>
    /// 游戏结束/胜利状态上报
    /// </summary>
    [Command]
    public void CmdReportGameEnd(bool isWin)
    {
        if (isWin)
        {
            RpcOnGameWin(playerName);
        }
        else
        {
            RpcOnGameOver();
        }
    }

    // ==================== ClientRpcs (服务器 → 所有客户端) ====================

    /// <summary>
    /// 命中通知 (替代原先 PlayerOnHit 事件的 UDP 间接通知)
    /// includeOwner=false: 权威客户端已在本地Player.OnHit中处理，避免重复扣血
    /// </summary>
    [ClientRpc(includeOwner = false)]
    private void RpcOnHit(string targetName, bool dead, int currentHp)
    {
        EventDispatcher.PostEvent(MessageEvent.PlayerOnHit, this, targetName, dead);

        // 更新本地 HP UI
        if (targetName.Equals(playerName))
        {
            EventDispatcher.PostEvent(MessageEvent.UpdateNetPlayer, this, this);
        }
    }

    /// <summary>
    /// 受击视觉特效同步 (闪烁 / 死亡动画)
    /// 本地已通过 PlayerAnimator.OnHit 立即显示，此处只同步到其他客户端
    /// </summary>
    [Command]
    public void CmdSyncHitEffect(bool isLethal)
    {
        RpcShowHitEffect(isLethal);
    }

    [ClientRpc(includeOwner = false)]
    private void RpcShowHitEffect(bool isLethal)
    {
        IHitEffectHandler handler = GetComponentInChildren<IHitEffectHandler>();
        if (handler != null)
        {
            handler.OnHit(isLethal, () => { });
        }
    }

    /// <summary>
    /// 开火通知 (替代原先 Fireball 本地实例化)
    /// </summary>
    [ClientRpc(includeOwner = false)]
    private void RpcOnFire(Vector3 pos, bool facingRight)
    {
        GameObject fire = ObjectPool.instance.GetInPool("Fireball");
        if (fire == null)
        {
            fire = GameObject.Instantiate(ResourceManager.Instance.LoadAsset<GameObject>("Bullets_Fireball"));
            fire.name = "Fireball";
        }
        fire.transform.parent = null;
        fire.transform.position = pos;
        fire.GetComponent<IProjectile>()?.Initialized(facingRight);
    }

    /// <summary>
    /// 游戏胜利通知
    /// </summary>
    [ClientRpc]
    private void RpcOnGameWin(string winnerName)
    {
        EventDispatcher.PostEvent(MessageEvent.GameWin, this, winnerName);
    }

    /// <summary>
    /// 游戏结束通知
    /// </summary>
    [ClientRpc]
    private void RpcOnGameOver()
    {
        EventDispatcher.PostEvent(MessageEvent.GameOver, this, null);
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 服务器端 API: 设置玩家初始数据
    /// </summary>
    [Server]
    public void ServerInit(string name, int initialHp = 3)
    {
        playerName = name;
        hp = initialHp;
        starCount = 0;
        isDead = false;
        isFlipX = false;
    }
}
}
