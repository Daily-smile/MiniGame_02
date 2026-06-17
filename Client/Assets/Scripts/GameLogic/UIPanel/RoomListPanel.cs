using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class RoomListPanel : BasePanel
{
    public enum RoomModel
    {
        RoomHall,
        MyRoom,
    }
    private Button back, find, create, quit, findRoomConfirmBtn, findRoomCancelBtn, createRoomConfirmBtn, createRoomCancelBtn;
    private Transform createRoomPanel, findRoomPanel, myRoomToggleRed;
    private Slider cr_roomnum_slider;
    private TMP_InputField fd_roomname, cr_roomname;
    private TextMeshProUGUI createRoomNumText, roomLabel;
    private Toggle roomHallToggle, myRoomToggle;
    private RoomHallUI roomHallUI;
    private MyRoomUI myRoomUI;
    public RoomModel roomModel {  get; private set; }

    public override void Init()
    {
        panelType = UIPanelType.RoomList;
        EventDispatcher.AddObserver(this, MessageEvent.OnReciveChatRoomMsgUpdate, OnReciveChatRoomMsgUpdate, null);
        Transform roomPanelRoot = transform.Find("RoomPanel");
        roomHallToggle = roomPanelRoot.Find("RoomHallTog").GetComponent<Toggle>();
        myRoomToggle = roomPanelRoot.Find("MyRoomTog").GetComponent<Toggle>();
        myRoomToggleRed = myRoomToggle.transform.Find("red");
        myRoomToggleRed.gameObject.SetActive(false);
        roomLabel = roomPanelRoot.Find("Label").GetComponent<TextMeshProUGUI>();
        Transform btnRoot = transform.Find("ButtonList");
        back = btnRoot.Find("ReturnRoot").GetComponent<Button>();
        find = btnRoot.Find("SearchRoom").GetComponent<Button>();
        create = btnRoot.Find("CreateRoom").GetComponent<Button>();
        quit = btnRoot.Find("QuitRoom").GetComponent<Button>();
        createRoomPanel = transform.Find("CreateRoomPanel");
        cr_roomnum_slider = createRoomPanel.Find("RoomNum").GetComponent<Slider>();
        createRoomNumText = cr_roomnum_slider.transform.Find("Num").GetComponent<TextMeshProUGUI>();
        cr_roomname = createRoomPanel.Find("RoomName").GetComponent<TMP_InputField>();
        findRoomPanel = transform.Find("FindRoomPanel");
        fd_roomname = findRoomPanel.Find("RoomName").GetComponent<TMP_InputField>();
        roomHallUI = roomPanelRoot.Find("RoomHallUI").GetComponent<RoomHallUI>();
        roomHallUI.Init(this);
        myRoomUI = roomPanelRoot.Find("MyRoomUI").GetComponent<MyRoomUI>();
        myRoomUI.Init(this);
        findRoomConfirmBtn = findRoomPanel.Find("Comfirm").GetComponent<Button>();
        findRoomCancelBtn = findRoomPanel.Find("Cancel").GetComponent<Button>();
        createRoomConfirmBtn = createRoomPanel.Find("Comfirm").GetComponent<Button>();
        createRoomCancelBtn = createRoomPanel.Find("Cancel").GetComponent<Button>();
    }

    public override void Dispose()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.OnReciveChatRoomMsgUpdate, null);
        back = null;
        find = null;
        create = null;
        quit = null;
        roomHallUI = null;
        myRoomUI = null;
        findRoomConfirmBtn = null;
        findRoomCancelBtn = null;
        createRoomConfirmBtn = null;
        createRoomCancelBtn = null;
        findRoomPanel = null;
    }

    public override void StartUI()
    {
        base.StartUI();
        back.onClick.AddListener(OnReturnRootClick);
        find.onClick.AddListener(OnFindRoomClick);
        create.onClick.AddListener(OnCreateRoomClick);
        quit.onClick.AddListener(OnQuitRoomClick);
        roomHallToggle.onValueChanged.AddListener(OnRoomHallToggleClick);
        myRoomToggle.onValueChanged.AddListener(OnMyRoomnToggleClick);
        findRoomConfirmBtn.onClick.AddListener(FindRoomConfirmPanelClick);
        findRoomCancelBtn.onClick.AddListener(FindRoomCancelPanelClick);
        createRoomConfirmBtn.onClick.AddListener(CreateRoomConfirmPanelClick);
        createRoomCancelBtn.onClick.AddListener(CreateRoomCancelPanelClick);
        cr_roomnum_slider.onValueChanged.AddListener(f => { createRoomNumText.text = cr_roomnum_slider.value.ToString(); });
    }

    public override void OnExist(Action<BasePanel> callback = null)
    {
        createRoomPanel.gameObject.SetActive(false);
        findRoomPanel.gameObject.SetActive(false);
        cr_roomnum_slider.value = cr_roomnum_slider.minValue;
        createRoomNumText.text = cr_roomnum_slider.value.ToString();
        fd_roomname.text = "";
        cr_roomname.text = "";
        base.OnExist();
        callback?.Invoke(this);
    }

    public void OnEnterPanel()
    {
        if (roomHallToggle.isOn)
        {
            SwitchToggleUI(false, myRoomToggle);
            OnRoomHallToggleClick(true);
            CustomNetworkManager.singleton.SendFindRoom();
        }
        else
        {
            roomHallToggle.isOn = true;
        }
    }

    public void SwitchRoomModel(RoomModel model)
    {
        if (roomModel == model) return;
        if (roomModel == RoomModel.RoomHall)
            myRoomToggle.isOn = true;
        else
            roomHallToggle.isOn = true;
    }

    private void SwitchToggleUI(bool isOn, Toggle toggle)
    {
        toggle.transform.Find("Background").GetComponent<Image>().color = isOn ? Color.black : Color.white;
        toggle.transform.Find("Checkmark").GetComponent<Image>().color = isOn ? Color.white : Color.black;
        toggle.transform.Find("Label").GetComponent<TextMeshProUGUI>().color = isOn ? Color.black : Color.white;
        if (isOn)
        {
            roomLabel.text = toggle.transform.Find("Label").GetComponent<TextMeshProUGUI>().text;
        }
    }
    private void OnRoomHallToggleClick(bool isOn)
    {
        SwitchToggleUI(isOn, roomHallToggle);
        if (isOn)
        {
            roomHallUI.gameObject.SetActive(true);
            myRoomUI.gameObject.SetActive(false);
            find.gameObject.SetActive(true);
            quit.gameObject.SetActive(false);
            if (roomModel != RoomModel.RoomHall)
            {
                roomModel = RoomModel.RoomHall;
                roomHallUI.SwitchUI();
            }
        }
    }
    private void OnMyRoomnToggleClick(bool isOn)
    {
        SwitchToggleUI(isOn, myRoomToggle);
        if (isOn) 
        {
            myRoomToggleRed.gameObject.SetActive(false);
            roomHallUI.gameObject.SetActive(false);
            myRoomUI.gameObject.SetActive(true);
            find.gameObject.SetActive(false);
            quit.gameObject.SetActive(true);
            if (roomModel != RoomModel.MyRoom)
            {
                roomModel = RoomModel.MyRoom;
                myRoomUI.SwitchUI();
            }
        }
    }
    private void FindRoomConfirmPanelClick()
    {
        if (string.IsNullOrEmpty(fd_roomname.text))
        {
            EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, MessageEvent.allMessageStr[MessageEventType.FindRoomNameEmpty]);
            return;
        }
        CustomNetworkManager.singleton.SendFindRoom(fd_roomname.text);
        findRoomPanel.gameObject.SetActive(false);
    }
    private void FindRoomCancelPanelClick()
    {
        findRoomPanel.gameObject.SetActive(false);
    }
    private void CreateRoomConfirmPanelClick()
    {
        if (string.IsNullOrEmpty(cr_roomname.text))
        {
            EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, MessageEvent.allMessageStr[MessageEventType.CreateRoomNameEmpty]);
            return;
        }
        if (GameManager.Instance.IsInRoom())
        {
            CommonUtility.ShowTipPanel(MessageEvent.allMessageStr[MessageEventType.InRoomToCreateRoom], () =>
            {
                CustomNetworkManager.singleton.SendQuitRoom(GameManager.Instance.GetRoomID);
                CustomNetworkManager.singleton.SendCreateRoom(cr_roomname.text, (int)cr_roomnum_slider.value);
                createRoomPanel.gameObject.SetActive(false);
            });
        }
        else
        {
            CustomNetworkManager.singleton.SendCreateRoom(cr_roomname.text, (int)cr_roomnum_slider.value);
            createRoomPanel.gameObject.SetActive(false);
        }
    }
    private void CreateRoomCancelPanelClick()
    {
        createRoomPanel.gameObject.SetActive(false);
    }
    private void OnReturnRootClick()
    {
        if (GameManager.Instance.IsInRoom())
        {
            Action com = () => {
                CustomNetworkManager.singleton.SendQuitRoom(GameManager.Instance.GetRoomID);
            };
            EventDispatcher.PostEvent(MessageEvent.OpenTipPanel, this, MessageEvent.allMessageStr[MessageEventType.IsQuitRoomPanelOnRoom], com);
        }
        else
        {
            UIManager.instance.OpenPanel(UIPanelType.Game);
        }
    }
    private void OnFindRoomClick()
    {
        findRoomPanel.gameObject.SetActive(true);
        createRoomPanel.gameObject.SetActive(false);
    }
    private void OnCreateRoomClick()
    {
        findRoomPanel.gameObject.SetActive(false);
        createRoomPanel.gameObject.SetActive(true);
    }
    private void OnQuitRoomClick()
    {
        if (GameManager.Instance.IsInRoom())
        {
            CommonUtility.ShowTipPanel(MessageEvent.allMessageStr[MessageEventType.IsQuitRoom], () => {
                CustomNetworkManager.singleton.SendQuitRoom(GameManager.Instance.GetRoomID);
            });
        }
        else
        {
            CommonUtility.ShowUIMessage(MessageEventType.NoJoinRoomQuitRoom);
        }
    }

    private void HealthSwitchToogle(bool switchMyToggle)
    {
        roomHallToggle.onValueChanged.RemoveListener(OnRoomHallToggleClick);
        myRoomToggle.onValueChanged.RemoveListener(OnMyRoomnToggleClick);
        roomHallToggle.isOn = !switchMyToggle;
        SwitchToggleUI(!switchMyToggle, roomHallToggle);
        myRoomToggle.isOn = switchMyToggle;
        SwitchToggleUI(switchMyToggle, myRoomToggle);
        roomModel = switchMyToggle ? RoomModel.MyRoom : RoomModel.RoomHall;
        roomHallToggle.onValueChanged.AddListener(OnRoomHallToggleClick);
        myRoomToggle.onValueChanged.AddListener(OnMyRoomnToggleClick);
    }

    private bool OnReciveChatRoomMsgUpdate(params object[] args)
    {
        //Debug.Log("��������Ϣ����");
        ChatRoomMessage msg = (ChatRoomMessage)args[0];
        if (roomModel == RoomModel.RoomHall)
        {
            myRoomToggleRed.gameObject.SetActive(true);
        }
        myRoomUI.OnUpdateOneChatRoomMessage(msg.senderName, msg.content);
        return false;
    }
}
}
