using System.Collections;

namespace LF.Framework
{
    /// <summary>
    /// 热更程序集入口接口。
    /// 由 LFFramework（AOT）定义，GameLogic（热更）实现。
    /// GameBootstrap 在预加载完成后通过此接口桥接调用热更代码。
    /// </summary>
    public interface IGameLogicEntry
    {
        /// <summary>
        /// 游戏初始化协程。
        /// 由 GameBootstrap 通过 StartCoroutine 启动。
        /// </summary>
        IEnumerator InitializeAsync(PatchManager patchMgr);
    }
}
