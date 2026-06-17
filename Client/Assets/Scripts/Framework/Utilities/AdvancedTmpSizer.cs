using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace LF.Framework
{
public class AdvancedTmpSizer : MonoBehaviour
{
    [Header("Size Constraints")]
    public float maxWidth = 600f;
    public float minWidth = 100f;
    public Vector2 padding = new Vector2(20f, 10f); // 可额外添加边距

    [Header("References")]
    public RectTransform backgroundRect; // 可选的背景RectTransform

    private TMP_Text tmpText;
    private RectTransform textRectTransform;
    private bool isInit;

    void Awake()
    {
        OnInit();
    }

    public void OnInit()
    {
        if (isInit)
        {
            return;
        }
        isInit = true;
        tmpText = GetComponent<TMP_Text>();
        textRectTransform = GetComponent<RectTransform>();
        Debug.Assert(tmpText != null, "TMP_Text component not found!", this);
    }

    // 在设置文本后调用此方法
    public void UpdateSize()
    {
        if (tmpText == null) return;

        // 1. 计算无约束时的理想尺寸
        Vector2 preferredSize = tmpText.GetPreferredValues();

        float targetWidth = Mathf.Clamp(preferredSize.x, minWidth, maxWidth);
        float targetHeight;

        // 2. 如果理想宽度超过最大宽度，则在最大宽度约束下重新计算高度（考虑换行）
        if (preferredSize.x > maxWidth)
        {
            targetHeight = tmpText.GetPreferredValues(maxWidth, 0f).y;
        }
        else
        {
            targetHeight = preferredSize.y;
        }

        // 3. 应用尺寸到TMP自身的RectTransform (可加上内边距)
        Vector2 finalSize = new Vector2(targetWidth, targetHeight);
        textRectTransform.sizeDelta = finalSize;

        // 4. 如果有背景，同时更新背景大小 (可加上外边距)
        if (backgroundRect != null)
        {
            backgroundRect.sizeDelta = finalSize + padding;
        }
    }

    // 方便在Inspector中测试
    [ContextMenu("Test Update Size")]
    private void TestUpdateSize()
    {
        UpdateSize();
    }
}
}