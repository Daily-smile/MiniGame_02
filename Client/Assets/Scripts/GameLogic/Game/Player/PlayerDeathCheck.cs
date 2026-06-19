using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class PlayerDeathCheck : MonoBehaviour
{
    private LayerMask platformLayer;                  // 平台所在层

    [Header("Settings")]
    public float maxRayDistance = 20f;                // 射线最大检测距离
    [Range(10, 90)]
    public int rayCount = 10;                          // 射线数量（越多检测越精确）
    public float noPlatformTimeout = 2f;            // 未检测到平台时的超时等待时间

    private float scanAngle = 150f;                     // 扇形扫描角度（左右各一半）
    private Collider2D playerCollider;
    private Rigidbody2D rb;
    private float noPlatformTimer = 0f;

    // 扫描结果结构体
    private struct ScanResult
    {
        public bool hasPlatform;
        public Vector2 hitPoint;
        public Collider2D platformCollider;
    }

    private PlayerController _playerController;
    private MirrorPlayer _mirrorPlayer;

    private void Start()
    {
        playerCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        platformLayer = 1 << LayerMask.NameToLayer("Ground");
        _playerController = GetComponentInParent<PlayerController>();
        _mirrorPlayer = GetComponentInParent<MirrorPlayer>();

        EventDispatcher.AddObserver(this, MessageEvent.AgainGame, OnAgainGame, null);
    }

    private void OnDestroy()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.AgainGame, null);
    }

    private void Update()
    {
        // 游戏状态检查
        if (!GameManager.Instance.IsGameRuningState || GameManager.Instance.GameIsOver)
            return;

        // Mirror 网络模式：只有本地玩家执行掉落检测，远程玩家的位置由 NetworkTransform 同步
        if (_mirrorPlayer != null)
        {
            if (!_mirrorPlayer.authority)
                return;
        }
        else if (_playerController != null && !_playerController.isLocalPlayer)
        {
            // 非 Mirror 模式（单机）：使用 isLocalPlayer 标志
            return;
        }

        // 仅在下降时检测
        if (rb.velocity.y < 0)
            CheckDeath();
    }

    private void CheckDeath()
    {
        // 如果检测到当前站在平台上，重置计时并返回
        if (playerCollider.IsTouchingLayers(platformLayer))
        {
            noPlatformTimer = 0f;
            return;
        }

        // 执行扇形扫描检测
        ScanResult result = ScanForPlatform();

        if (result.hasPlatform)
        {
            noPlatformTimer = 0f;
        }
        else
        {
            // 没有检测到平台，累加计时
            noPlatformTimer += Time.deltaTime;
            if (noPlatformTimer >= noPlatformTimeout)
            {
                LoseLife();
                noPlatformTimer = 0f;
            }
        }
    }

    private ScanResult ScanForPlatform()
    {
        Vector2 bottom = new Vector2(transform.position.x, playerCollider.bounds.min.y);
        float halfAngle = scanAngle * 0.5f;
        float startAngle = -halfAngle;               // 相对于垂直向下方向的偏移角
        float step = scanAngle / (rayCount - 1);

        for (int i = 0; i < rayCount; i++)
        {
            float angle = startAngle + i * step;
            Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.down;
            RaycastHit2D hit = Physics2D.Raycast(bottom, dir, maxRayDistance, platformLayer);

            if (hit.collider != null)
            {
                return new ScanResult
                {
                    hasPlatform = true,
                    hitPoint = hit.point,
                    platformCollider = hit.collider
                };
            }
        }

        return new ScanResult { hasPlatform = false };
    }

    private void LoseLife()
    {
        // 优先使用 MirrorPlayer.playerName 确保网络模式下命中正确的玩家
        // GameManager.Instance.userName 在 Host 模式下可能指向错误的玩家
        string playerName = _mirrorPlayer != null
            ? _mirrorPlayer.playerName
            : GameManager.Instance.userName;
        EventDispatcher.PostEvent(MessageEvent.PlayerOnHit, this, playerName, false);
    }

    private bool OnAgainGame(params object[] args)
    {
        noPlatformTimer = 0f;
        return false;
    }
}
}