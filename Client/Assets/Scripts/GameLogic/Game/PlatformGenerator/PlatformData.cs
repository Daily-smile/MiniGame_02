using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
[CreateAssetMenu(fileName = "PlatformData", menuName = "Game/PlatformData")]
public class PlatformData : ScriptableObject
{
    public GameObject prefab;
    public float width = 4f;
    public float height = 1f;
    public float weight = 1f; // 生成权重
    public bool canBeDynamic = true;
    public bool canBeTrap = true;
}
}