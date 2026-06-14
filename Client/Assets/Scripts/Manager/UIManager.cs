using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    private GameObject _canvas;
    public GameObject canvas
    {
        get
        {
            if (_canvas == null)
            {
                _canvas = GameObject.Find("Canvas");
                if (_canvas != null)
                {
                    GameObject.DontDestroyOnLoad(canvas);
                }
            }
            return _canvas;
        }
    }
    private Dictionary<UIPanelType, BasePanel> panelDict;
    private Dictionary<UIPanelType, string> panelPath;
    private Stack<BasePanel> panelStack;
    public UIManager()
    {
        panelDict = new Dictionary<UIPanelType, BasePanel>();
        panelPath = new Dictionary<UIPanelType, string>();
        panelStack = new Stack<BasePanel>();
        InitPanel();
        EventDispatcher.AddObserver(this, MessageEvent.OpenTipPanel, OnOpenTipPanel, null);
    }

    private bool OnOpenTipPanel(params object[] args)
    {
        OpenTipPanel(null, args);
        return false;
    }
    private TipPanel OpenTipPanel(Action<BasePanel> callback = null, params object[] args)
    {
        if (args == null)
        {
            return null;
        }
        string str = (string)args[0];
        if (string.IsNullOrEmpty(str))
        {
            return null;
        }
        Action com1Action = args.Length > 1 ? args[1] as Action : null;
        Action com2Action = args.Length > 2 ? args[2] as Action : null;
        TipPanel tipPanel = SpawnPanel(UIPanelType.Tip) as TipPanel;
        tipPanel.Show(str, com1Action, com2Action);
        return tipPanel;
    }

    private BasePanel SpawnPanel(UIPanelType type)
    {
        if (!panelDict.ContainsKey(type) || panelDict[type] == null)
        {
            if (canvas == null)
            {
                Debug.LogWarning("[UIManager] Canvas not ready yet, skip panel spawn.");
                return null;
            }
            Transform setParent = canvas.transform.Find("UI");
            GameObject newObj = ResourceManager.Instance.LoadAsset<GameObject>(panelPath[type]);
            if (newObj == null)
            {
                Debug.LogWarning($"[UIManager] Failed to load panel asset: {panelPath[type]}");
                return null;
            }
            GameObject newPrefab = GameObject.Instantiate(newObj);
            newPrefab.name = newObj.name;
            newPrefab.transform.SetParent(setParent, false);
            BasePanel panel = newPrefab.GetComponent<BasePanel>();
            if (!panelDict.ContainsKey(type))
            {
                panelDict.Add(type, panel);
            }
            else
            {
                panelDict[type] = panel;
            }
        }
        return panelDict[type];
    }

    private void InitPanel()
    {
        panelPath.Add(UIPanelType.Message, "Panel_MessagePanel");
        panelPath.Add(UIPanelType.Start, "Panel_StartPanel");
        panelPath.Add(UIPanelType.Logon, "Panel_LogonPanel");
        panelPath.Add(UIPanelType.Login, "Panel_LoginPanel");
        panelPath.Add(UIPanelType.Game, "Panel_GamePanel");
        panelPath.Add(UIPanelType.Room, "Panel_RoomListPanel");
        panelPath.Add(UIPanelType.RoomList, "Panel_RoomListPanel");
        panelPath.Add(UIPanelType.Tip, "Panel_TipPanel");
        panelPath.Add(UIPanelType.Disconnect, "Panel_DisconnectPanel");
        panelPath.Add(UIPanelType.GameUI, "Panel_GameUIPanel");
        panelPath.Add(UIPanelType.GameLoad, "Panel_GameLoadPanel");
        panelPath.Add(UIPanelType.GameOver, "Panel_GameOverPanel");
        panelPath.Add(UIPanelType.PausePanel, "Panel_PausePanel");
        panelPath.Add(UIPanelType.GameWin, "Panel_GameWinPanel");
        panelPath.Add(UIPanelType.DoubleModelMatch, "Panel_DoubleModelMatchPanel");
        panelPath.Add(UIPanelType.Update, "Boot_UpdatePanel");
    }

    public void LoadCacheUIPanel(params UIPanelType[] uiList)
    {
        for (int i = 0; i < uiList.Length; i++)
        {
            BasePanel panel = SpawnPanel(uiList[i]);
            if (panel != null)
                panel.gameObject.SetActive(false);
        }
    }

    public bool IsOpenPanel(UIPanelType panelType)
    {
        if (panelDict.TryGetValue(panelType, out BasePanel panel))
        {
            return panel.openState;
        }
        return false;
    }

    public bool TryGetPanel(UIPanelType panelType, out BasePanel panel)
    {
        return panelDict.TryGetValue(panelType, out panel);
    }
    public bool TryGetPanel<T>(UIPanelType panelType, out T panel) where T : BasePanel
    {
        panelDict.TryGetValue(panelType, out BasePanel bsPanel);
        panel = bsPanel != null ? bsPanel as T : null;
        return panel != null;
    }

    public T OpenPanel<T>(UIPanelType panelType, Action<BasePanel> callback = null, params object[] args) where T : BasePanel
    {
        BasePanel ui = OpenPanel(panelType, callback, args);
        if (ui == null)
        {
            return default(T);
        }
        return ui as T;
    }
    public BasePanel OpenPanel(UIPanelType panelType, Action<BasePanel> callback = null, params object[] args)
    {
        if (panelType == UIPanelType.Tip)
        {
            return OpenTipPanel(callback, args);
        }
        UIPanelType topUI = UIPanelType.None;
        if (panelStack.Count > 0)
        {
            BasePanel topPanel = panelStack.Peek();
            topPanel.OnPause();
            topUI = topPanel.panelType;
        }
        if (!panelDict.TryGetValue(panelType, out BasePanel panel))
        {
            panel = SpawnPanel(panelType);
        }
        if (panel != null)
        {
            panel.OnEnter(callback);
            if (topUI != panelType)
                panelStack.Push(panel);
        }
        return panel;
    }

    public void ClosePanel(Action<BasePanel> callback = null)
    {
        if (panelStack.Count == 0) return;
        BasePanel topPanel = panelStack.Pop();
        topPanel.OnExist(callback);
        if (panelStack.Count >= 1)
        {
            BasePanel nextPanel = panelStack.Peek();
            if (!topPanel.Equals(nextPanel))
                nextPanel.OnRecovery();
        }
    }
}
