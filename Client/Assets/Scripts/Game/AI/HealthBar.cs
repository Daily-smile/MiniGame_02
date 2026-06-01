using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("血条组件")]
    public Slider healthSlider;          // 血条Slider组件
    public Image healthFill;             // 血条填充图像
    public TextMeshProUGUI healthText;   // 血量文本（可选）
    public Gradient healthGradient;      // 血条颜色渐变

    [Header("动画设置")]
    public bool useSmoothChange = true;  // 是否使用平滑变化
    public float smoothSpeed = 2f;       // 平滑变化速度

    [Header("伤害数字")]
    public GameObject damageTextPrefab;  // 伤害数字预制体
    public Vector2 textOffset = new Vector2(0, 50f); // 伤害数字偏移

    private float targetHealth;          // 目标血量值（用于平滑变化）
    private int maxHealth;               // 最大血量值

    void Start()
    {
        // 初始化血条
        if (healthSlider != null)
        {
            healthSlider.maxValue = 1f;
            healthSlider.value = 1f;
        }

        // 设置初始颜色
        if (healthFill != null)
        {
            healthFill.color = healthGradient.Evaluate(1f);
        }
    }

    void Update()
    {
        // 平滑更新血条
        if (useSmoothChange && healthSlider != null)
        {
            float currentValue = healthSlider.value;
            float newValue = Mathf.Lerp(currentValue, targetHealth, smoothSpeed * Time.deltaTime);

            healthSlider.value = newValue;

            // 更新血条颜色
            if (healthFill != null)
            {
                healthFill.color = healthGradient.Evaluate(newValue);
            }
        }
    }

    // 设置最大血量
    public void SetMaxHealth(int health)
    {
        maxHealth = health;
        targetHealth = 1f; // 归一化值

        if (!useSmoothChange && healthSlider != null)
        {
            healthSlider.value = 1f;
        }

        // 更新血量文本
        UpdateHealthText(maxHealth, maxHealth);
    }

    // 设置当前血量
    public void SetHealth(int health)
    {
        // 计算归一化血量值
        float normalizedHealth = Mathf.Clamp01((float)health / maxHealth);
        targetHealth = normalizedHealth;

        // 如果不使用平滑变化，直接设置血条值
        if (!useSmoothChange && healthSlider != null)
        {
            healthSlider.value = normalizedHealth;

            // 更新血条颜色
            if (healthFill != null)
            {
                healthFill.color = healthGradient.Evaluate(normalizedHealth);
            }
        }

        // 更新血量文本
        UpdateHealthText(health, maxHealth);
    }

    // 显示伤害数字
    public void ShowDamageText(int damage, Vector3 worldPosition)
    {
        if (damageTextPrefab == null) return;

        // 将世界坐标转换为屏幕坐标
        Vector2 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

        // 实例化伤害数字
        GameObject damageText = Instantiate(damageTextPrefab, transform);
        damageText.transform.position = screenPosition + textOffset;

        // 设置伤害值
        Text textComponent = damageText.GetComponent<Text>();
        if (textComponent != null)
        {
            textComponent.text = damage.ToString();
        }

        // 自动销毁
        Destroy(damageText, 1f);
    }

    // 更新血量文本
    private void UpdateHealthText(int currentHealth, int maxHealth)
    {
        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
    }

    // 设置血条可见性
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    // 重置血条
    public void ResetHealthBar()
    {
        targetHealth = 1f;

        if (healthSlider != null)
        {
            healthSlider.value = 1f;
        }

        if (healthFill != null)
        {
            healthFill.color = healthGradient.Evaluate(1f);
        }

        UpdateHealthText(maxHealth, maxHealth);
    }

    // 获取当前血量百分比
    public float GetHealthPercentage()
    {
        return healthSlider != null ? healthSlider.value : 0f;
    }

    // 获取当前血量值
    public int GetCurrentHealth()
    {
        return Mathf.RoundToInt(GetHealthPercentage() * maxHealth);
    }

    // 显示治疗数字
    public void ShowHealText(int healAmount, Vector3 worldPosition)
    {
        if (damageTextPrefab == null) return;

        Vector2 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
        GameObject healText = Instantiate(damageTextPrefab, transform);
        healText.transform.position = screenPosition + textOffset;

        Text textComponent = healText.GetComponent<Text>();
        if (textComponent != null)
        {
            textComponent.text = $"+{healAmount}";
            textComponent.color = Color.green; // 治疗数字设为绿色
        }

        Destroy(healText, 1f);
    }

    // 血条闪烁警告（当血量低时）
    public void FlashWarning(bool enable)
    {
        if (enable)
        {
            // 开始闪烁协程
            StartCoroutine(FlashRoutine());
        }
        else
        {
            // 停止闪烁，恢复原色
            StopAllCoroutines();
            if (healthFill != null)
            {
                healthFill.color = healthGradient.Evaluate(healthSlider.value);
            }
        }
    }

    private IEnumerator FlashRoutine()
    {
        while (true)
        {
            if (healthFill != null)
            {
                // 在红色和当前颜色之间闪烁
                healthFill.color = Color.Lerp(Color.red, healthGradient.Evaluate(healthSlider.value), Mathf.PingPong(Time.time * 2f, 1f));
            }
            yield return null;
        }
    }

    // 血条分段显示（用于多阶段Boss）
    public void SetHealthSegments(int segments)
    {
        // 根据阶段数调整血条外观
        // 例如：将血条分为几段，每段不同颜色
    }
}