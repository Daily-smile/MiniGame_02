using System;
using System.Timers;

public class CountdownTimer<T>
{
    private Timer _timer;
    private int _elapsedSeconds;
    private readonly int _totalSeconds;
    private readonly int _dueSeconds;
    private T _callback_arg;
    private readonly Action<T> _callback;
    private readonly Action<T> _end;

    public CountdownTimer(int totalSeconds, int dueSeconds, Action<T> callback, Action<T> onEnd)
    {
        _totalSeconds = totalSeconds;
        _dueSeconds = dueSeconds;
        _callback = callback;
        _end = onEnd;
        _elapsedSeconds = 0;
    }

    public void Start(T obj)
    {
        // 创建计时器，间隔为_dueSeconds秒（_dueSeconds * 1000毫秒）
        _timer = new Timer(_dueSeconds * 1000);
        _timer.Elapsed += OnTimedEvent;
        _timer.AutoReset = true; // 设置为重复触发
        _timer.Enabled = true;
        _callback_arg = obj;

        //Console.WriteLine("计时器开始，将持续20秒");
    }

    private void OnTimedEvent(object source, ElapsedEventArgs e)
    {
        _elapsedSeconds++;

        // 执行回调函数，传递当前剩余的秒数
        _callback?.Invoke(_callback_arg);

        // 检查是否达到总时长
        if (_elapsedSeconds >= _totalSeconds)
        {
            _end?.Invoke(_callback_arg);
            Stop();
            //Console.WriteLine("计时器已销毁");
        }
    }

    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Enabled = false;
            _timer.Elapsed -= OnTimedEvent;
            _timer.Dispose();
            _timer = null;
        }
    }
}