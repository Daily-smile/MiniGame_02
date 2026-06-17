using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
[CreateAssetMenu]
public class ScriptableStats : ScriptableObject
{
    [Header("Layers层级")]
    [Tooltip("将此设置为玩家所在的图层")]
    public LayerMask PlayerLayer;

    [Header("输入")]
    [Tooltip("使所有输入捕捉到一个整数。防止手柄走得慢。建议值为true以确保手柄/键盘的一致性。")]
    public bool SnapInput = true;

    [Tooltip("在攀登梯子或爬上壁架之前，需要有最低限度的输入操作。此举可避免使用控制器时出现意外攀爬情况 。"), Range(0.01f, 0.99f)]
    public float VerticalDeadZoneThreshold = 0.3f;

    [Tooltip("在识别向左或向右动作前，需有最低输入量。这能避免因控制器黏滞而导致的误操作 。"), Range(0.01f, 0.99f)]
    public float HorizontalDeadZoneThreshold = 0.1f;

    [Header("移动")]
    [Tooltip("最高水平移动速度")]
    public float MaxSpeed = 14;

    [Tooltip("玩家水平加速度")]
    public float Acceleration = 120;

    [Tooltip("玩家停下来的加速度")]
    public float GroundDeceleration = 60;

    [Tooltip("空中的加速度，只有在半空停止输入后才能在空气中减速")]
    public float AirDeceleration = 30;

    [Tooltip("在地面上施加的恒定向下的力。斜坡上的帮手"), Range(0f, -10f)]
    public float GroundingForce = -1.5f;

    [Tooltip("落地和顶板检测距离"), Range(0f, 0.5f)]
    public float GrounderDistance = 0.05f;

    [Header("跳跃")]
    [Tooltip("跳跃时的直接速度")]
    public float JumpPower = 36;

    [Tooltip("最大垂直移动速度")]
    public float MaxFallSpeed = 40;

    [Tooltip("玩家的下落加速度，也就是在空气重力中")]
    public float FallAcceleration = 110;

    [Tooltip("当跳跃提前释放时，重力倍增器增加")]
    public float JumpEndEarlyGravityModifier = 3;

    [Tooltip("在郊狼跳跃之前的时间变得不可用。郊狼跳跃允许跳跃执行，即使离开一个突出")]
    public float CoyoteTime = .15f;

    [Tooltip("我们缓冲一次跳跃所需的时间。这允许玩家在触地前进行跳跃输入")]
    public float JumpBuffer = .2f;
}
}