using Mirror;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
/// 本地玩家驱动 —— 仅在 isLocalPlayerByUserName 时由 MirrorPlayer.OnStartAuthority() 激活。
/// 负责：启用 PlayerController/PlayerAnimator、同步 flipX、调用 Command。
/// 与 RemotePlayerView 互斥，永远不会同时启用。
/// </summary>
[RequireComponent(typeof(MirrorPlayer))]
public class LocalPlayerDriver : MonoBehaviour, ILocalPlayerDriver
{
    private MirrorPlayer _mp;
    private PlayerController _controller;
    private PlayerAnimator _animator;
    private NetworkTransformUnreliable _netTransform;
    private SpriteRenderer _sprite;
    private bool _initialized;

    private void Awake()
    {
        _mp = GetComponent<MirrorPlayer>();
        _controller = GetComponentInChildren<PlayerController>();
        _animator = GetComponentInChildren<PlayerAnimator>();
        _netTransform = GetComponent<NetworkTransformUnreliable>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        enabled = false; // 默认禁用，由 Initialize() 激活
    }

    /// <summary>
    /// 由 MirrorPlayer.OnStartAuthority() 调用。
    /// 仅在 hasAuthority 且 isLocalPlayerByUserName 时执行完整初始化。
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        if (_mp.playerName != GameManager.Instance.userName)
        {
            Debug.LogWarning($"[LocalPlayerDriver] playerName 不匹配，跳过初始化: {_mp.playerName}");
            return;
        }

        // 启用本地输入控制和动画
        if (_controller != null)
        {
            _controller.enabled = true;
            _controller.isLocalPlayer = true;
        }
        else
        {
            Debug.LogError($"[LocalPlayerDriver] PlayerController 未找到于 {gameObject.name}!");
        }

        if (_animator != null)
        {
            _animator.enabled = true;
            _animator.isLocalPlayer = true;
        }
        else
        {
            Debug.LogError($"[LocalPlayerDriver] PlayerAnimator 未找到于 {gameObject.name}!");
        }

        // NetworkTransform: 本地玩家只发送，不从服务器接收自己的位置
        if (_netTransform != null)
        {
            _netTransform.syncDirection = SyncDirection.ClientToServer;
            _netTransform.syncInterval = 1f / _mp.clientSendRate;
        }

        // 订阅 SyncVar 变更事件 (target=_mp 过滤只收到自己 MirrorPlayer 的事件)
        EventDispatcher.AddObserver(this, MessageEvent.MirrorHpChanged, OnHpChanged, _mp);
        EventDispatcher.AddObserver(this, MessageEvent.MirrorDeadChanged, OnDeadChanged, _mp);

        // 启动 flipX 同步协程
        StartCoroutine(SyncFlipXToServer());

        // 注册玩家到服务器
        _mp.CmdRegisterPlayer(_mp.playerName);

        _initialized = true;
        enabled = true;
        Debug.Log($"[LocalPlayerDriver] 初始化完成: {_mp.playerName}");
    }

    /// <summary>
    /// 由 MirrorPlayer.OnStopAuthority() / OnDestroy 调用。
    /// </summary>
    public void Cleanup()
    {
        _initialized = false;
        enabled = false;

        if (_controller != null)
            _controller.enabled = false;
        if (_animator != null)
            _animator.enabled = false;

        EventDispatcher.RemoveObserver(this, MessageEvent.MirrorHpChanged, _mp);
        EventDispatcher.RemoveObserver(this, MessageEvent.MirrorDeadChanged, _mp);

        StopAllCoroutines();
        Debug.Log($"[LocalPlayerDriver] 清理完成: {_mp.playerName}");
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    // ==================== FlipX 同步协程 (从 MirrorPlayer 移入) ====================

    private System.Collections.IEnumerator SyncFlipXToServer()
    {
        Debug.Log($"[LocalPlayerDriver] SyncFlipXToServer 启动: {_mp.playerName}, _sprite={(_sprite != null ? _sprite.gameObject.name : "NULL")}");

        while (enabled && _mp.authority)
        {
            if (_sprite != null && _sprite.flipX != _mp.isFlipX)
            {
                Debug.Log($"[LocalPlayerDriver] 发送 CmdSetFlipX({_sprite.flipX}) for {_mp.playerName}");
                _mp.CmdSetFlipX(_sprite.flipX);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    // ==================== SyncVar 变更事件 (本地玩家可选处理) ====================

    private bool OnHpChanged(params object[] args)
    {
        // 本地玩家的 HP 变化由 Player.cs → CmdTakeDamage → RpcOnHit 流程处理，
        // SyncVar 回写时也会触发此事件，此处暂不做额外处理。
        return false;
    }

    private bool OnDeadChanged(params object[] args)
    {
        // 本地玩家的死亡状态由现有事件流处理。
        return false;
    }
}
}
