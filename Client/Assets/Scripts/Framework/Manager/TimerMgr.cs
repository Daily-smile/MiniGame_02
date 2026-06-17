using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LF.Framework
{
class TimerNode
{
    public TimerMgr.TimerHandle callback;
    public float duration;//��ʱ��������ʱ����
    public float delay;//��һ�δ���Ҫ�������ʱ��
    public int repeat;//�����Ĵ���
    public float passedTime;//���Timer��ȥ��ʱ��
    public object[] param;//�û�Ҫ���Ĳ���
    public bool isRemoved;//�Ƿ��Ѿ�ɾ��
    public int timerId;//��ʶ���timer��ΨһID��
}

public class TimerMgr : UnitySingleton<TimerMgr>
{
    public delegate void TimerHandle(object[] param);

    private int autoIncId = 1;

    private Dictionary<int, TimerNode> timers = null;
    private List<TimerNode> removeTimers = null;
    private List<TimerNode> newAddTimers = null;

    public override void Awake()
    {
        base.Awake();
        this.Init();
    }

    //��ʼ�������
    private void Init()
    {
        this.timers = new Dictionary<int, TimerNode>();
        this.autoIncId = 1;
        this.removeTimers = new List<TimerNode>();
        this.newAddTimers = new List<TimerNode>();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        //���¼ӽ����ļ��뵽���ǵı�����
        for (int i = 0; i < this.newAddTimers.Count; i++)
        {
            this.timers.Add(this.newAddTimers[i].timerId, this.newAddTimers[i]);
        }
        this.newAddTimers.Clear();
        //end

        foreach (TimerNode timer in this.timers.Values)
        {
            if (timer.isRemoved)
            {
                this.removeTimers.Add(timer);
                continue;
            }

            timer.passedTime += dt;
            if (timer.passedTime >= (timer.delay + timer.duration))
            {
                //��һ�δ���
                timer.callback(timer.param);
                timer.repeat--;
                timer.passedTime -= (timer.delay + timer.duration);
                timer.delay = 0;//����Ҫ
                if (timer.repeat == 0)//��������������ɾ�����timer
                {
                    timer.isRemoved = true;
                    this.removeTimers.Add(timer);
                }
                //end
            }
        }

        //�����Ժ�������Ҫɾ����Timer
        for (int i = 0; i < this.removeTimers.Count; i++)
        {
            this.timers.Remove(this.removeTimers[i].timerId);
        }
        this.removeTimers.Clear();
    }

    public int ScheduleOnce(TimerHandle func, float delay)
    {
        return this.Schedule(func, 1, 0, delay);
    }

    public int ScheduleOnce(TimerHandle func, float delay, params object[] param)
    {
        return this.Schedule(func, 1, 0, delay, param);
    }

    //[repeat < 0 or repeat == 0 ��ʾ�������޴���]
    public int Schedule(TimerHandle func, int repeat, float duration)
    {
        return this.Schedule(func, repeat, duration, 0);
    }
    public int Schedule(TimerHandle func, int repeat, float duration, float delay)
    {
        return this.Schedule(func, repeat, duration, delay, null);
    }
    public int Schedule(TimerHandle func, int repeat, float duration, params object[] param)
    {
        return this.Schedule(func, repeat, duration, 0, param);
    }
    public int Schedule(TimerHandle func, int repeat, float duration, float delay, params object[] param)
    {
        TimerNode timer = new TimerNode();
        timer.callback = func;
        timer.param = param;
        timer.repeat = repeat;
        timer.duration = duration;
        timer.delay = delay;
        timer.passedTime = 0;
        timer.isRemoved = false;
        timer.timerId = this.autoIncId;
        this.autoIncId++;

        this.newAddTimers.Add(timer);

        return timer.timerId;
    }

    public void Unschedule(int timerId)
    {
        if (!this.timers.ContainsKey(timerId))
        {
            return;
        }

        TimerNode timer = this.timers[timerId];
        timer.isRemoved = true;
    }

    public void Unschedule(TimerHandle func)
    {
        if (func == null) return;
        foreach (TimerNode timer in timers.Values)
        {
            if (timer.callback == func)
            {
                timer.isRemoved = true;
            }
        }
    }

    public bool IsExistSchedule(int timerID)
    {
        return this.timers.ContainsKey(timerID);
    }

    /* **********************************ʱ����������*************************************** */
    public int GetCurrentTimeYear()
    {
        DateTime current = DateTime.Now;
        return current.Year;
    }
    public int GetCurrentTimeMonth()
    {
        DateTime current = DateTime.Now;
        return current.Month;
    }
    public int GetCurrentTimeDay()
    {
        DateTime current = DateTime.Now;
        return current.Day;
    }
    public int GetCurrentTimeHour()
    {
        DateTime current = DateTime.Now;
        return current.Hour;
    }
    public int GetCurrentTimeMinite()
    {
        DateTime current = DateTime.Now;
        return current.Minute;
    }
    public int GetCurrentTimeSecond()
    {
        DateTime current = DateTime.Now;
        return current.Second;
    }
    /// <summary>
    /// ��ȡʱ����(�ܷ��Ӽ���)
    /// </summary>
    public double GetTotalMinutesInterval(int toHours, int toMinute)
    {
        string timeKey = string.Format("{0}-{1}-{2} {3}:{4}:{5}", GetCurrentTimeYear(), GetCurrentTimeMonth(), GetCurrentTimeDay(), toHours, toMinute, GetCurrentTimeSecond());
        DateTime startTime = DateTime.Parse(timeKey);
        DateTime endTime = DateTime.Now;

        TimeSpan ts = endTime.Subtract(startTime);
        return ts.TotalMinutes;
    }
}
}
