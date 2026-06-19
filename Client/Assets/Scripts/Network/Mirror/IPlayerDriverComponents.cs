using System;
using UnityEngine;

namespace LF.Network
{
/// <summary>
/// 本地玩家驱动器接口。
/// 由 MirrorPlayer 通过 GetComponent 查找并调用。
/// 具体实现在 GameLogic 程序集中（热更层），接口定义在 Network（AOT 层）。
/// </summary>
public interface ILocalPlayerDriver
{
    void Initialize();
    void Cleanup();
}

/// <summary>
/// 远程玩家视图接口。
/// 由 MirrorPlayer 通过 GetComponent 查找并调用。
/// </summary>
public interface IRemotePlayerView
{
    void Initialize();
}

/// <summary>
/// 受击效果处理器接口。
/// 由 MirrorPlayer 在 RpcShowHitEffect 中调用。
/// 具体实现在 GameLogic 程序集中（热更层）。
/// </summary>
public interface IHitEffectHandler
{
    void OnHit(bool isLethal, System.Action onComplete);
}

/// <summary>
/// 投射物接口。
/// 由 MirrorPlayer 在 RpcOnFire 中调用，具体实现在 GameLogic 程序集中（热更层）。
/// </summary>
public interface IProjectile
{
    void Initialized(bool facingRight);
}

/// <summary>
/// MirrorPlayer 组件初始化钩子。
/// 热更程序集（GameLogic）在启动时注册回调，为 MirrorPlayer 动态添加 PlayerController、
/// PlayerAnimator 等热更脚本。避免预制体直接引用热更 MonoBehaviour 导致的序列化不匹配。
/// </summary>
public static class MirrorPlayerComponentSetup
{
    /// <summary>
    /// 当 MirrorPlayer 的 OnStartClient 触发时调用。
    /// 参数: MirrorPlayer 所在的 GameObject。
    /// 由 GameLogicEntry 在初始化时注册。
    /// </summary>
    public static Action<GameObject> OnMirrorPlayerCreated;

    /// <summary>
    /// 当 MirrorPlayer 获取本地权限时调用（仅本地玩家）。
    /// 参数: MirrorPlayer 所在的 GameObject。
    /// 由 GameLogicEntry 在初始化时注册。
    /// </summary>
    public static Action<GameObject> OnMirrorPlayerAuthority;
}
}
