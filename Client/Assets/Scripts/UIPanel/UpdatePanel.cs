using System;
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
    [SerializeField] private Button _confirmButton;

    private PatchManager _patchManager;

    private bool _isDownloading;
    private long _totalDownloadSize;
    private int _totalDownloadCount;

    public override void Init()
    {
        panelType = UIPanelType.Update;
        // UI 元素由预制体提供，通过序列化字段引用
        // 按钮点击事件需要在代码中绑定（预制体中的 Button 不会自动关联）
        if (_retryButton != null) _retryButton.onClick.AddListener(OnRetryClicked);
        if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);
        if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public override void Dispose()
    {
        if (_retryButton != null) _retryButton.onClick.RemoveListener(OnRetryClicked);
        if (_skipButton != null) _skipButton.onClick.RemoveListener(OnSkipClicked);
        if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
    }

    #region Lifecycle

    public override void OnEnter(Action<BasePanel> callback = null)
    {
        base.OnEnter(null);
        SubscribeEvents();
        _patchManager = null;
        SetTitle("正在检查更新...");
        SetStatus("");
        SetProgress(0, "0 B / 0 B");
        SetButtonsVisible(false, false, false);
        SetProgressVisible(false);
        callback?.Invoke(this);
    }

    public override void OnExist(Action<BasePanel> callback = null)
    {
        base.OnExist(null);
        UnsubscribeEvents();
        callback?.Invoke(this);
    }

    #endregion

    #region Event Handlers

    private void SubscribeEvents()
    {
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchCheckStart, OnCheckStart, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPatchVersionGet, OnVersionGet, null);
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
        SetButtonsVisible(false, false, false);
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
                SetTitle("连接服务器失败");
                SetStatus("无法连接到更新服务器，请检查网络后重试");
                SetButtonsVisible(false, true, false);
            }
            else if (remoteVer == localVer)
            {
                SetTitle("资源已是最新");
                SetStatus($"版本: {localVer}");
                SetProgress(1f, "100%");
            }
            else if (string.IsNullOrEmpty(localVer))
            {
                SetTitle("首次启动");
                SetStatus("正在准备资源...");
            }
            else
            {
                SetTitle("发现新版本");
                SetStatus($"当前版本: {localVer} → 最新版本: {remoteVer}");
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
        SetProgressVisible(true);
        SetButtonsVisible(false, _patchManager?.UpdateStrategy == EUpdateStrategy.Optional, false);
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
            long totalSize = args[3] is long ts ? ts : 1;
            float progress = totalSize > 0 ? (float)currentSize / totalSize : 0f;
            SetProgress(progress, $"{FormatSize(currentSize)} / {FormatSize(totalSize)}");
            SetStatus($"正在下载第 {currentCount}/{totalCount} 个文件...");
        }
        return false;
    }

    private bool OnDownloadComplete(params object[] args)
    {
        _isDownloading = false;
        SetTitle("更新完成！");
        SetStatus("正在进入游戏...");
        SetProgress(1f, "100%");
        SetProgressVisible(true);
        SetButtonsVisible(false, false, false);
        return false;
    }

    private bool OnDownloadFailed(params object[] args)
    {
        _isDownloading = false;
        string errorMsg = args.Length > 0 ? (args[0] as string ?? "下载失败") : "下载失败";
        int retryCount = args.Length > 1 ? (args[1] is int rc ? rc : 0) : 0;
        SetTitle("更新失败");
        SetStatus($"下载失败（已重试 {retryCount} 次）\n{errorMsg}");
        SetProgressVisible(false);
        SetButtonsVisible(true, _patchManager?.UpdateStrategy == EUpdateStrategy.Optional, false);
        return false;
    }

    private bool OnPatchFinish(params object[] args)
    {
        bool success = args.Length > 0 && args[0] is bool s && s;
        if (!success && !_isDownloading)
        {
            SetTitle("启动失败");
            SetStatus("资源初始化失败，请重试");
            SetButtonsVisible(true, false, false);
        }
        return false;
    }

    #endregion

    #region Button Callbacks

    private void OnRetryClicked()
    {
        Debug.Log("[UpdatePanel] Retry requested.");
        SetButtonsVisible(false, false, false);
    }

    private void OnSkipClicked()
    {
        Debug.Log("[UpdatePanel] User chose to skip update.");
        if (_patchManager != null)
        {
            _patchManager.UserAgreedUpdate = false;
            _patchManager.UserSkippedUpdate = true;
        }
        SetTitle("已跳过更新");
        SetStatus("使用本地资源，下次启动时可更新");
        SetButtonsVisible(false, false, false);
        SetProgressVisible(false);
    }

    private void OnConfirmClicked()
    {
        Debug.Log("[UpdatePanel] User confirmed update.");
        if (_patchManager != null)
            _patchManager.UserAgreedUpdate = true;
        SetTitle("正在准备下载...");
        SetStatus("");
        SetButtonsVisible(false, false, false);
        SetProgressVisible(true);
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
        if (_progressSlider != null) _progressSlider.value = Mathf.Clamp01(progress);
        if (_progressText != null) _progressText.text = detailText;
    }

    private void SetProgressVisible(bool visible)
    {
        if (_progressSlider != null) _progressSlider.gameObject.SetActive(visible);
        if (_progressText != null) _progressText.gameObject.SetActive(visible);
    }

    private void SetButtonsVisible(bool retry, bool skip, bool confirm)
    {
        if (_retryButton != null) _retryButton.gameObject.SetActive(retry);
        if (_skipButton != null) _skipButton.gameObject.SetActive(skip);
        if (_confirmButton != null) _confirmButton.gameObject.SetActive(confirm);
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
