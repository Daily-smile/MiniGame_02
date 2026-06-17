using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LF.Framework
{
public static class CommonUtility
{
    private static readonly string eventSender = "CommonUtility";

    public static void ShowUIMessage(MessageEventType messageType)
    {
        EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, eventSender, MessageEvent.allMessageStr[messageType]);
    }

    public static void ShowTipPanel(string tipStr, Action confirmCallback, Action cancelCallback = null)
    {
        EventDispatcher.PostEvent(MessageEvent.OpenTipPanel, eventSender, tipStr, confirmCallback, cancelCallback);
    }

    public static void ShowTipPanel(MessageEventType message, Action confirmCallback, Action cancelCallback = null)
    {
        EventDispatcher.PostEvent(MessageEvent.OpenTipPanel, eventSender, MessageEvent.allMessageStr[message], confirmCallback, cancelCallback);
    }

    /// <summary>
    /// 安全获取组件: transform.Find + GetComponent, 失败时返回null并记录警告
    /// </summary>
    public static T SafeGetComponent<T>(this Transform parent, string path) where T : Component
    {
        if (parent == null)
        {
            Debug.LogWarning($"[SafeGetComponent] parent is null, path={path}");
            return null;
        }
        Transform child = parent.Find(path);
        if (child == null)
        {
            Debug.LogWarning($"[SafeGetComponent] {parent.name}/{path} not found");
            return null;
        }
        T comp = child.GetComponent<T>();
        if (comp == null)
        {
            Debug.LogWarning($"[SafeGetComponent] {typeof(T).Name} not found on {parent.name}/{path}");
        }
        return comp;
    }
}
}
