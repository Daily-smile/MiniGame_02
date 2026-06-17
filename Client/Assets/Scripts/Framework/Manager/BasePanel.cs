using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LF.Framework
{
public abstract class BasePanel : MonoBehaviour
{
    public bool openState { get; protected set; }
    private Action onInit;
    private Action onStartUI;
    private Action onShow;
    private Action onHide;
    private Action onDispose;
    public UIPanelType panelType {  get; protected set; }

    void Awake()
    {
        onInit = Init;
        onStartUI = StartUI;
        onShow = Show;
        onHide = Hide;
        onDispose = Dispose;
        onInit();
    }
    void Start()
    {
        onStartUI();
    }
    void OnEnable()
    {
        onShow();
    }
    void OnDisable()
    {
        onHide();
    }
    void OnDestroy()
    {
        onDispose();
        CleanupDelegates();
    }

    private void CleanupDelegates()
    {
        onInit = null;
        onStartUI = null;
        onShow = null;
        onHide = null;
        onDispose = null;
    }

    /// <summary>
    /// 面板关闭时清理事件订阅 (面板池复用场景下OnDestroy不会被调用)
    /// 子类重写此方法取消订阅
    /// </summary>
    protected virtual void OnPanelCleanup() { }
    protected virtual void Update()
    {
        
    }

    public abstract void Init();
    public abstract void Dispose();
    public virtual void StartUI() { }
    public virtual void Show() { }
    public virtual void Hide() { }

    public virtual void OnEnter(Action<BasePanel> callback = null)
    {
        gameObject.SetActive(true);
        openState = true;
        callback?.Invoke(this);
    }

    public virtual void OnPause(Action<BasePanel> callback = null)
    {
        gameObject.SetActive(false);
        openState = false;
        callback?.Invoke(this);
    }

    public virtual void OnRecovery(Action<BasePanel> callback = null)
    {
        gameObject.SetActive(true);
        openState = true;
        callback?.Invoke(this);
    }

    public virtual void OnExist(Action<BasePanel> callback = null)
    {
        OnPanelCleanup();
        gameObject.SetActive(false);
        openState = false;
        callback?.Invoke(this);
    }
}
}
