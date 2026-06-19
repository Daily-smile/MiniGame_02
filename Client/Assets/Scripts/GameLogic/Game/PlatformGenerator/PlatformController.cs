using System;
using System.Collections.Generic;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class PlatformController : MonoBehaviour
{
    public float width;          // 来自PlatformData的值
    public float height;

    private Vector3 basePosition; // 生成时的基准位置
    private PlatformData data;
    private Collider2D platformCollider;
    private Transform playerObj;
    private bool isReset = false;

    private List<GameObject> itemObjects = new();
    private List<GameObject> itemPrefabRefs = new();
    private List<GameObject> obstacleObjects = new();
    private List<GameObject> obstaclePrefabRefs = new();

    // 基准右边界（生成时的位置+宽度），用于生成下一个平台
    public float BaseRight => basePosition.x + width;
    public float BaseY => basePosition.y;

    // 实时右边界（考虑动态移动后的实际边界），用于回收判断
    public float RealRightEdge => platformCollider ? platformCollider.bounds.max.x : transform.position.x + width;

    void Awake()
    {
        platformCollider = GetComponent<Collider2D>();
        if (platformCollider == null)
            Debug.LogError("PlatformController: No Collider2D found on platform prefab!");
        EventDispatcher.AddObserver(this, MessageEvent.BackPlatformRebirth, BackPlatformRebirth, null);
    }

    private void OnDestroy()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.BackPlatformRebirth, null);
    }

    public void Init(PlatformData data, Vector3 spawnPos, Transform playerTran, bool isOrigin = false)
    {
        this.data = data;
        this.width = data.width;
        this.height = data.height;
        this.basePosition = spawnPos;
        this.playerObj = playerTran;
        this.isReset = isOrigin;

        transform.position = spawnPos;
        transform.rotation = Quaternion.identity;

        // 确保碰撞器已启用
        if (platformCollider != null && !platformCollider.enabled)
            platformCollider.enabled = true;
    }

    private bool BackPlatformRebirth(params object[] args)
    {
        if (args == null || args.Length == 0)
        {
            return false;
        }
        if (GetInstanceID() != (int)args[0])
        {
            return false;
        }
        ResetPlatform();
        Action<Vector3> setPosCallback = args.Length > 1 ? (Action<Vector3>)args[1] : null;
        if (setPosCallback == null)
        {
            return false;
        }
        Vector3 drawPos;
        Transform[] drawPoints = GetSpawnPoints();
        if (drawPoints.Length > 0)
        {
            int index = UnityEngine.Random.Range(0, drawPoints.Length);
            drawPos = drawPoints[index].position;
        }
        else
        {
            drawPos = transform.position + Vector3.up * 0.5f;
        }
        setPosCallback.Invoke(drawPos);
        return false;
    }

    // 获取所有标记为"SpawnPoint"的子对象
    public Transform[] GetSpawnPoints()
    {
        var points = new List<Transform>();
        foreach (Transform child in transform)
            if (child.CompareTag("SpawnPoint"))
                points.Add(child);
        return points.ToArray();
    }

    public void AddItem(GameObject obj, GameObject prefab)
    {
        itemObjects.Add(obj);
        itemPrefabRefs.Add(prefab);
    }
    public void AddObstacle(GameObject obj, GameObject prefab)
    {
        obstacleObjects.Add(obj);
        obstaclePrefabRefs.Add(prefab);
    }

    public int ItemCount => itemObjects.Count;
    public int ObstacleCount => obstacleObjects.Count;

    public GameObject GetItemObject(int index) => itemObjects[index];
    public GameObject GetItemPrefab(int index) => itemPrefabRefs[index];
    public GameObject GetObstacleObject(int index) => obstacleObjects[index];
    public GameObject GetObstaclePrefab(int index) => obstaclePrefabRefs[index];

    public void ClearChildren()
    {
        itemObjects.Clear();
        itemPrefabRefs.Clear();
        obstacleObjects.Clear();
        obstaclePrefabRefs.Clear();
    }

    // 重置平台到初始状态
    public void ResetPlatform()
    {
        transform.position = basePosition;
        transform.rotation = Quaternion.identity;
        isReset = false;

        if (platformCollider != null)
            platformCollider.enabled = true;

        // 重置可能附加的动态/陷阱组件
        var dyn = GetComponent<DynamicPlatform>();
        if (dyn) dyn.Reset();

        var trap = GetComponent<TrapPlatform>();
        if (trap) trap.Reset();
    }

    // 编辑器可视化辅助
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 center = transform.position + new Vector3(width / 2f, height / 2f, 0);
        Gizmos.DrawWireCube(center, new Vector3(width, height, 0));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("NetPlayer"))
        {
            Vector3 drawPos;
            Transform[] drawPoints = GetSpawnPoints();
            if (drawPoints.Length > 0)
            {
                int index = UnityEngine.Random.Range(0, drawPoints.Length);
                drawPos = drawPoints[index].position;
            }
            else
            {
                drawPos = transform.position + Vector3.up * 0.5f;
            }
            EventDispatcher.PostEvent(MessageEvent.InfinityModelSetGroundHitPoint, this, drawPos, GetInstanceID());
        }
    }

    private void Update()
    {
        if (playerObj == null || isReset) return;
        if (playerObj.position.x >= transform.position.x)
        {
            isReset = true;
            EventDispatcher.PostEvent(MessageEvent.UpdateInfinityScore, this, 1);
        }
    }
}
}