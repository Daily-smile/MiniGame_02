using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
/// UI按钮控制器
/// </summary>
public class UIButtonController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Serializable]
    public class UIButtonEvent : UnityEvent<PointerEventData> { }

    [SerializeField] private string targetButtonName = "Fire1"; // 要模拟的按钮名称
    [SerializeField] private bool allowDragOutside = true;

    [FormerlySerializedAs("onPointDown")]
    [SerializeField]
    private UIButtonEvent _onPointDown = new UIButtonEvent();
    public UIButtonEvent onPointDown
    {
        get { return _onPointDown; }
        set { _onPointDown = value; }
    }

    [FormerlySerializedAs("onPointUp")]
    [SerializeField]
    private UIButtonEvent _onPointUp = new UIButtonEvent();
    public UIButtonEvent onPointUp
    {
        get { return _onPointUp; }
        set { _onPointUp = value; }
    }

    private void Start()
    {
        // 注册虚拟按钮
        VirtualInputSystem.Instance.RegisterButton(targetButtonName);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        VirtualInputSystem.Instance.SetButtonDown(targetButtonName);
        onPointDown?.Invoke(eventData);

        if (allowDragOutside)
        {
            // 捕获指针，以便在指针离开UI区域后仍能接收OnPointerUp事件
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        VirtualInputSystem.Instance.SetButtonUp(targetButtonName);
        onPointUp?.Invoke(eventData);

        if (allowDragOutside)
        {
            // 释放指针捕获
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // 处理UI元素被禁用时的情况
    private void OnDisable()
    {
        // 确保按钮状态被正确重置
        VirtualInputSystem.Instance.SetButtonUp(targetButtonName);
    }
}
}