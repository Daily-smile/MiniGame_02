using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 轴方向按钮控制器
/// </summary>
public class UIAxisButtonController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private string targetAxisName = "Horizontal"; // 目标轴名称
    [SerializeField] private bool isPositiveDirection = true; // 是否是正向

    public void OnPointerDown(PointerEventData eventData)
    {
        VirtualInputSystem.Instance.SetAxisButton(targetAxisName, isPositiveDirection, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        VirtualInputSystem.Instance.SetAxisButton(targetAxisName, isPositiveDirection, false);
    }

    private void OnDisable()
    {
        // 确保按钮状态被正确重置
        VirtualInputSystem.Instance.SetAxisButton(targetAxisName, isPositiveDirection, false);
    }
}