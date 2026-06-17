using System.Collections.Generic;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
/// Boss状态机
/// </summary>
public class BossStateMachine
{
    private Dictionary<System.Type, IState> states;
    public IState CurrentState { get; private set; }

    public BossStateMachine()
    {
        states = new Dictionary<System.Type, IState>();
    }

    public void AddState(System.Type stateType, IState state)
    {
        states[stateType] = state;
    }

    public void ChangeState(System.Type newStateType)
    {
        if (CurrentState != null)
        {
            CurrentState.OnExist();
        }

        CurrentState = states[newStateType];
        CurrentState.OnEnter();
    }

    public void Update()
    {
        if (CurrentState != null)
        {
            CurrentState.OnUpdate();
        }
    }

    public System.Type GetCurrentStateType()
    {
        return CurrentState?.GetType();
    }
}
}