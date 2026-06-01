using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnermyStateType
{
    Idle, Patrol, Chase, React, Attack, Hit, Death
}

[Serializable]
public class Parameter
{
    public int health;
    public float moveSpeed;
    public float chaseSpeed;
    public float idleTime;
    public Vector3[] patrolPoints;
    public Vector3[] chasePoints;
    public Transform target;
    public LayerMask targetLayer;
    public Transform attackPoint;
    public float attackArea;
    public float viewArea;
    public Vector2 moveFrameInput;
    public Animator animator;
    public Rigidbody2D rb;
    public bool curFlipX;
    public bool getHit;
    public bool isDead;
}

[Serializable]
public struct StartAnim
{
    public EnermyStateType stateType;
    public string animName;
}

public class FSM : MonoBehaviour, IDamageable
{
    private bool startInColliders;
    private RaycastHit2D originGroundHit;
    private IState currentState;
    private SpriteRenderer _sprite;
    public SpriteRenderer sprite { get { return _sprite; } }
    [SerializeField]
    private List<StartAnim> state_anim = new List<StartAnim>();
    private Dictionary<EnermyStateType, IState> states = new Dictionary<EnermyStateType, IState>();

    [Header("��Ⱦʵ��")]
    public Transform origin;
    public Parameter parameter;
    [HideInInspector]
    public Vector2 originSize;
    [HideInInspector]
    public LayerMask layerMask;

    // ����
    private Vector3 drawPos;
    private bool isFlipX;
    private Parameter temp_parameter;

    void Awake()
    {
        // ȷ����Awake�г�ʼ���������
        if (parameter.animator == null)
            parameter.animator = GetComponent<Animator>();
        if (parameter.rb == null)
            parameter.rb = GetComponent<Rigidbody2D>();

        // ȷ��origin������
        if (origin == null)
            origin = transform;

        // ����sprite
        Transform spriteTransform = transform.Find("sprite");
        if (spriteTransform != null)
        {
            _sprite = spriteTransform.GetComponent<SpriteRenderer>();
            if (_sprite != null)
            {
                isFlipX = _sprite.flipX;
                parameter.curFlipX = _sprite.flipX;
            }
        }
    }

    void Start()
    {
        // ��ʼ��״̬��
        states.Add(EnermyStateType.Idle, new IdleState(this));
        states.Add(EnermyStateType.Patrol, new PatrolState(this));
        states.Add(EnermyStateType.Chase, new ChaseState(this));
        states.Add(EnermyStateType.React, new ReactState(this));
        states.Add(EnermyStateType.Attack, new AttackState(this));
        states.Add(EnermyStateType.Hit, new HitState(this));
        states.Add(EnermyStateType.Death, new DeathState(this));

        TransitionState(EnermyStateType.Idle);

        layerMask = gameObject.layer;
        parameter.moveFrameInput = Vector2.zero;

        // ��ȡ��ײ���С
        BoxCollider2D box = origin.GetComponent<BoxCollider2D>();
        if (box != null)
            originSize = box.size;
        else
            originSize = Vector2.one;

        drawPos = transform.position;
        temp_parameter = CopyParameter(parameter);

        // ע�ᵽGameReferee��ȷ��ʵ������
        if (GameReferee.instance != null)
            GameReferee.instance.RegisterFSM(origin, this);
    }

    private Parameter CopyParameter(Parameter original)
    {
        return new Parameter()
        {
            health = original.health,
            moveSpeed = original.moveSpeed,
            chaseSpeed = original.chaseSpeed,
            idleTime = original.idleTime,
            patrolPoints = original.patrolPoints != null && original.patrolPoints.Length >= 2 ?
                new Vector3[] { original.patrolPoints[0], original.patrolPoints[1] } : new Vector3[2],
            chasePoints = original.chasePoints != null && original.chasePoints.Length >= 2 ?
                new Vector3[] { original.chasePoints[0], original.chasePoints[1] } : new Vector3[2],
            target = null,
            targetLayer = original.targetLayer,
            attackPoint = original.attackPoint,
            attackArea = original.attackArea,
            viewArea = original.viewArea,
            moveFrameInput = Vector2.zero,
            animator = original.animator,
            rb = original.rb,
            getHit = false,
            isDead = original.isDead,
        };
    }

    void OnEnable()
    {
        EventDispatcher.AddObserver(this, MessageEvent.AgainGame, OnAgainGame, null);
    }

    void OnDestroy()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.AgainGame, null);
        if (GameReferee.instance != null)
            GameReferee.instance.UnRegisterFSM(origin);
    }

    void Update()
    {
        startInColliders = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = false;

        if (_sprite != null)
            parameter.curFlipX = _sprite.flipX;

        if (currentState != null)
            currentState.OnUpdate();

        originGroundHit = Physics2D.BoxCast(origin.position, originSize, 0, Vector2.down, .1f, ~layerMask);
        if (originGroundHit.collider == null || (originGroundHit.collider.isTrigger && originGroundHit.collider.transform != transform && originGroundHit.collider.transform != origin))
        {
            parameter.moveFrameInput = Vector2.MoveTowards(new Vector2(parameter.moveFrameInput.x * .7f, parameter.moveFrameInput.y), new Vector2(0, -100), 8 * Time.deltaTime);
        }
        else
        {
            parameter.moveFrameInput = new Vector2(parameter.moveFrameInput.x, 0);
        }
        Physics2D.queriesStartInColliders = startInColliders;
    }

    void FixedUpdate()
    {
        if (parameter.rb != null)
            parameter.rb.velocity = parameter.moveFrameInput;
    }

    public string GetStateAnimName(EnermyStateType type)
    {
        for (int i = 0; i < state_anim.Count; i++)
        {
            if (state_anim[i].stateType == type)
            {
                return state_anim[i].animName;
            }
        }
        return string.Empty;
    }

    public void TransitionState(EnermyStateType type)
    {
        if (parameter.isDead) return;
        if (currentState != null)
            currentState.OnExist();
        currentState = states[type];
        currentState.OnEnter();
    }

    public void FlipTo(Vector3 target)
    {
        if (transform.position.x > target.x)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (transform.position.x < target.x)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
    }

    public int GetPositiveDirect(float v, float targetV)
    {
        return v < targetV ? 1 : -1;
    }

    public void OnHit()
    {
        parameter.getHit = true;
    }

    private void OnCollisionEnter2D(Collision2D c)
    {
        if (parameter.isDead) return;
        if (c.collider.CompareTag("Player") || c.collider.CompareTag("NetPlayer"))
        {
            EventDispatcher.PostEvent(MessageEvent.PlayerOnHit, this, GameManager.Instance.userName, false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("NetPlayer"))
        {
            parameter.target = other.transform;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("NetPlayer"))
        {
            parameter.target = null;
        }
    }

    private void OnDrawGizmos()
    {
        if (parameter.attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(parameter.attackPoint.position, parameter.attackArea);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.position, parameter.viewArea);
    }

    private bool OnAgainGame(params object[] args)
    {
        // 只重置战斗状态，保留target等运行时引用
        parameter.moveFrameInput = Vector2.zero;
        parameter.getHit = false;
        parameter.isDead = false;
        Parameter originalSnapshot = CopyParameter(parameter);
        // 从快照恢复生命值和速度等数值字段，但保留target
        parameter.health = originalSnapshot.health;
        parameter.moveSpeed = originalSnapshot.moveSpeed;
        parameter.chaseSpeed = originalSnapshot.chaseSpeed;
        parameter.idleTime = originalSnapshot.idleTime;
        parameter.patrolPoints = originalSnapshot.patrolPoints;
        parameter.chasePoints = originalSnapshot.chasePoints;
        temp_parameter = CopyParameter(parameter);
        gameObject.SetActive(true);
        TransitionState(EnermyStateType.Idle);
        transform.position = drawPos;

        if (_sprite != null)
            _sprite.flipX = isFlipX;

        if (parameter.rb != null)
            parameter.rb.simulated = true;

        return false;
    }

    public void TakeDamage(DamageData damage)
    {
        parameter.getHit = true;
    }

    public bool IsAlive()
    {
        return !parameter.isDead;
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }
}