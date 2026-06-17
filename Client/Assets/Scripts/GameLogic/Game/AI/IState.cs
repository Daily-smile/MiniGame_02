using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public interface IState
{
    void OnEnter();
    void OnUpdate();
    void OnExist();
}
}
