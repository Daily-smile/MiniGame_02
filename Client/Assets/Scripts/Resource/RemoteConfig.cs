using UnityEngine;

/// <summary>
/// 资源热更新远端配置（ScriptableObject）。
/// URL 类配置打进内置包（Boot 组）保证可达，策略类字段可在后续热更中覆盖。
/// 加载地址：RemoteConfig（对应 Assets/GameAssets/Boot/RemoteConfig.asset）
/// </summary>
[CreateAssetMenu(fileName = "RemoteConfig", menuName = "Game/Remote Config")]
public class RemoteConfig : ScriptableObject
{
    [Header("Server")]
    [Tooltip("远端资源服务器根地址")]
    public string UpdateServerURL = "http://127.0.0.1/";

    [Tooltip("版本文件服务器地址，留空则使用 UpdateServerURL")]
    public string VersionServerURL = "";

    [Header("Network")]
    [Tooltip("连接超时秒数")]
    public int ConnectTimeout = 10;

    [Tooltip("下载超时秒数")]
    public int DownloadTimeout = 60;

    [Tooltip("失败重试次数")]
    public int MaxRetryCount = 3;

    [Header("Strategy")]
    [Tooltip("更新策略")]
    public EUpdateStrategy UpdateStrategy = EUpdateStrategy.Force;

    [Tooltip("渠道标识（release / beta / debug）")]
    public string Channel = "release";

    /// <summary>
    /// 获取实际版本服务器地址，如果未单独配置则返回 UpdateServerURL
    /// </summary>
    public string GetVersionServerURL()
    {
        if (!string.IsNullOrEmpty(VersionServerURL))
            return VersionServerURL.Trim().TrimEnd('/');
        return UpdateServerURL.Trim().TrimEnd('/');
    }

    /// <summary>
    /// 获取实际资源服务器地址
    /// </summary>
    public string GetUpdateServerURL()
    {
        return UpdateServerURL.Trim().TrimEnd('/');
    }
}

/// <summary>
/// 资源更新策略
/// </summary>
public enum EUpdateStrategy
{
    /// <summary>静默更新：后台下载，下次启动生效</summary>
    Silent,
    /// <summary>强制更新：不更新不能进入游戏</summary>
    Force,
    /// <summary>可选更新：弹窗提示用户选择是否更新</summary>
    Optional
}
