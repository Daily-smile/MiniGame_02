using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 虚拟摇杆控制器
/// </summary>
public class UIJoystickController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;
    [SerializeField] private float handleRange = 1.0f;

    private Vector2 inputVector = Vector2.zero;

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 direction;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground, eventData.position, eventData.pressEventCamera, out direction))
        {
            // 计算摇杆偏移量
            direction.x /= joystickBackground.sizeDelta.x;
            direction.y /= joystickBackground.sizeDelta.y;

            // 限制摇杆移动范围
            inputVector = (direction.magnitude > 1.0f) ? direction.normalized : direction;

            // 更新摇杆手柄位置
            joystickHandle.anchoredPosition = new Vector2(
                inputVector.x * (joystickBackground.sizeDelta.x * handleRange),
                inputVector.y * (joystickBackground.sizeDelta.y * handleRange));

            // 更新虚拟输入
            VirtualInputSystem.Instance.SetAxis("Horizontal", inputVector.x);
            VirtualInputSystem.Instance.SetAxis("Vertical", inputVector.y);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 重置摇杆位置和输入
        inputVector = Vector2.zero;
        joystickHandle.anchoredPosition = Vector2.zero;

        // 重置虚拟输入
        VirtualInputSystem.Instance.SetAxis("Horizontal", 0);
        VirtualInputSystem.Instance.SetAxis("Vertical", 0);
    }
}