using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

public class GameWinPanel : BasePanel
{
    Button returnBtn, againBtn;
    Image[] stars;
    Sprite blueStar, grayStar;

    public override void Init()
    {
        panelType = UIPanelType.GameWin;
        returnBtn = transform.Find("QuitBtn").GetComponent<Button>();
        returnBtn.onClick.AddListener(OnClickReturnBtn);
        againBtn = transform.Find("AgainBtn").GetComponent<Button>();
        againBtn.onClick.AddListener(OnClickAgainBtn);
        stars = transform.Find("Star").GetComponentsInChildren<Image>();
        SpriteAtlas atlas = ResourceManager.Instance.LoadAsset<SpriteAtlas>("Atlas_UI");
        blueStar = atlas.GetSprite("blue_star");
        grayStar = atlas.GetSprite("blue_star_outline");
    }
    public override void Dispose()
    {

    }

    public override void Show()
    {
        base.Show();
    }

    public override void OnEnter(Action<BasePanel> callback = null)
    {
        base.OnEnter(callback);
        int starNum = GameReferee.instance.GetPlayerStarCount(GameManager.Instance.userName);
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].sprite = starNum > i ? blueStar : grayStar;
        }
        callback?.Invoke(this);
    }

    public override void OnRecovery(Action<BasePanel> callback = null)
    {
        base.OnRecovery();
        callback?.Invoke(this);
    }

    private void OnClickReturnBtn()
    {
        UIManager.instance.ClosePanel();
        EventDispatcher.PostEvent(MessageEvent.QuitGame, this, null);
        // 通知服务器：玩家离开游戏，清理旧的玩家对象和匹配状态
        if (GameManager.Instance.IsLoginServer && CustomNetworkManager.singleton != null)
            CustomNetworkManager.singleton.SendReturnLogin();
        UIManager.instance.OpenPanel(UIPanelType.GameLoad, (panel) =>
        {
            GameLoadPanel ui = panel as GameLoadPanel;
            ui.OnQuitGameScene();
        });
    }

    private void OnClickAgainBtn()
    {
        EventDispatcher.PostEvent(MessageEvent.AgainGame, this, null);
        UIManager.instance.ClosePanel();
    }
}
