using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class InfinitePlatformGenerator : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform grid; // ����ƽ̨�ĸ�����

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
    public Transform[] spawnPoints; // ���Ԥ����û��Ԥ��㣬���ô�ȫ�ֵ㣬������Ԥ�����Դ�

    // �����
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
            string[] platformList = { "PlatformData_Platform_base", "PlatformData_Platform_1", "PlatformData_Platform_2", "PlatformData_Platform_3", "PlatformData_Platform_4", "PlatformData_Platform_5" };
            for (int i = 0; i < platformList.Length; i++)
            {
                string key = platformList[i];
                PlatformData platformData = ResourceManager.Instance.LoadAsset<PlatformData>(key);
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
            GameObject itemPrefab = ResourceManager.Instance.LoadAsset<GameObject>("Props_BombProp");
            GameObject item = GameObject.Instantiate(itemPrefab);
            itemPrefabs.Add(item);
        }
        if (obstaclePrefabs == null)
        {
            obstaclePrefabs = new List<GameObject>();
        }
        if (obstaclePrefabs.Count == 0)
        {
            GameObject itemPrefab = ResourceManager.Instance.LoadAsset<GameObject>("Obstacles_Spike");
            GameObject item = GameObject.Instantiate(itemPrefab);
            obstaclePrefabs.Add(item);
        }
        grid = transform.Find("Grid");
        // ��ʼ������أ���Ԥ���������ʵ����
        GenerateStartPlatform();

        EventDispatcher.AddObserver(this, MessageEvent.AgainGame, OnAgainGame, null);
    }

    private void OnDestroy()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.AgainGame, null);
    }

    void Update()
    {
        // ��ǰ����
        while (NeedMorePlatforms())
        {
            GenerateNextPlatform();
        }
        // ������
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
        // ʹ�����һ��ƽ̨�Ļ�׼�ұ߽磨���⶯̬�ƶ�Ӱ�죩
        float farthestBaseRight = activePlatforms[^1].BaseRight;
        return farthestBaseRight < player.position.x + lookAheadDistance;
    }

    void GenerateStartPlatform()
    {
        // ʹ�õ�һ��ƽ̨���ݣ�������Ϊ��ͨƽ̨��
        var data = platformDatas[0];

        GameObject go = GetPooledObject(data.prefab, platformPools);
        go.transform.SetParent(grid.transform);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        var controller = go.GetComponent<PlatformController>();
        controller.Init(data, Vector3.zero, player, true);

        // �����Ӷ�̬/��������������ɵ���/�ϰ�

        activePlatforms.Add(controller);
    }

    void GenerateNextPlatform()
    {
        var prev = activePlatforms[^1];
        // ���ѡ��ƽ̨���ͣ����ǿ���Լ����
        PlatformData data = SelectValidPlatformData(prev.width);
        if (data == null) return; // �޷�����ʱ�ʵ�����

        float gap = Random.Range(minGap, maxJumpDistance);
        float dy = Random.Range(-maxJumpHeight, maxJumpHeight);
        float newX = prev.BaseRight + gap;
        float newY = prev.BaseY + dy; // BaseYΪ��׼Y���꣨����ʱ��y��

        var newPlatform = CreatePlatform(data, new Vector3(newX, newY, 0));
        activePlatforms.Add(newPlatform);
    }

    PlatformData SelectValidPlatformData(float prevWidth)
    {
        // ��ʵ�֣����ѡһ����������ȹ������޷�������С��϶�������Լ���
        int attempts = 10;
        while (attempts-- > 0)
        {
            var data = platformDatas[Random.Range(0, platformDatas.Count)];
            float minRequiredGap = prevWidth / 2 + data.width / 2; // �����ص�����С���ľ��룬�������ǻ��ڱ�Ե������gap>=0���ɣ����������
            // ʵ���ϻ��ڱ�Ե���ɣ�ֻҪgap>=0�Ͳ����ص������Կ���������
            return data;
        }
        return platformDatas[0]; // ����
    }

    PlatformController CreatePlatform(PlatformData data, Vector3 position)
    {
        GameObject go = GetPooledObject(data.prefab, platformPools);

        // �������Ƴ���ȷ���ɾ���
        var oldDyn = go.GetComponent<DynamicPlatform>();
        if (oldDyn) Destroy(oldDyn);
        var oldTrap = go.GetComponent<TrapPlatform>();
        if (oldTrap) Destroy(oldTrap);

        go.transform.SetParent(grid.transform);
        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;

        var controller = go.GetComponent<PlatformController>();
        controller.Init(data, position, player);

        // ���Ӷ�̬����������ʣ�
        if (Random.value < dynamicProbability && data.canBeDynamic)
        {
            var dynamic = go.AddComponent<DynamicPlatform>();
            dynamic.Init(controller);
        }

        // ���ӻ�������������ʣ�
        if (Random.value < trapProbability && data.canBeTrap)
        {
            var trap = go.AddComponent<TrapPlatform>();
            trap.Init(controller);
        }

        // ���ɵ���/�ϰ�
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
        // ���������壨����/�ϰ���
        foreach (var (obj, prefab) in p.GetAllItems())
        {
            ReturnToPool(obj, itemPools);
        }
        foreach (var (obj, prefab) in p.GetAllObstacles())
        {
            ReturnToPool(obj, obstaclePools);
        }
        p.ClearChildren();

        // �Ƴ���̬�ͻ����������ֹ�´θ���ʱ������
        var dyn = p.GetComponent<DynamicPlatform>();
        if (dyn != null) Destroy(dyn);

        var trap = p.GetComponent<TrapPlatform>();
        if (trap != null) Destroy(trap);

        // ����ƽ̨״̬��λ�á���ײ���ȣ�
        p.ResetPlatform();

        // ƽ̨�����س�
        ReturnToPool(p.gameObject, platformPools);
    }

    // ����ط���
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
                pooledObject = obj.AddComponent<PooledObject>(); // ȷ����PooledObject���
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
            Destroy(obj); // ��ȫ����
        }
    }
}
}