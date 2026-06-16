using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 热更新下载进度面板。
/// UI 元素通过 YooAsset 加载预制体（GameAssets/Boot/UpdatePanel.prefab）获取，
/// 文字组件使用 TextMeshProUGUI（TMP）。
/// 监听 PatchManager 的 EventDispatcher 事件，展示版本检查、下载进度、错误状态。
/// </summary>
public class UpdatePanel : BasePanel
{
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _skipButton;

    private PatchManager _patchManager;

    private bool _isDownloading;
    private long _totalDownloadSize;
    private int _totalDownloadCount;

    /// <summary> 全屏透明点击区域，下载完成后捕获"任意点击进入游戏" </summary>
    private GameObject _clickOverlay;
    /// <summary> "任意点击进入游戏" 文字的 DOTween 动画引用 </summary>
    private Tween _completeTween;

    public override void Init()
    {
        panelType = UIPanelType.Update;
        // UI 元素由预制体提供，通过序列化字段引用
        // 按钮点击事件需要在代码中绑定（预制体中的 Button 不会自动关联）
        if (_retryButton != null) _retryButton.onClick.AddListener(OnRetryClicked);
        if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);
        if (_progressSlider != null)
        {
            _progressSlider.wholeNumbers = false;
            _progressSlider.minValue = 0f;
            _progressSlider.maxValue = 100f;
            _progressSlider.value = 0f;
        }

        // 创建全屏透明点击区域（下载完成后用于"任意点击进入游戏"）
        CreateClickOverlay();
    }

    public override void Dispose()
    {
        if (_retryButton != null) _retryButton.onClick.RemoveListener(OnRetryClicked);
        if (_skipButton != null) _skipButton.onClick.RemoveListener(OnSkipClicked);
        KillCompleteTween();
    }

    private void KillCompleteTween()
    {
        _completeTween?.Kill();
        _completeTween = null;
    }

    /// <summary>
    /// 创建全屏透明点击区域，用于下载完成后的"任意点击进入游戏"交互。
    /// </summary>
    private void CreateClickOverlay()
    {
        _clickOverlay = new GameObject("ClickOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        _clickOverlay.transform.SetParent(transform, false);
        _clickOverlay.transform.SetAsLastSibling(); // 放到最顶层

        var rt = _clickOverlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = _clickOverlay.GetComponent<Image>();
        img.color = new Color(0, 0, 0, 0); // 全透明
        img.raycastTarget = true;

        var btn = _clickOverlay.GetComponent<Button>();
        btn.onClick.AddListener(OnClickToEnter);

        _clickOverlay.SetActive(false); // 默认隐藏
    }

    #region Lifecycle

    public override void OnEnter(Action<BasePanel> callback = null)
    {
        base.OnEnter(null);
        SubscribeEvents();
        _patchManager = null;
        _isDownloading = false;
        SetTitle("正在检查更新...");
        SetStatus("");
        SetProgress(0, "0 B / 0 B");
        SetButtonsVisible(false, false);
        SetProgressVisible(false);
        HideClickToEnter();
        callback?.Invoke(this);
    }

    public override void OnExist(Action<BasePanel> callback = null)
    {
        base.OnExist(null);
        HideClickToEnter();
        UnsubscribeEvents();
        callback?.Invoke(this);
    }

    #endregion

    #region Event Handlers

    private void SubscribeEvents()
    {
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchCheckStart, OnCheckStart, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchVersionGet, OnVersionGet, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchVerifyStart, OnVerifyStart, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchDownloadStart, OnDownloadStart, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchDownloadProgress, OnDownloadProgress, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchDownloadComplete, OnDownloadComplete, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchDownloadFailed, OnDownloadFailed, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchFinish, OnPatchFinish, null);
    }

    private void UnsubscribeEvents()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPatchCheckStart, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPatchVersionGet, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPatchVerifyStart, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPatchDownloadStart, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPatchDownloadProgress, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPatchDownloadComplete, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPatchDownloadFailed, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPatchFinish, null);
    }

    private bool OnCheckStart(params object[] args)
    {
        if (args.Length > 0 && args[0] is PatchManager mgr)
            _patchManager = mgr;
        SetTitle("正在检查更新...");
        SetStatus("正在连接服务器...");
        SetButtonsVisible(false, false);
        return false;
    }

    private bool OnVerifyStart(params object[] args)
    {
        SetTitle("校验资源完整性");
        SetStatus("正在对比本地与远端资源清单...");
        SetButtonsVisible(false, false);
        SetProgressVisible(false);
        HideClickToEnter();
        return false;
    }

    private bool OnVersionGet(params object[] args)
    {
        if (args.Length >= 2)
        {
            string remoteVer = args[0] as string ?? "";
            string localVer = args[1] as string ?? "";

            if (string.IsNullOrEmpty(remoteVer))
            {
                // 服务器不可达 → 显示跳过按钮
                SetTitle("连接服务器失败");
                SetStatus("无法连接到更新服务器，请检查网络后重试");
                SetButtonsVisible(false, true);
            }
            else if (remoteVer == localVer)
            {
                // 已是最新版本 → 显示点击进入提示（与下载完成后交互一致）
                SetTitle("资源已是最新");
                SetStatus("任意点击进入游戏");
                SetButtonsVisible(false, false);
                SetProgressVisible(true);

                // 用 DOTween 让滑动条动画滑到 100%
                if (_progressSlider != null)
                    _progressSlider.DOValue(100f, 0.3f).SetEase(Ease.OutQuad);
                if (_progressText != null)
                    _progressText.text = "100%";

                ShowClickToEnter();
            }
            else
            {
                // 有新版本 → 直接自动下载，不显示确认按钮
                int downloadCount = 0;
                long downloadSize = 0;
                if (args.Length >= 4)
                {
                    downloadCount = args[2] is int dc ? dc : 0;
                    downloadSize = args[3] is long ds ? ds : 0;
                }

                SetTitle("发现新版本");
                SetStatus($"当前版本: {localVer} → 最新版本: {remoteVer}\n正在准备下载...");
                SetButtonsVisible(false, false); // 不显示任何按钮，自动开始下载
            }
        }
        return false;
    }

    private bool OnDownloadStart(params object[] args)
    {
        _isDownloading = true;
        if (args.Length >= 2)
        {
            _totalDownloadCount = args[0] is int count ? count : 0;
            _totalDownloadSize = args[1] is long size ? size : 0;
        }
        SetTitle("正在下载更新...");
        SetStatus($"共 {_totalDownloadCount} 个文件，{FormatSize(_totalDownloadSize)}");
        // 显式归零进度条，避免遗留之前的状态
        SetProgress(0f, $"0 B / {FormatSize(_totalDownloadSize)}");
        SetProgressVisible(true);
        SetButtonsVisible(false, false); // 下载中不显示任何按钮
        return false;
    }

    private bool OnDownloadProgress(params object[] args)
    {
        if (!_isDownloading) return false;
        if (args.Length >= 4)
        {
            int currentCount = args[0] is int cc ? cc : 0;
            int totalCount = args[1] is int tc ? tc : 1;
            long currentSize = args[2] is long cs ? cs : 0;
            long totalSize = args[3] is long ts ? ts : 0;

            // 优先用字节算进度，字节为 0 时回退到文件数算进度
            float progress;
            if (totalSize > 0)
                progress = (float)currentSize / totalSize;
            else if (totalCount > 0)
                progress = (float)currentCount / totalCount;
            else
                progress = 0f;

            SetProgress(progress, $"{FormatSize(currentSize)} / {FormatSize(totalSize)}");
            SetStatus($"正在下载第 {currentCount}/{totalCount} 个文件...");
        }
        return false;
    }

    private bool OnDownloadComplete(params object[] args)
    {
        _isDownloading = false;
        SetTitle("刷新成功！");
        SetStatus("任意点击进入游戏");
        SetButtonsVisible(false, false);
        SetProgressVisible(true);

        // 用 DOTween 让滑动条动画滑到 100%，避免直接设值导致的视觉不刷新问题
        if (_progressSlider != null)
            _progressSlider.DOValue(100f, 0.3f).SetEase(Ease.OutQuad);
        if (_progressText != null)
            _progressText.text = "100%";

        ShowClickToEnter();
        return false;
    }

    private bool OnDownloadFailed(params object[] args)
    {
        _isDownloading = false;
        string errorMsg = args.Length > 0 ? (args[0] as string ?? "下载失败") : "下载失败";
        SetTitle("更新失败");
        SetStatus(errorMsg);
        SetProgressVisible(false);
        SetButtonsVisible(true, true); // 显示重试 + 跳过
        return false;
    }

    private bool OnPatchFinish(params object[] args)
    {
        // 流程结束时清理动画
        HideClickToEnter();
        return false;
    }

    #endregion

    #region Button Callbacks

    private void OnRetryClicked()
    {
        Debug.Log("[UpdatePanel] Retry requested.");
        HideClickToEnter();
        if (_patchManager != null)
            _patchManager.UserRequestedRetry = true;
        SetTitle("正在重新下载...");
        SetStatus("");
        SetButtonsVisible(false, false);
        SetProgressVisible(true);
    }

    private void OnSkipClicked()
    {
        Debug.Log("[UpdatePanel] User chose to skip update.");
        if (_patchManager != null)
        {
            _patchManager.UserAgreedUpdate = false;
            _patchManager.UserSkippedUpdate = true;
        }
        HideClickToEnter();
        SetTitle("已跳过更新");
        SetStatus("正在进入游戏...");
        SetButtonsVisible(false, false);
        SetProgressVisible(false);
    }

    #endregion

    #region Click-To-Enter (下载完成后"任意点击进入游戏")

    /// <summary>
    /// 下载完成后调用：显示全息点击区域 + 启动文字呼吸动画。
    /// </summary>
    private void ShowClickToEnter()
    {
        if (_clickOverlay != null)
            _clickOverlay.SetActive(true);

        KillCompleteTween();

        // 对状态文字做透明度呼吸动画：文字从透明慢慢浮现再消失，循环
        if (_statusText != null)
        {
            Color baseColor = _statusText.color;
            _completeTween = _statusText.DOColor(
                new Color(baseColor.r, baseColor.g, baseColor.b, 0.2f),
                1.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
    }

    /// <summary>
    /// 隐藏点击区域并停止动画。
    /// </summary>
    private void HideClickToEnter()
    {
        if (_clickOverlay != null)
            _clickOverlay.SetActive(false);
        KillCompleteTween();
    }

    /// <summary>
    /// 用户点击任意位置后，通知 PatchManager 进入游戏。
    /// </summary>
    private void OnClickToEnter()
    {
        Debug.Log("[UpdatePanel] User clicked to enter game.");
        if (_patchManager != null)
            _patchManager.UserReadyToEnter = true;
        HideClickToEnter();
        SetTitle("正在进入游戏...");
        SetStatus("");
        SetButtonsVisible(false, false);
    }

    #endregion

    #region UI Helpers

    private void SetTitle(string text)
    {
        if (_titleText != null) _titleText.text = text;
    }

    private void SetStatus(string text)
    {
        if (_statusText != null) _statusText.text = text;
    }

    private void SetProgress(float progress, string detailText)
    {
        // 滑动条范围为 0~100，progress 为 0~1，需放大
        if (_progressSlider != null) _progressSlider.value = Mathf.Clamp(progress * 100f, 0f, 100f);
        if (_progressText != null) _progressText.text = detailText;
    }

    private void SetProgressVisible(bool visible)
    {
        if (_progressSlider != null) _progressSlider.gameObject.SetActive(visible);
        if (_progressText != null) _progressText.gameObject.SetActive(visible);
    }

    private void SetButtonsVisible(bool retry, bool skip)
    {
        if (_retryButton != null) _retryButton.gameObject.SetActive(retry);
        if (_skipButton != null) _skipButton.gameObject.SetActive(skip);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
        return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
    }

    #endregion
}
