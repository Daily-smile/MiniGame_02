using UnityEngine;
using System.Collections.Generic;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
/// <summary>
/// ���ⰴť������
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
/// ������������
/// </summary>
[System.Serializable]
public class VirtualAxis
{
    public string axisName;
    public float value;
    public float sensitivity = 3.0f; // ������
    public float gravity = 3.0f; // �����ٶ�
    public bool snap = true; // �Ƿ���ٻ���

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
/// ��������ϵͳ
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
        // Ԥע�᳣����
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
        // ������ʵ���뵽���������ӳ��
        HandleRealInputMapping();
    }
    private void OnLateUpdate()
    {
        // ÿ֡���õ�֡״̬
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

    // ע�����ⰴť
    public void RegisterButton(string buttonName)
    {
        if (virtualButtons.Find(b => b.buttonName == buttonName) == null)
        {
            virtualButtons.Add(new VirtualButton { buttonName = buttonName });
        }
    }

    // ע��������
    public void RegisterAxis(string axisName)
    {
        if (virtualAxes.Find(a => a.axisName == axisName) == null)
        {
            virtualAxes.Add(new VirtualAxis { axisName = axisName });
        }
    }

    // ���ð�ť����״̬
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

    // ���ð�ť�ͷ�״̬
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

    // ֱ��������ֵ��-1��1��
    public void SetAxis(string axisName, float value)
    {
        VirtualAxis axis = virtualAxes.Find(a => a.axisName == axisName);
        if (axis != null)
        {
            axis.value = Mathf.Clamp(value, -1.0f, 1.0f);
        }
    }

    // �����᷽��ť״̬
    public void SetAxisButton(string axisName, bool isPositive, bool isPressed)
    {
        VirtualAxis axis = virtualAxes.Find(a => a.axisName == axisName);
        if (axis != null)
        {
            // ����Ψһ�İ�ť���������ڲ�����
            string buttonName = $"{axisName}_{(isPositive ? "Positive" : "Negative")}";

            // ע�ᰴť�������δע�ᣩ
            RegisterButton(buttonName);

            // ���ð�ť״̬
            if (isPressed)
            {
                SetButtonDown(buttonName);
            }
            else
            {
                SetButtonUp(buttonName);
            }

            // ������ֵ
            bool posPressed = GetButton($"{axisName}_Positive");
            bool negPressed = GetButton($"{axisName}_Negative");

            if (posPressed && negPressed)
            {
                // ����������򶼰��£����������þ�����Ϊ
                // Ĭ����Ϊ�ǵ���Ϊ0
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

    // ģ�� Input.GetButtonDown
    public bool GetButtonDown(string buttonName)
    {
        VirtualButton button = virtualButtons.Find(b => b.buttonName == buttonName);
        return button != null && button.wasPressedThisFrame;
    }

    // ģ�� Input.GetButton
    public bool GetButton(string buttonName)
    {
        VirtualButton button = virtualButtons.Find(b => b.buttonName == buttonName);
        return button != null && button.isPressed;
    }

    // ģ�� Input.GetButtonUp
    public bool GetButtonUp(string buttonName)
    {
        VirtualButton button = virtualButtons.Find(b => b.buttonName == buttonName);
        return button != null && button.wasReleasedThisFrame;
    }

    // ģ�� Input.GetAxisRaw
    public float GetAxisRaw(string axisName)
    {
        VirtualAxis axis = virtualAxes.Find(a => a.axisName == axisName);
        if (axis != null)
        {
            // ����а�ť���ƣ�����ʹ�ð�ť���Ƶ�ֵ
            bool posPressed = GetButton($"{axisName}_Positive");
            bool negPressed = GetButton($"{axisName}_Negative");

            if (posPressed && !negPressed) return 1.0f;
            if (!posPressed && negPressed) return -1.0f;
            if (posPressed && negPressed) return 0f;

            // ���û�а�ť���ƣ�����ֱ�����õ�ֵ
            return axis.value;
        }

        return 0f;
    }

    // ģ�� Input.GetAxis (��ƽ��)
    public float GetAxis(string axisName)
    {
        // ������Ҫƽ����������������ʹ�ô˷���
        // ����򵥷���GetAxisRaw��ʵ���п�������ƽ������
        return GetAxisRaw(axisName);
    }

    // ���������
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