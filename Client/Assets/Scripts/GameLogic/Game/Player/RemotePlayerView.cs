using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
/// 远程玩家视图 —— 仅在 !isLocalPlayerByUserName 时由 MirrorPlayer.OnStartClient() 激活。
/// 负责：禁用 PlayerController/PlayerAnimator、响应 SyncVar 变更事件 → 视觉表现。
/// 与 LocalPlayerDriver 互斥，永远不会同时启用。
/// </summary>
[RequireComponent(typeof(MirrorPlayer))]
public class RemotePlayerView : MonoBehaviour, IRemotePlayerView
{
    private MirrorPlayer _mp;
    private PlayerController _controller;
    private PlayerAnimator _animator;
    private SpriteRenderer _sprite;
    private bool _initialized;

    private void Awake()
    {
        _mp = GetComponent<MirrorPlayer>();
        _controller = GetComponentInChildren<PlayerController>();
        _animator = GetComponentInChildren<PlayerAnimator>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        enabled = false; // 默认禁用，由 Initialize() 激活
    }

    /// <summary>
    /// 由 MirrorPlayer.OnStartClient() 调用（仅当 !isLocalPlayerByUserName）。
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        if (_mp.playerName == GameManager.Instance.userName)
        {
            Debug.LogWarning($"[RemotePlayerView] 本地玩家不应初始化 RemotePlayerView: {_mp.playerName}");
            return;
        }

        // 确保远程玩家不响应本地输入，完全由 NetworkTransform/NetworkAnimator 驱动
        if (_controller != null)
        {
            _controller.enabled = false;
            _controller.isLocalPlayer = false;
        }
        else
        {
            Debug.LogError($"[RemotePlayerView] PlayerController 未找到于 {gameObject.name}!");
        }

        if (_animator != null)
        {
            _animator.enabled = false;
            _animator.isLocalPlayer = false;
        }
        else
        {
            Debug.LogError($"[RemotePlayerView] PlayerAnimator 未找到于 {gameObject.name}!");
        }

        // 应用初始 flipX (SyncVars 在 OnStartClient 之前已同步)
        if (_sprite != null)
        {
            _sprite.flipX = _mp.isFlipX;
        }

        // 订阅 SyncVar 变更事件 (target=_mp 过滤只收到自己 MirrorPlayer 的事件)
        EventDispatcher.AddObserver(this, MessageEvent.MirrorHpChanged, OnHpChanged, _mp);
        EventDispatcher.AddObserver(this, MessageEvent.MirrorDeadChanged, OnDeadChanged, _mp);
        EventDispatcher.AddObserver(this, MessageEvent.MirrorStarCountChanged, OnStarCountChanged, _mp);
        EventDispatcher.AddObserver(this, MessageEvent.MirrorFlipXChanged, OnFlipXChanged, _mp);

        _initialized = true;
        enabled = true;
        Debug.Log($"[RemotePlayerView] 初始化完成: {_mp.playerName}");
    }

    private void OnDestroy()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.MirrorHpChanged, _mp);
        EventDispatcher.RemoveObserver(this, MessageEvent.MirrorDeadChanged, _mp);
        EventDispatcher.RemoveObserver(this, MessageEvent.MirrorStarCountChanged, _mp);
        EventDispatcher.RemoveObserver(this, MessageEvent.MirrorFlipXChanged, _mp);
    }

    // ==================== SyncVar 变更事件处理 ====================

    private bool OnHpChanged(params object[] args)
    {
        // 远程玩家 HP UI 刷新
        EventDispatcher.PostEvent(MessageEvent.UpdateNetPlayer, this, _mp);
        return false;
    }

    private bool OnDeadChanged(params object[] args)
    {
        bool newDead = (bool)args[1];
        if (newDead)
        {
            EventDispatcher.PostEvent(MessageEvent.OnNetPlayerDead, this, _mp.playerName);
        }
        return false;
    }

    private bool OnStarCountChanged(params object[] args)
    {
        EventDispatcher.PostEvent(MessageEvent.InGameGetStar, this, _mp.playerName);
        return false;
    }

    private bool OnFlipXChanged(params object[] args)
    {
        bool newVal = (bool)args[1];
        Debug.Log($"[RemotePlayerView] OnFlipXChanged: playerName={_mp.playerName}, newVal={newVal}, _sprite指向={(_sprite != null ? _sprite.gameObject.name : "NULL")}");
        if (_sprite != null)
        {
            _sprite.flipX = newVal;
        }
        return false;
    }
}
}
