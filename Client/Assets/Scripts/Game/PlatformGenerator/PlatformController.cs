using System;
using System.Collections.Generic;
using UnityEngine;

public class PlatformController : MonoBehaviour
{
    public float width;          // ��PlatformData��ֵ
    public float height;

    private Vector3 basePosition; // ����ʱ�Ļ�׼λ��
    private PlatformData data;
    private Collider2D platformCollider;
    private Transform playerObj;
    private bool isReset = false;

    private List<(GameObject obj, GameObject prefab)> items = new();
    private List<(GameObject obj, GameObject prefab)> obstacles = new();

    // ��׼�ұ߽磨����ʱ��λ��+���ȣ�������������һ��ƽ̨
    public float BaseRight => basePosition.x + width;
    public float BaseY => basePosition.y;

    // ʵʱ�ұ߽磨���Ƕ�̬�ƶ����ʵ�ʱ߽磩�����ڻ����ж�
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

        // ȷ����ײ������
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

    // ��ȡ���б��Ϊ"SpawnPoint"��������
    public Transform[] GetSpawnPoints()
    {
        var points = new List<Transform>();
        foreach (Transform child in transform)
            if (child.CompareTag("SpawnPoint"))
                points.Add(child);
        return points.ToArray();
    }

    public void AddItem(GameObject obj, GameObject prefab) => items.Add((obj, prefab));
    public void AddObstacle(GameObject obj, GameObject prefab) => obstacles.Add((obj, prefab));

    public List<(GameObject obj, GameObject prefab)> GetAllItems() =>
        items.ConvertAll(x => (x.obj, x.prefab));
    public List<(GameObject obj, GameObject prefab)> GetAllObstacles() =>
        obstacles.ConvertAll(x => (x.obj, x.prefab));

    public void ClearChildren()
    {
        items.Clear();
        obstacles.Clear();
    }

    // ����ǰ����ƽ̨״̬
    public void ResetPlatform()
    {
        transform.position = basePosition;
        transform.rotation = Quaternion.identity;
        isReset = false;

        if (platformCollider != null)
            platformCollider.enabled = true;

        // ���ÿ��ܸ��ӵĶ�̬/�������
        var dyn = GetComponent<DynamicPlatform>();
        if (dyn) dyn.Reset();

        var trap = GetComponent<TrapPlatform>();
        if (trap) trap.Reset();
    }

    // �༭�����ӻ�����
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