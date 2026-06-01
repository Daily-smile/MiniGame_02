using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TipPanel : BasePanel
{
    private TextMeshProUGUI tipContent;
    private Button comfirm, cancel;
    private Action comfirmCallback;
    private Action cancelCallback;
    public override void Init()
    {
        panelType = UIPanelType.Tip;
        Transform bgTran = transform.Find("BG");
        tipContent = bgTran.Find("Content").GetComponent<TextMeshProUGUI>();
        comfirm = bgTran.Find("Comfirm").GetComponent<Button>();
        cancel = bgTran.Find("Cancel").GetComponent<Button>();
        comfirmCallback = null;
        cancelCallback = null;
    }
    public override void StartUI()
    {
        base.StartUI();
        comfirm.onClick.AddListener(OnComfirmClick);
        cancel.onClick.AddListener(OnCancelClick);
    }
    public override void Dispose()
    {
        comfirmCallback = null;
        cancelCallback = null;
    }

    public override void OnEnter(Action<BasePanel> callback = null)
    {
        base.OnEnter();
        transform.SetAsLastSibling();
        callback?.Invoke(this);
    }

    public override void OnRecovery(Action<BasePanel> callback = null)
    {
        base.OnRecovery();
        transform.SetAsLastSibling();
        callback?.Invoke(this);
    }

    public override void OnExist(Action<BasePanel> callback = null)
    {
        base.OnExist();
        tipContent.text = "";
        comfirmCallback = null;
        cancelCallback = null;
        callback?.Invoke(this);
    }

    public override void OnPause(Action<BasePanel> callback = null)
    {
        base.OnPause();
        tipContent.text = "";
        comfirmCallback = OnComfirmClick;
        cancelCallback = OnCancelClick;
        callback?.Invoke(this);
    }

    public void Show(string str, Action comfirmCallBack, Action cancelCallBack)
    {
        tipContent.text = str;
        comfirmCallback = comfirmCallBack;
        cancelCallback = cancelCallBack;
        OnEnter();
    }

    private void OnComfirmClick()
    {
        comfirmCallback?.Invoke();
        OnExist();
    }

    private void OnCancelClick()
    {
        cancelCallback?.Invoke();
        OnExist();
    }
}
