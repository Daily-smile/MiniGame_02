using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour, IPlayerController, IDamageable
{
    /// <summary>
    /// 是否由本地玩家控制。
    /// 当挂载 MirrorPlayer 时，只有 hasAuthority 的客户端才设为 true
    /// </summary>
    [HideInInspector] public bool isLocalPlayer = true;

    [SerializeField] private ScriptableStats _stats;
    private Rigidbody2D _rb;
    private CapsuleCollider2D _col;
    private FrameInput _frameInput;
    private Vector2 _frameVelocity;
    private bool _cachedQueryStartInColliders;

    private float _gravity;
    private float _time;

    private RaycastHit2D downHit;
    private RaycastHit2D upHit;
    private Vector3 drawPos;
    private Vector3 groundHitPoint; // 着地点位置

    // 藤蔓相关变量
    private Tengman _currentVine;
    private bool _isOnVine = false;
    private bool _isStayOnVine = false;
    private bool _jumpFromVineConsumed = false; // 藤蔓跳跃是否已消耗

    private int recordPlatformID;
    /// <summary>
    /// 是否已死亡
    /// </summary>
    private bool _isDead;
    private bool _isGameEnd;
    #region 属性接口
    public Vector2 FrameInput => _frameInput.Move;
    public event Action<bool, float> GroundedChanged;
    public event Action Fire;
    public event Action Jumped;
    #endregion

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<CapsuleCollider2D>();

        _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
        EventDispatcher.AddObserver(this, MessageEvent.AgainGame, OnAgainGame, null);
        EventDispatcher.AddObserver(this, MessageEvent.GameWin, OnGameEnd, null);
        EventDispatcher.AddObserver(this, MessageEvent.GameEnd, OnGameEnd, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPlayerDead, OnDead, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnPlayerFullRebirth, OnFullRebirth, null);
        EventDispatcher.AddObserver(this, MessageEvent.InfinityModelSetGroundHitPoint, InfinityModelSetGroundHitPoint, null);
    }

    private void Start()
    {
        UpdateManager.Instance.RegisterNormalUpdate(OnNormalUpdate);
        _gravity = _rb.gravityScale;
        drawPos = transform.position;
        groundHitPoint = drawPos;
        _rb.simulated = true;
    }

    private void OnDestroy()
    {
        UpdateManager.Instance?.Unregister(OnNormalUpdate);
        EventDispatcher.RemoveObserver(this, MessageEvent.AgainGame, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.GameWin, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.GameEnd, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPlayerDead, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnPlayerFullRebirth, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.InfinityModelSetGroundHitPoint, null);
    }

    private bool CheckInvalid()
    {
        return _isDead || _isGameEnd;
    }

    #region 全局事件监听

    private bool OnAgainGame(params object[] args)
    {
        if (!isLocalPlayer) return false;
        recordPlatformID = 0;
        transform.position = drawPos;
        _time = 0;
        _frameInput.Move = Vector2.zero;
        _frameVelocity = Vector2.zero;
        _rb.velocity = Vector2.zero;
        _endedJumpEarly = false;
        _timeJumpWasPressed = 0;
        _bufferedJumpUsable = false;
        _coyoteUsable = false;
        _isOnVine = false;
        _currentVine = null;
        _jumpFromVineConsumed = false;
        _isDead = false;
        _isGameEnd = false;
        _rb.simulated = true;
        return false;
    }
    private bool OnGameEnd(params object[] args)
    {
        _frameInput.Move = Vector2.zero;
        _frameVelocity = Vector2.zero;
        _rb.velocity = Vector2.zero;
        _endedJumpEarly = false;
        _timeJumpWasPressed = 0;
        _bufferedJumpUsable = false;
        _coyoteUsable = false;
        _isOnVine = false;
        _currentVine = null;
        _jumpFromVineConsumed = false;
        _isGameEnd = true;
        _rb.simulated = false;
        return false;
    }

    private bool OnDead(params object[] args)
    {
        if (!isLocalPlayer) return false;
        _isDead = true;
        EventDispatcher.PostEvent(MessageEvent.GameOver, this, null);
        _rb.simulated = false;
        return false;
    }

    private bool InfinityModelSetGroundHitPoint(params object[] args)
    {
        if (!isLocalPlayer) return false;
        groundHitPoint = (Vector3)args[0];
        recordPlatformID = (int)args[1];
        return false;
    }

    private bool OnFullRebirth(params object[] args)
    {
        if (!isLocalPlayer) return false;
        transform.position = groundHitPoint;
        _time = 0;
        _frameInput.Move = Vector2.zero;
        _frameVelocity = Vector2.zero;
        _rb.velocity = Vector2.zero;
        _endedJumpEarly = false;
        _timeJumpWasPressed = 0;
        _bufferedJumpUsable = false;
        _coyoteUsable = false;
        _isOnVine = false;
        _currentVine = null;
        _jumpFromVineConsumed = false;
        _isDead = false;
        _isGameEnd = false;
        _rb.simulated = true;
        Action<Vector3> setPosEvent = (pos) =>
        {
            transform.position = pos;
        };
        EventDispatcher.PostEvent(MessageEvent.BackPlatformRebirth, this, recordPlatformID, setPosEvent);
        return false;
    }

    #endregion

    public void OnNormalUpdate()
    {
        if (!isLocalPlayer || CheckInvalid())
        {
            return;
        }
        _time += Time.deltaTime;
        GatherInput();
        HandleFire();
        // 藤蔓跳跃在Update中处理以确保及时响应
        HandleVineJump();
    }

    private void GatherInput()
    {
        _frameInput = new FrameInput
        {
            JumpDown = GameInputSystem.instance.JumpDown,
            JumpHeld = GameInputSystem.instance.JumpHeld,
            Move = GameInputSystem.instance.Move
        };

        if (_stats.SnapInput)
        {
            _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < _stats.HorizontalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.x);
            _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < _stats.VerticalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.y);
        }

        if (_frameInput.JumpDown)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
        }
    }

    private void FixedUpdate()
    {
        if (CheckInvalid())
        {
            _rb.gravityScale = 0;
            return;
        }
        _rb.gravityScale = _gravity;

        CheckCollisions();
        HandleVineMovement(); // 藤蔓上的移动

        HandleJump();
        HandleDirection();
        HandleGravity();

        ApplyMovement();
    }

    #region 碰撞检测
    private float _frameLeftGrounded = float.MinValue;
    private bool _grounded;

    private void CheckCollisions()
    {
        Physics2D.queriesStartInColliders = false;

        downHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer);
        upHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up, _stats.GrounderDistance, ~_stats.PlayerLayer);
        bool groundHit = downHit.collider != null;
        if (downHit.collider != null && GameManager.Instance.gameModel != GameManager.GameModel.Infinity)
        {
            Tilemap tileMap = downHit.transform.GetComponent<Tilemap>();
            if (tileMap != null)
            {
                // 获取碰撞点并微调偏移
                groundHitPoint = downHit.point;
                // 沿碰撞点的碰撞法线方向做微小偏移，确保检测点在瓦片内部
                Vector3 adjustedWorldPosition = groundHitPoint - (Vector3)(downHit.normal * 0.01f);
                groundHitPoint = adjustedWorldPosition;
                // 将世界坐标转换为Tilemap的单元格坐标
                Vector3Int cellPosition = tileMap.WorldToCell(adjustedWorldPosition);
                // 获取该单元格的瓦片
                TileBase tile = tileMap.GetTile(cellPosition);
                if (tile != null)
                {
                    if (tile is RuleTile ruleTile)
                    {
                        groundHit = ruleTile.m_DefaultGameObject.layer == LayerMask.NameToLayer("Ground");
                    }
                    else
                    {
                        // 其他类型瓦片无需额外判断
                    }
                }
            }
        }

        bool ceilingHit = upHit;
        if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

        if (!_grounded && groundHit)
        {
            _grounded = true;
            _coyoteUsable = true;
            _bufferedJumpUsable = true;
            _endedJumpEarly = false;
            GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));

            // 着地时离开藤蔓
            if (_isOnVine)
            {
                OnVineExit();
            }
        }
        else if (_grounded && !groundHit)
        {
            _grounded = false;
            _frameLeftGrounded = _time;
            GroundedChanged?.Invoke(false, 0);
        }

        Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
    }
    #endregion

    #region 藤蔓系统
    public void OnVineEnter(Tengman vine)
    {
        _currentVine = vine;
        _isOnVine = true;
        _jumpFromVineConsumed = false; // 重新进入藤蔓时重置跳跃标记
        _isStayOnVine = false;

        // 接触藤蔓时立即停止垂直速度
        _frameVelocity.y = 0;
        _gravity = _rb.gravityScale;
        _rb.gravityScale = 0; // 暂时关闭重力
    }

    public void OnVineStay(Tengman vine)
    {
        _currentVine = vine;
        _isOnVine = true;
        _isStayOnVine = true;
    }

    public void OnVineExit()
    {
        _isOnVine = false;
        _currentVine = null;
        _jumpFromVineConsumed = false; // 离开藤蔓时重置跳跃标记
        _rb.gravityScale = _gravity; // 恢复重力
    }

    private void HandleVineMovement()
    {
        if (!_isOnVine) return;

        // 藤蔓上滑动/下滑
        float slideSpeed;
        if (_currentVine != null)
        {
            if (_isStayOnVine)
            {
                var inAirGravity = _stats.FallAcceleration * .5f;
                slideSpeed = Mathf.MoveTowards(_frameVelocity.y, -1f, inAirGravity * Time.fixedDeltaTime);
            }
            else
            {
                slideSpeed = -_currentVine.GetSlideSpeed();
            }
        }
        else
        {
            slideSpeed = -1f;
        }

        // 检测是否靠墙（碰撞后限制水平移动）
        bool isAgainstWall = CheckWallCollision(_frameInput.Move.x);

        if (isAgainstWall)
        {
            // 靠墙时只允许下滑，不允许水平移动
            _frameVelocity.x = 0;
        }
        else
        {
            // 不靠墙时可以自由水平移动
            if (_frameInput.Move.x != 0)
            {
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * _stats.MaxSpeed * 0.5f, _stats.Acceleration * Time.fixedDeltaTime);
            }
            else
            {
                // 无输入时水平减速
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, _stats.AirDeceleration * Time.fixedDeltaTime);
            }
        }

        // 始终保持下滑
        _frameVelocity.y = slideSpeed;
    }

    // 墙壁碰撞检测
    private bool CheckWallCollision(float direction)
    {
        if (direction == 0) return false;

        Physics2D.queriesStartInColliders = false;
        // 向指定方向检测是否靠墙
        float checkDistance = 0.1f; // 检测距离
        Vector2 checkDirection = direction > 0 ? Vector2.right : Vector2.left;

        RaycastHit2D hit = Physics2D.CapsuleCast(
            _col.bounds.center,
            _col.size,
            _col.direction,
            0,
            checkDirection,
            checkDistance,
            ~_stats.PlayerLayer
        );
        Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
        return hit.collider != null && !hit.collider.isTrigger;
    }

    // 藤蔓跳跃在Update中处理以确保及时响应
    private void HandleVineJump()
    {
        if (_isOnVine && _frameInput.JumpDown && !_jumpFromVineConsumed)
        {
            ExecuteVineJump();
            _jumpFromVineConsumed = true;
        }

        // 松开跳跃键时重置标记
        if (!_frameInput.JumpDown)
        {
            _jumpFromVineConsumed = false;
        }
    }

    // 执行藤蔓跳跃
    private void ExecuteVineJump()
    {
        _endedJumpEarly = false;
        _timeJumpWasPressed = 0;
        _bufferedJumpUsable = false;
        _coyoteUsable = false;

        // 藤蔓专用跳跃力加成
        float jumpPower = _stats.JumpPower;
        if (_currentVine != null)
        {
            jumpPower *= _currentVine.GetJumpBoost();
        }

        // 有水平输入时沿方向跳跃
        Vector2 jumpDirection = Vector2.up;
        if (_frameInput.Move.x != 0)
        {
            // 有水平输入时跳跃方向偏移
            jumpDirection.x = _frameInput.Move.x * 0.5f;
            jumpDirection.Normalize();
        }

        _frameVelocity = jumpDirection * jumpPower;
        Jumped?.Invoke();

        // 跳跃后离开藤蔓
        OnVineExit();
    }
    #endregion

    #region 攻击
    private void HandleFire()
    {
        if (GameInputSystem.instance.Fire)
        {
            Fire?.Invoke();
        }
    }
    #endregion

    #region Jumping

    private bool _jumpToConsume;
    private bool _bufferedJumpUsable;
    private bool _endedJumpEarly;
    private bool _coyoteUsable;
    private float _timeJumpWasPressed;

    private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + _stats.JumpBuffer;
    private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + _stats.CoyoteTime;

    private void HandleJump()
    {
        // 藤蔓跳跃已在HandleVineJump中处理
        if (_isOnVine) return;

        if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.velocity.y > 0) _endedJumpEarly = true;

        if (!_jumpToConsume && !HasBufferedJump) return;

        if ((_frameInput.JumpHeld && _grounded) || CanUseCoyote)
        {
            ExecuteJump();
        }

        _jumpToConsume = false;
    }

    private void ExecuteJump()
    {
        _endedJumpEarly = false;
        _timeJumpWasPressed = 0;
        _bufferedJumpUsable = false;
        _coyoteUsable = false;

        _frameVelocity.y = _stats.JumpPower;
        Jumped?.Invoke();
    }

    #endregion

    #region Horizontal

    private void HandleDirection()
    {
        if (_isOnVine)
        {
            // 藤蔓上的水平移动已在HandleVineMovement中处理
            return;
        }

        if (_frameInput.Move.x == 0)
        {
            var deceleration = _grounded ? _stats.GroundDeceleration : _stats.AirDeceleration;
            _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
        }
        else
        {
            _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * _stats.MaxSpeed, _stats.Acceleration * Time.fixedDeltaTime);
        }
    }

    #endregion

    #region Gravity And ApplyMovement

    private void HandleGravity()
    {
        if (_isOnVine)
        {
            // 藤蔓上的重力已在HandleVineMovement中处理
            return;
        }

        if (_grounded && _frameVelocity.y <= 0f)
        {
            _frameVelocity.y = _stats.GroundingForce;
        }
        else
        {
            var inAirGravity = _stats.FallAcceleration;
            if (_endedJumpEarly && _frameVelocity.y > 0) inAirGravity *= _stats.JumpEndEarlyGravityModifier;
            _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -_stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
        }
    }

    private void ApplyMovement() => _rb.velocity = _frameVelocity;
    #endregion

    #region 发射器反冲
    [SerializeField] private float _shooterRecoilForce = 60f;
    public void HandleShooterBuffer()
    {
        _frameVelocity.y = _shooterRecoilForce;
    }
    #endregion

    #region 处理受到的伤害
    public void TakeDamage(DamageData damageData)
    {
        bool isDeadly = damageData.damageType == DamageType.Fall || damageData.damageType == DamageType.Bomb;
        // 优先使用 MirrorPlayer.playerName 确保网络模式下命中正确的玩家
        // GameManager.Instance.userName 在 Host 模式下可能指向错误的玩家
        string playerName;
        MirrorPlayer mp = GetComponentInParent<MirrorPlayer>();
        if (mp != null && !string.IsNullOrEmpty(mp.playerName))
            playerName = mp.playerName;
        else
            playerName = GameManager.Instance.userName;
        EventDispatcher.PostEvent(MessageEvent.PlayerOnHit, this, playerName, isDeadly);
    }

    public bool IsAlive()
    {
        return !_isDead;
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }
    #endregion
}

public struct FrameInput
{
    public bool JumpDown;
    public bool JumpHeld;
    public Vector2 Move;
}

public interface IPlayerController
{
    public event Action<bool, float> GroundedChanged;
    public event Action Fire;
    public event Action Jumped;
    public Vector2 FrameInput { get; }
    /// <summary>
    /// 发射器反冲Buffer
    /// </summary>
    public abstract void HandleShooterBuffer();
}
}