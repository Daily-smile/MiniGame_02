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
}
