using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class GameUIPanel : BasePanel
{
    Image[] stars;
    Transform inputRoot, waitingForUI, weaponInputKey, propItemPrefab, PropRoot, freeUI, infinityUI;
    Text waitingStartTimerText, runingGameSurplusTimerText, HPText;
    TextMeshProUGUI waitingForTipText, scoreText, saveMaxScoreText;
    Button waitingForQuitBtn, MenuBtn;
    UIButtonController useBtn, jumpBtn, fireBtn;
    Coroutine waitingForTimer;
    Dictionary<PropType, GameObject> propItems = new Dictionary<PropType, GameObject>();
    bool isInit;
    HealthBar healthBar;
    Player playerData;
    PropType curSelectProp = PropType.None;
    public ContestantPlayer[] players;
    private SpriteAtlas _gameSceneAtlas;
    public override void Init()
    {
        if (isInit)
        {
            return;
        }
        isInit = true;
        panelType = UIPanelType.GameUI;
        freeUI = transform.Find("FreeModel");
        infinityUI = transform.Find("InfinityModel");
        stars = freeUI.Find("Star").GetComponentsInChildren<Image>();
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].color = Color.gray;
        }
        players = freeUI.Find("Info").GetComponentsInChildren<ContestantPlayer>();
        weaponInputKey = transform.Find("JoyStick/fire");
        weaponInputKey.gameObject.SetActive(false);
        waitingForUI = transform.Find("WaitingForStartPanel");
        waitingForUI.gameObject.SetActive(false);
        waitingForTipText = waitingForUI.Find("text").GetComponent<TextMeshProUGUI>();
        waitingForTipText.gameObject.SetActive(true);
        scoreText = infinityUI.Find("score").GetComponent<TextMeshProUGUI>();
        scoreText.text = "0";
        saveMaxScoreText = infinityUI.Find("saveMaxScore").GetComponent<TextMeshProUGUI>();

        PropRoot = transform.Find("Prop");
        propItemPrefab = PropRoot.Find("item");
        propItemPrefab.gameObject.SetActive(false);
        _gameSceneAtlas = ResourceManager.Instance.LoadAsset<SpriteAtlas>("Atlas_GameScene");
        healthBar = transform.Find("BossHealth").GetComponent<HealthBar>();
        healthBar.gameObject.SetActive(false);

        waitingForTipText.text = MessageEvent.allMessageStr[MessageEventType.WaitingForStartGame];
        waitingStartTimerText = waitingForUI.Find("timer").GetComponent<Text>();
        waitingStartTimerText.text = "";
        runingGameSurplusTimerText = freeUI.Find("Info/surplusTimer/time").GetComponent<Text>();
        runingGameSurplusTimerText.text = "***";
        HPText = transform.Find("HP/num").GetComponent<Text>();
        HPText.text = "0";
        waitingForQuitBtn = waitingForUI.Find("QuitBtn").GetComponent<Button>();
        waitingForQuitBtn.gameObject.SetActive(false);
        waitingForQuitBtn.onClick.AddListener(WaitingForStartGameOnQuiutClick);
        MenuBtn = transform.Find("MenuBtn").GetComponent<Button>();
        MenuBtn.onClick.AddListener(MenuBtnClick);
        inputRoot = transform.Find("JoyStick/input");
        useBtn = transform.Find("JoyStick/use").GetComponent<UIButtonController>();
        useBtn.onPointDown.AddListener(OnUseBtnClick);
        useBtn.gameObject.SetActive(false);
        jumpBtn = transform.Find("JoyStick/jump").GetComponent<UIButtonController>();
        jumpBtn.onPointDown.AddListener(OnJumpBtnClick);
        fireBtn = transform.Find("JoyStick/fire").GetComponent<UIButtonController>();
        fireBtn.onPointDown.AddListener(OnFireBtnClick);

        EventDispatcher.AddObserver(this, MessageEvent.StartGameRuning, OnWaitingForStartGame, null);
        EventDispatcher.AddObserver(this, MessageEvent.RefreshGameUITimer, RefreshGameUITimer, null);
        EventDispatcher.AddObserver(this, MessageEvent.RefreshInfinityScoreUI, RefreshScoreUI, null);
        EventDispatcher.AddObserver(this, MessageEvent.RefreshInfinitySaveMaxScoreUI, RefreshInfinitySaveMaxScoreUI, null);
    }
    public void Initialize(Player player)
    {
        playerData = player;
        playerData.onHPChanged -= SetHP;
        playerData.onHPChanged += SetHP;
        playerData.onStarChanged -= SetStarsColor;
        playerData.onStarChanged += SetStarsColor;
        playerData.onGetWeapon -= UnlockWeapon;
        playerData.onGetWeapon += UnlockWeapon;
        playerData.onPropChanged -= PropRefresh;
        playerData.onPropChanged += PropRefresh;
        Init();
        SetStarsColor(0);
        SetHP(playerData.HP);
    }
    public override void Dispose()
    {
        if (playerData != null)
        {
            playerData.onHPChanged -= SetHP;
            playerData.onStarChanged -= SetStarsColor;
            playerData.onGetWeapon -= UnlockWeapon;
            playerData.onPropChanged -= PropRefresh;
        }
        if (waitingForTimer != null)
        {
            StopCoroutine(waitingForTimer);
            waitingForTimer = null;
        }
        propItems.Clear();
        isInit = false;
        EventDispatcher.RemoveObserver(this, MessageEvent.StartGameRuning, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.RefreshGameUITimer, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.RefreshInfinityScoreUI, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.RefreshInfinitySaveMaxScoreUI, null);
    }

    public override void StartUI()
    {
        EventDispatcher.PostEvent(MessageEvent.OnRegistSelfPlayer, this, playerData);
        EventDispatcher.PostEvent(MessageEvent.OnGameUILoad, this, healthBar);
    }

    public override void Show()
    {
        base.Show();
        bool isInfinity = GameManager.Instance.gameModel == GameManager.GameModel.Infinity;
        infinityUI.gameObject.SetActive(isInfinity);
        freeUI.gameObject.SetActive(!isInfinity);
        RefreshInfinitySaveMaxScoreUI();

        if (GameInputSystem.instance.InputModel == InputModel.Default)
        {
            inputRoot.gameObject.SetActive(false);
            jumpBtn.gameObject.SetActive(false);
            fireBtn.gameObject.SetActive(false);
        }
        else
        {
            inputRoot.gameObject.SetActive(true);
            jumpBtn.gameObject.SetActive(true);
            fireBtn.gameObject.SetActive(true);
        }
    }

    private void SetStarsColor(int starNum)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            Color color = starNum > i ? Color.white : Color.gray;
            stars[i].color = color;
        }
    }

    private void SetHP(int hp)
    {
        HPText.text = hp.ToString();
    }

    private void UnlockWeapon(PlayerWeaponType weaponType)
    {
        weaponInputKey.gameObject.SetActive(weaponType == PlayerWeaponType.Fireball);
    }

    private void PropRefresh(PropType propType, int count)
    {
        if (propType == PropType.None)
        {
            foreach (PropType key in propItems.Keys)
            {
                propItems[key].transform.GetComponent<Toggle>().isOn = false;
                propItems[key].gameObject.SetActive(false);
                useBtn.gameObject.SetActive(false);
            }
            return;
        }
        if (!propItems.ContainsKey(propType))
        {
            propItems.Add(propType, GameObject.Instantiate(propItemPrefab.gameObject, PropRoot));
            Sprite sprite = _gameSceneAtlas != null ? _gameSceneAtlas.GetSprite($"{propType}") : null;
            if (sprite != null)
                propItems[propType].transform.Find("icon").GetComponent<Image>().sprite = sprite;
            propItems[propType].transform.GetComponent<Toggle>().onValueChanged.AddListener((isOn)=>OnPropSelect(isOn, propType));
        }
        else if (!propItems[propType].activeSelf)
        {
            propItems[propType].transform.GetComponent<Toggle>().isOn = false;
        }
        propItems[propType].transform.Find("num").GetComponent<Text>().text = count.ToString();
        propItems[propType].SetActive(count > 0);
        if (count <= 0)
        {
            useBtn.gameObject.SetActive(false);
        }
    }

    private void OnPropSelect(bool isOn, PropType propType)
    {
        if (isOn)
        {
            curSelectProp = propType;
        }
        else
        {
            curSelectProp = PropType.None;
        }
        useBtn.gameObject.SetActive(isOn);
    }

    private void OnUseBtnClick(PointerEventData e)
    {
        if (curSelectProp == PropType.None)
        {
            CommonUtility.ShowUIMessage(MessageEventType.NoSelectPropTip);
            return;
        }
        EventDispatcher.PostEvent(MessageEvent.PlayerUseProp, this, GameManager.Instance.userName, curSelectProp);
    }

    private void OnJumpBtnClick(PointerEventData e)
    {
        
    }

    private void OnFireBtnClick(PointerEventData e)
    {

    }

    private void MenuBtnClick()
    {
        UIManager.instance.OpenPanel(UIPanelType.PausePanel);
    }

    private void WaitingForStartGameOnQuiutClick()
    {
        if (waitingForTimer != null)
        {
            StopCoroutine(waitingForTimer);
            waitingForTimer = null;
        }
        UIManager.instance.ClosePanel();
        EventDispatcher.PostEvent(MessageEvent.QuitGame, this, null);
        UIManager.instance.OpenPanel(UIPanelType.GameLoad, (panel) =>
        {
            GameLoadPanel ui = panel as GameLoadPanel;
            ui.OnQuitGameScene();
        });
    }

    private bool RefreshScoreUI(params object[] args)
    {
        scoreText.text = GameManager.Instance.InfinityModelScore.ToString();
        return false;
    }
    private bool RefreshInfinitySaveMaxScoreUI(params object[] args)
    {
        saveMaxScoreText.text = $"��ʷ��߷�����{GameManager.Instance.InfinityModelSaveMaxScore}";
        return false;
    }

    private bool RefreshGameUITimer(params object[] args)
    {
        int timer = (int)args[0];
        if (timer < 0)
        {
            runingGameSurplusTimerText.text = "***";
            return false;
        }
        runingGameSurplusTimerText.text = timer.ToString();
        return false;
    }

    private bool OnWaitingForStartGame(params object[] args)
    {
        waitingForUI.gameObject.SetActive(true);
        waitingForTipText.gameObject.SetActive(true);
        waitingForQuitBtn.gameObject.SetActive(false);
        waitingForTipText.text = MessageEvent.allMessageStr[MessageEventType.StartGameRuningTimer];
        waitingForTimer = StartCoroutine(OnWaitingTimerStartGame());
        return false;
    }

    private IEnumerator OnWaitingTimerStartGame()
    {
        int count = 5;
        while (count > 0)
        {
            waitingStartTimerText.text = count.ToString();
            count--;
            yield return new WaitForSeconds(1);
        }
        waitingForTimer = null;
        waitingForUI.gameObject.SetActive(false);
        EventDispatcher.PostEvent(MessageEvent.WaitingForGameRuning, this, null);
    }

    public void WaitingForGameStart(bool isDisconnect)
    {
        runingGameSurplusTimerText.text = "***";
        if (waitingForTimer != null)
        {
            StopCoroutine(waitingForTimer);
            waitingForTimer = null;
        }
        waitingForUI.gameObject.SetActive(true);
        waitingForTipText.gameObject.SetActive(true);
        waitingForQuitBtn.gameObject.SetActive(false);
        if (!isDisconnect)
        {
            waitingForTipText.text = MessageEvent.allMessageStr[MessageEventType.StartGameRuningTimer];
            waitingForTimer = StartCoroutine(OnWaitingTimerStartGame());
        }
        else
        {
            waitingForTipText.text = MessageEvent.allMessageStr[MessageEventType.WaitingForStartGame];
            waitingStartTimerText.text = "";
        }
    }
}
}
