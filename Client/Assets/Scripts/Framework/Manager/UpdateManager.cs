using System;
using System.Collections.Generic;
using UnityEngine;

namespace LF.Framework
{
/// <summary>
/// ������¹�����
/// </summary>
public class UpdateManager : MonoBehaviour
{
    private static UpdateManager _instance;
    public static UpdateManager Instance => _instance;

    // ���岻ͬ���ȼ��ĸ����б�
    private List<Action> _earlyUpdateCallbacks = new List<Action>();
    private List<Action> _normalUpdateCallbacks = new List<Action>();
    private List<Action> _lateUpdateCallbacks = new List<Action>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // 1. ����ִ�У������ȼ�����
        ExecuteCallbacks(_earlyUpdateCallbacks);

        // 2. Ȼ��ִ�У���ͨ����
        ExecuteCallbacks(_normalUpdateCallbacks);

        // 3. ���ִ�У������ȼ�����
        ExecuteCallbacks(_lateUpdateCallbacks);
    }

    private void ExecuteCallbacks(List<Action> callbacks)
    {
        if (callbacks.Count == 0) return;
        var snapshot = new List<Action>(callbacks);
        for (int i = 0; i < snapshot.Count; i++)
        {
            snapshot[i]?.Invoke();
        }
    }

    // ע�᲻ͬ���ȼ��ĸ��·���
    public void RegisterEarlyUpdate(Action callback) => _earlyUpdateCallbacks.Add(callback);
    public void RegisterNormalUpdate(Action callback) => _normalUpdateCallbacks.Add(callback);
    public void RegisterLateUpdate(Action callback) => _lateUpdateCallbacks.Add(callback);

    // ȡ��ע��
    public void Unregister(Action callback)
    {
        _earlyUpdateCallbacks.Remove(callback);
        _normalUpdateCallbacks.Remove(callback);
        _lateUpdateCallbacks.Remove(callback);
    }
}
}