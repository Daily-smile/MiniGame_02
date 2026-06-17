using DG.Tweening;
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
public class DoubleModelMatchPanel : BasePanel
{
    private int time, matchSchedule;
    private TextMeshProUGUI timer, playerName, enermyName, label;
    private Transform enermyIcon;
    private RectTransform leftBattle, rightBattle;
    private Button quitBtn, reMatchBtn;
    private Sequence matchSequence, matchEndSequence;

    public override void Init()
    {
        panelType = UIPanelType.DoubleModelMatch;
        timer = transform.Find("Time").GetComponent<TextMeshProUGUI>();
        playerName = transform.Find("Player/name").GetComponent<TextMeshProUGUI>();
        enermyName = transform.Find("Enermy/name").GetComponent<TextMeshProUGUI>();
        label = transform.Find("Label").GetComponent<TextMeshProUGUI>();
        label.text = MessageEvent.allMessageStr[MessageEventType.DoubleModelStartMatchTip];
        enermyIcon = transform.Find("Enermy/img");
        leftBattle = transform.Find("Battle_left").GetComponent<RectTransform>();
        rightBattle = transform.Find("Battle_right").GetComponent<RectTransform>();
        quitBtn = transform.Find("Quit").GetComponent<Button>();
        quitBtn.onClick.AddListener(OnQuitPanel);
        reMatchBtn = transform.Find("ReMatch").GetComponent<Button>();
        reMatchBtn.onClick.AddListener(OnReMatch);

    }

    public override void Dispose()
    {
        KillAnimSequence();
    }

    public override void OnPause(Action<BasePanel> callback = null)
    {
        base.OnPause();
        TimerMgr.Instance.Unschedule(matchSchedule);
        OnRestoreMatchAnim();
        KillAnimSequence();
        callback?.Invoke(this);
    }

    public override void OnExist(Action<BasePanel> callback = null)
    {
        base.OnExist();
        TimerMgr.Instance.Unschedule(matchSchedule);
        OnRestoreMatchAnim();
        KillAnimSequence();
        callback?.Invoke(this);
    }

    private void Initialize()
    {
        base.Show();
        time = 0;
        timer.text = "0";
        label.text = MessageEvent.allMessageStr[MessageEventType.DoubleModelStartMatchTip];
        playerName.text = GameManager.Instance.userName;
        enermyName.gameObject.SetActive(false);
        enermyIcon.gameObject.SetActive(true);
        reMatchBtn.gameObject.SetActive(false);
        matchSchedule = TimerMgr.Instance.Schedule(MatchUpdate, -1, 1, 1);
        OnRestoreMatchAnim();

        // ÿ��Showʱ���´�������
        CreateMatchSequence();
        matchEndSequence.Pause();
        matchSequence.Restart();
    }

    public override void Show()
    {
        Initialize();
        EventDispatcher.AddObserver(this, MessageEvent.StartGame, ServerMatchBack, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnQuitMatch, ServerQuitMatchBack, null);
    }

    public override void Hide()
    {
        base.Hide();
        EventDispatcher.RemoveObserver(this, MessageEvent.StartGame, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnQuitMatch, null);

        // ����ʱ��ͣ����
        PauseAnimSequence();
    }

    private void CreateMatchSequence()
    {
        // ȷ�������б�����
        if (matchSequence != null && matchSequence.IsActive())
        {
            matchSequence.Kill();
        }
        matchSequence = DOTween.Sequence();
        matchSequence.Append(leftBattle.DOBlendableLocalMoveBy(new Vector3(-500, 0, 0), 2f, true));
        matchSequence.Join(rightBattle.DOBlendableLocalMoveBy(new Vector3(500, 0, 0), 2f, true));
        // �������в��Զ�����
        matchSequence.SetAutoKill(false);

        if (matchEndSequence != null && matchEndSequence.IsActive())
        {
            matchEndSequence.Kill();
        }
        matchEndSequence = DOTween.Sequence();
        matchEndSequence.Append(leftBattle.DOBlendableLocalMoveBy(new Vector3(500, 0, 0), 2f, true));
        matchEndSequence.Join(rightBattle.DOBlendableLocalMoveBy(new Vector3(-500, 0, 0), 2f, true));
        // �������в��Զ�����
        matchEndSequence.SetAutoKill(false);
    }

    private void KillAnimSequence()
    {
        if (matchSequence != null && matchSequence.IsActive())
        {
            matchSequence.Kill();
        }
        if (matchEndSequence != null && matchEndSequence.IsActive())
        {
            matchEndSequence.Kill();
        }
    }

    private void PauseAnimSequence()
    {
        if (matchSequence != null && matchSequence.IsActive())
        {
            matchSequence.Pause();
        }
        if (matchEndSequence != null && matchEndSequence.IsActive())
        {
            matchEndSequence.Pause();
        }
    }

    private void OnRestoreMatchAnim()
    {
        leftBattle.anchoredPosition = new Vector2(-50, -200);
        rightBattle.anchoredPosition = new Vector2(50, -200);
    }

    private void OnQuitPanel()
    {
        CommonUtility.ShowTipPanel(MessageEventType.DoubleModelIsQuitMatchTip, () => {
            CustomNetworkManager.singleton.SendQuitMatch();
        });
    }
    private void OnReMatch()
    {
        Initialize();
        CustomNetworkManager.singleton.SendStartGame(1, 0, null);
    }

    private void MatchUpdate(object obj)
    {
        time++;
        timer.text = time.ToString();
        if (time > 20)
        {
            //UIManager.instance.OpenPanel(UIPanelType.Game);
            TimerMgr.Instance.Unschedule(matchSchedule);
            label.text = MessageEvent.allMessageStr[MessageEventType.DoubleModelMatchFailTip];
        }
    }

    private bool ServerMatchBack(params object[] args)
    {
        StartGameResponse response = (StartGameResponse)args[0];
        if (!response.success)
        {
            CommonUtility.ShowUIMessage(MessageEventType.DoubleModelMatchStart);
            return false;
        }
        bool isSuccessed = false;
        if (response.success && !response.opponentName.Equals(GameManager.Instance.userName))
        {
            TimerMgr.Instance.Unschedule(matchSchedule);
            enermyName.gameObject.SetActive(true);
            enermyIcon.gameObject.SetActive(false);
            enermyName.text = response.opponentName;
            timer.text = "";
            label.text = MessageEvent.allMessageStr[MessageEventType.DoubleModelMatchOKTip];
            isSuccessed = true;
        }
        else
        {
            TimerMgr.Instance.Unschedule(matchSchedule);
            timer.text = "";
            label.text = MessageEvent.allMessageStr[MessageEventType.DoubleModelMatchFailTip];
        }

        if (matchSequence.IsPlaying())
        {
            matchSequence.OnComplete(() => {
                OnMatchEndSequenceAnimPlay(isSuccessed, response.opponentName);
            });
        }
        else
        {
            OnMatchEndSequenceAnimPlay(isSuccessed, response.opponentName);
        }
        return false;
    }

    private void OnMatchEndSequenceAnimPlay(bool isSuccessed, string name)
    {
        matchEndSequence.OnComplete(() => {
            if (isSuccessed)
            {
                reMatchBtn.gameObject.SetActive(false);
                Hide();
                EventDispatcher.PostEvent(MessageEvent.EnterGame, this, GameManager.GameModel.Double, name);
            }
            else
            {
                reMatchBtn.gameObject.SetActive(true);
            }
        }).Restart();
    }

    private bool ServerQuitMatchBack(params object[] args)
    {
        CommonUtility.ShowUIMessage(MessageEventType.DoubleModelQuitMatchTip);
        UIManager.instance.OpenPanel(UIPanelType.Game);
        return false;
    }
}
}