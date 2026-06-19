using UnityEngine;
using System.Collections.Generic;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
/// 虚拟按钮类
/// </summary>
[System.Serializable]
public class VirtualButton
{
    public string buttonName;
    public bool isPressed;
    public bool wasPressedThisFrame;
    public bool wasReleasedThisFrame;

    public void ResetFrameStates()
    {
        wasPressedThisFrame = false;
        wasReleasedThisFrame = false;
    }
}

/// <summary>
/// 虚拟轴类
/// </summary>
[System.Serializable]
public class VirtualAxis
{
    public string axisName;
    public float value;
    public float sensitivity = 3.0f; // 灵敏度
    public float gravity = 3.0f; // 回正速度
    public bool snap = true; // 是否快速回中

    public void Update(bool isPressed, bool isPositive)
    {
        if (isPressed)
        {
            if (snap && Mathf.Abs(value) < 0.01f)
            {
                value = isPositive ? 1.0f : -1.0f;
            }
            else
            {
                float target = isPositive ? 1.0f : -1.0f;
                value = Mathf.MoveTowards(value, target, sensitivity * Time.deltaTime);
            }
        }
        else
        {
            value = Mathf.MoveTowards(value, 0.0f, gravity * Time.deltaTime);
        }
    }
}

/// <summary>
/// 虚拟输入系统
/// </summary>
public class VirtualInputSystem : MonoBehaviour
{
    public static VirtualInputSystem Instance;

    [SerializeField] private List<VirtualButton> virtualButtons = new List<VirtualButton>();
    [SerializeField] private List<VirtualAxis> virtualAxes = new List<VirtualAxis>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        // 预注册常用轴
        RegisterAxis("Horizontal");
        RegisterAxis("Vertical");
    }

    private void Start()
    {
        UpdateManager.Instance.RegisterEarlyUpdate(OnEarlyUpdate);
        UpdateManager.Instance.RegisterLateUpdate(OnLateUpdate);
    }

    private void OnDestroy()
    {
        UpdateManager.Instance?.Unregister(OnEarlyUpdate);
        UpdateManager.Instance?.Unregister(OnLateUpdate);
    }

    public void OnEarlyUpdate()
    {
        // 将真实输入映射到虚拟输入（早更新阶段）
        HandleRealInputMapping();
    }
    private void OnLateUpdate()
    {
        // 每帧末尾重置当帧状态
        foreach (var button in virtualButtons)
        {
            button.ResetFrameStates();
        }
    }

    // 将真实输入映射到虚拟输入系统
    private void HandleRealInputMapping()
    {
        // 仅在非移动端使用键盘输入；移动端完全依赖UI虚拟输入
#if !UNITY_ANDROID && !UNITY_IOS
        SetAxis("Horizontal", Input.GetAxisRaw("Horizontal"));
        SetAxis("Vertical", Input.GetAxisRaw("Vertical"));
#endif
    }

    // 注册虚拟按钮
    public void RegisterButton(string buttonName)
    {
        if (virtualButtons.Find(b => b.buttonName == buttonName) == null)
        {
            virtualButtons.Add(new VirtualButton { buttonName = buttonName });
        }
    }

    // 注册虚拟轴
    public void RegisterAxis(string axisName)
    {
        if (virtualAxes.Find(a => a.axisName == axisName) == null)
        {
            virtualAxes.Add(new VirtualAxis { axisName = axisName });
        }
    }

    // 设置按钮按下状态
    public void SetButtonDown(string buttonName)
    {
        VirtualButton button = virtualButtons.Find(b => b.buttonName == buttonName);
        if (button != null)
        {
            if (!button.isPressed)
            {
                button.wasPressedThisFrame = true;
            }
            button.isPressed = true;
        }
    }

    // 设置按钮释放状态
    public void SetButtonUp(string buttonName)
    {
        VirtualButton button = virtualButtons.Find(b => b.buttonName == buttonName);
        if (button != null)
        {
            if (button.isPressed)
            {
                button.wasReleasedThisFrame = true;
            }
            button.isPressed = false;
        }
    }

    // 直接设置轴值（-1到1）
    public void SetAxis(string axisName, float value)
    {
        VirtualAxis axis = virtualAxes.Find(a => a.axisName == axisName);
        if (axis != null)
        {
            axis.value = Mathf.Clamp(value, -1.0f, 1.0f);
        }
    }

    // 通过按钮方向设置轴
    public void SetAxisButton(string axisName, bool isPositive, bool isPressed)
    {
        VirtualAxis axis = virtualAxes.Find(a => a.axisName == axisName);
        if (axis != null)
        {
            // 为每个方向创建唯一的按钮名，用于内部追踪
            string buttonName = $"{axisName}_{(isPositive ? "Positive" : "Negative")}";

            // 注册按钮（如果尚未注册）
            RegisterButton(buttonName);

            // 设置按钮状态
            if (isPressed)
            {
                SetButtonDown(buttonName);
            }
            else
            {
                SetButtonUp(buttonName);
            }

            // 更新轴值
            bool posPressed = GetButton($"{axisName}_Positive");
            bool negPressed = GetButton($"{axisName}_Negative");

            if (posPressed && negPressed)
            {
                // 如果两个方向同时按下，使用默认行为
                // 默认行为是归零
                axis.value = 0;
            }
            else if (posPressed)
            {
                axis.Update(true, true);
            }
            else if (negPressed)
            {
                axis.Update(true, false);
            }
            else
            {
                axis.Update(false, false);
            }
        }
    }

    // 模拟 Input.GetButtonDown
    public bool GetButtonDown(string buttonName)
    {
        VirtualButton button = virtualButtons.Find(b => b.buttonName == buttonName);
        return button != null && button.wasPressedThisFrame;
    }

    // 模拟 Input.GetButton
    public bool GetButton(string buttonName)
    {
        VirtualButton button = virtualButtons.Find(b => b.buttonName == buttonName);
        return button != null && button.isPressed;
    }

    // 模拟 Input.GetButtonUp
    public bool GetButtonUp(string buttonName)
    {
        VirtualButton button = virtualButtons.Find(b => b.buttonName == buttonName);
        return button != null && button.wasReleasedThisFrame;
    }

    // 模拟 Input.GetAxisRaw
    public float GetAxisRaw(string axisName)
    {
        VirtualAxis axis = virtualAxes.Find(a => a.axisName == axisName);
        if (axis != null)
        {
            // 如果有按钮控制，优先使用按钮控制的值
            bool posPressed = GetButton($"{axisName}_Positive");
            bool negPressed = GetButton($"{axisName}_Negative");

            if (posPressed && !negPressed) return 1.0f;
            if (!posPressed && negPressed) return -1.0f;
            if (posPressed && negPressed) return 0f;

            // 如果没有按钮控制，返回直接设置的值
            return axis.value;
        }

        return 0f;
    }

    // 模拟 Input.GetAxis（含平滑）
    public float GetAxis(string axisName)
    {
        // 当前不需要平滑处理，暂用此方法
        // 未来若需要可在实际中补充平滑逻辑
        return GetAxisRaw(axisName);
    }

    // 配置轴参数
    public void ConfigureAxis(string axisName, float sensitivity, float gravity, bool snap)
    {
        VirtualAxis axis = virtualAxes.Find(a => a.axisName == axisName);
        if (axis != null)
        {
            axis.sensitivity = sensitivity;
            axis.gravity = gravity;
            axis.snap = snap;
        }
    }
}
}