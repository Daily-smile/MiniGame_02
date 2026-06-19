using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LF.Framework
{
class TimerNode
{
    public TimerMgr.TimerHandle callback;
    public float duration;// 单次计时时长
    public float delay;// 第一次触发前延迟时间
    public int repeat;// 重复执行次数
    public float passedTime;// 已过去的时间
    public object[] param;// 回调参数列表
    public bool isRemoved;// 是否已标记删除
    public int timerId;// 定时器唯一ID
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

    //初始化管理器
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

        //将新添加的定时器加入字典
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
                //执行回调
                timer.callback(timer.param);
                timer.repeat--;
                timer.passedTime -= (timer.delay + timer.duration);
                timer.delay = 0;//后续不需要延迟
                if (timer.repeat == 0)//次数用完则删除此定时器
                {
                    timer.isRemoved = true;
                    this.removeTimers.Add(timer);
                }
                //end
            }
        }

        //更新完成后清理待删除定时器
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

    //[repeat < 0 或 repeat == 0 表示无限循环]
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

    /* **********************************时间工具函数*************************************** */
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
    /// 获取时间差（总分钟数）
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
