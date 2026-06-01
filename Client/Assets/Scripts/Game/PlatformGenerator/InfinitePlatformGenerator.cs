using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InfinitePlatformGenerator : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform grid; // 所有平台的父物体

    [Header("Generation Parameters")]
    public float lookAheadDistance = 20f;
    public float recycleDistance = 10f;
    public float maxJumpDistance = 4.8f;
    public float maxJumpHeight = 2.5f;
    public float minGap = 0.5f;

    [Header("Platform Config")]
    public List<PlatformData> platformDatas;
    [Range(0, 1)] public float dynamicProbability = 0.3f;
    [Range(0, 1)] public float trapProbability = 0.05f;

    [Header("Item & Obstacle")]
    public List<GameObject> itemPrefabs;
    [Range(0, 1)] public float itemSpawnChance = 0.2f;
    public List<GameObject> obstaclePrefabs;
    [Range(0, 1)] public float obstacleSpawnChance = 0.2f;
    public Transform[] spawnPoints; // 如果预制体没有预设点，可用此全局点，但建议预制体自带

    // 对象池
    private Dictionary<GameObject, Queue<GameObject>> platformPools = new();
    private Dictionary<GameObject, Queue<GameObject>> itemPools = new();
    private Dictionary<GameObject, Queue<GameObject>> obstaclePools = new();

    private List<PlatformController> activePlatforms = new();

    void Start()
    {
        if (platformDatas == null)
        {
            platformDatas = new List<PlatformData>();
        }
        if (platformDatas.Count == 0)
        {
            string[] platformList = { "PlatformData/Platform_base", "PlatformData/Platform_1", "PlatformData/Platform_2", "PlatformData/Platform_3", "PlatformData/Platform_4", "PlatformData/Platform_5" };
            for (int i = 0; i < platformList.Length; i++)
            {
                string key = platformList[i];
                PlatformData platformData = Resources.Load<PlatformData>(key);
                PlatformData platform = GameObject.Instantiate(platformData);
                platformDatas.Add(platform);
            }
        }
        if (itemPrefabs == null)
        {
            itemPrefabs = new List<GameObject>();
        }
        if (itemPrefabs.Count == 0)
        {
            GameObject itemPrefab = Resources.Load<GameObject>("Props/BombProp");
            GameObject item = GameObject.Instantiate(itemPrefab);
            itemPrefabs.Add(item);
        }
        if (obstaclePrefabs == null)
        {
            obstaclePrefabs = new List<GameObject>();
        }
        if (obstaclePrefabs.Count == 0)
        {
            GameObject itemPrefab = Resources.Load<GameObject>("Obstacles/Spike");
            GameObject item = GameObject.Instantiate(itemPrefab);
            obstaclePrefabs.Add(item);
        }
        grid = transform.Find("Grid");
        // 初始化对象池（可预先填充少量实例）
        GenerateStartPlatform();

        EventDispatcher.AddObserver(this, MessageEvent.AgainGame, OnAgainGame, null);
    }

    private void OnDestroy()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.AgainGame, null);
    }

    void Update()
    {
        // 向前生成
        while (NeedMorePlatforms())
        {
            GenerateNextPlatform();
        }
        // 向后回收
        RecyclePlatforms();
    }

    private bool OnAgainGame(params object[] args)
    {
        player.position = new Vector3(0, 0.5f, 0);
        RecyclePlatforms();
        GenerateStartPlatform();
        return false;
    }

    bool NeedMorePlatforms()
    {
        if (activePlatforms.Count == 0) return true;
        // 使用最后一个平台的基准右边界（避免动态移动影响）
        float farthestBaseRight = activePlatforms[^1].BaseRight;
        return farthestBaseRight < player.position.x + lookAheadDistance;
    }

    void GenerateStartPlatform()
    {
        // 使用第一个平台数据（可配置为普通平台）
        var data = platformDatas[0];

        GameObject go = GetPooledObject(data.prefab, platformPools);
        go.transform.SetParent(grid.transform);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        var controller = go.GetComponent<PlatformController>();
        controller.Init(data, Vector3.zero, player, true);

        // 不附加动态/机关组件，不生成道具/障碍

        activePlatforms.Add(controller);
    }

    void GenerateNextPlatform()
    {
        var prev = activePlatforms[^1];
        // 随机选择平台类型（考虑宽度约束）
        PlatformData data = SelectValidPlatformData(prev.width);
        if (data == null) return; // 无法生成时适当处理

        float gap = Random.Range(minGap, maxJumpDistance);
        float dy = Random.Range(-maxJumpHeight, maxJumpHeight);
        float newX = prev.BaseRight + gap;
        float newY = prev.BaseY + dy; // BaseY为基准Y坐标（生成时的y）

        var newPlatform = CreatePlatform(data, new Vector3(newX, newY, 0));
        activePlatforms.Add(newPlatform);
    }

    PlatformData SelectValidPlatformData(float prevWidth)
    {
        // 简单实现：随机选一个，如果宽度过大导致无法满足最小间隙，则重试几次
        int attempts = 10;
        while (attempts-- > 0)
        {
            var data = platformDatas[Random.Range(0, platformDatas.Count)];
            float minRequiredGap = prevWidth / 2 + data.width / 2; // 避免重叠的最小中心距离，但我们是基于边缘，所以gap>=0即可，无需此限制
            // 实际上基于边缘生成，只要gap>=0就不会重叠，所以宽度无限制
            return data;
        }
        return platformDatas[0]; // 保底
    }

    PlatformController CreatePlatform(PlatformData data, Vector3 position)
    {
        GameObject go = GetPooledObject(data.prefab, platformPools);

        // 防御性移除（确保干净）
        var oldDyn = go.GetComponent<DynamicPlatform>();
        if (oldDyn) Destroy(oldDyn);
        var oldTrap = go.GetComponent<TrapPlatform>();
        if (oldTrap) Destroy(oldTrap);

        go.transform.SetParent(grid.transform);
        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;

        var controller = go.GetComponent<PlatformController>();
        controller.Init(data, position, player);

        // 附加动态组件（按概率）
        if (Random.value < dynamicProbability && data.canBeDynamic)
        {
            var dynamic = go.AddComponent<DynamicPlatform>();
            dynamic.Init(controller);
        }

        // 附加机关组件（按概率）
        if (Random.value < trapProbability && data.canBeTrap)
        {
            var trap = go.AddComponent<TrapPlatform>();
            trap.Init(controller);
        }

        // 生成道具/障碍
        SpawnItemsAndObstacles(controller);

        return controller;
    }

    void SpawnItemsAndObstacles(PlatformController controller)
    {
        var spawnPoints = controller.GetSpawnPoints();
        foreach (var point in spawnPoints)
        {
            if (Random.value < itemSpawnChance)
            {
                var prefab = itemPrefabs[Random.Range(0, itemPrefabs.Count)];
                var item = GetPooledObject(prefab, itemPools);
                item.transform.SetParent(controller.transform);
                item.transform.localPosition = point.localPosition;
                item.transform.localRotation = Quaternion.identity;
                item.SetActive(true);
                controller.AddItem(item, prefab);
            }
            else if (Random.value < obstacleSpawnChance)
            {
                var prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Count)];
                var obstacle = GetPooledObject(prefab, obstaclePools);
                obstacle.transform.SetParent(controller.transform);
                obstacle.transform.localPosition = point.localPosition;
                obstacle.transform.localRotation = Quaternion.identity;
                obstacle.SetActive(true);
                controller.AddObstacle(obstacle, prefab);
            }
        }
    }

    void RecyclePlatforms()
    {
        for (int i = activePlatforms.Count - 1; i >= 0; i--)
        {
            var p = activePlatforms[i];
            if (p.RealRightEdge < player.position.x - recycleDistance)
            {
                RecyclePlatform(p);
                activePlatforms.RemoveAt(i);
            }
        }
    }

    void RecyclePlatform(PlatformController p)
    {
        // 回收子物体（道具/障碍）
        foreach (var (obj, prefab) in p.GetAllItems())
        {
            ReturnToPool(obj, itemPools);
        }
        foreach (var (obj, prefab) in p.GetAllObstacles())
        {
            ReturnToPool(obj, obstaclePools);
        }
        p.ClearChildren();

        // 移除动态和机关组件（防止下次复用时残留）
        var dyn = p.GetComponent<DynamicPlatform>();
        if (dyn != null) Destroy(dyn);

        var trap = p.GetComponent<TrapPlatform>();
        if (trap != null) Destroy(trap);

        // 重置平台状态（位置、碰撞器等）
        p.ResetPlatform();

        // 平台本身回池
        ReturnToPool(p.gameObject, platformPools);
    }

    // 对象池方法
    GameObject GetPooledObject(GameObject prefab, Dictionary<GameObject, Queue<GameObject>> pool)
    {
        if (!pool.ContainsKey(prefab))
            pool[prefab] = new Queue<GameObject>();

        if (pool[prefab].Count > 0)
        {
            var obj = pool[prefab].Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            var obj = Instantiate(prefab);
            PooledObject pooledObject = obj.GetComponent<PooledObject>();
            if (pooledObject == null ) 
                pooledObject = obj.AddComponent<PooledObject>(); // 确保有PooledObject组件
            pooledObject.prefab = prefab;
            return obj;
        }
    }

    void ReturnToPool(GameObject obj, Dictionary<GameObject, Queue<GameObject>> pool)
    {
        obj.SetActive(false);
        var pooled = obj.GetComponent<PooledObject>();
        if (pooled != null)
        {
            if (!pool.ContainsKey(pooled.prefab))
                pool[pooled.prefab] = new Queue<GameObject>();
            pool[pooled.prefab].Enqueue(obj);
        }
        else
        {
            Destroy(obj); // 安全处理
        }
    }
}