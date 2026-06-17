using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class DynamicPlatform : MonoBehaviour
{
    public enum MotionType { Translate, Rotate }
    public MotionType motionType;
    public float speed = 2f;
    public float range = 2f; // 平移距离或旋转角度

    private Vector3 startPos;
    private Quaternion startRot;
    private float phase;
    private PlatformController controller;

    public void Init(PlatformController ctrl)
    {
        controller = ctrl;
        startPos = ctrl.transform.position;
        startRot = ctrl.transform.rotation;
        // 随机化运动参数
        motionType = Random.value > 0.5f ? MotionType.Translate : MotionType.Rotate;
        speed = Random.Range(1f, 3f);
        range = motionType == MotionType.Translate ? Random.Range(1f, controller.width * 0.4f) : Random.Range(15f, 30f);
    }

    void Update()
    {
        phase += Time.deltaTime * speed;
        if (motionType == MotionType.Translate)
        {
            float offset = Mathf.Sin(phase) * range;
            transform.position = startPos + new Vector3(offset, 0, 0);
        }
        else
        {
            float angle = Mathf.Sin(phase) * range;
            transform.rotation = startRot * Quaternion.Euler(0, 0, angle);
        }
    }

    public void Reset()
    {
        transform.position = startPos;
        transform.rotation = startRot;
        phase = 0;
        // 确保碰撞器启用
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
    }
}
}