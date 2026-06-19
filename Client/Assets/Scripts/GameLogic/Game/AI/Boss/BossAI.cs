using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class BossAI : MonoBehaviour, IDamageable
{
    [Header("状态机")]
    public BossStateMachine stateMachine;

    [Header("生命属性")]
    public int maxHealth = 1000;
    public int currentHealth;
    public HealthBar healthBar;

    [Header("移动参数")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float patrolRange = 10f;
    public float chaseRange = 15f;
    public float attackRange = 3f;

    [Header("攻击伤害")]
    public int strikeDamage = 20;
    public int attackDamage = 30;
    public int jumpAttackDamage = 40;
    public int flyKickDamage = 35;
    public int crouchAttackDamage = 25;

    [Header("冷却时间")]
    public float strikeCooldown = 3f;
    public float attackCooldown = 5f;
    public float jumpCooldown = 8f;
    public float flyKickCooldown = 10f;
    public float crouchCooldown = 6f;

    [Header("阶段阈值")]
    public int phase2HealthThreshold = 700;
    public int phase3HealthThreshold = 400;
    public int phase4HealthThreshold = 200;

    // 内部变量
    [HideInInspector] public Transform player;
    [HideInInspector] public Animator animator;
    [HideInInspector] public Rigidbody2D rb;
    [HideInInspector] public Collider2D bossCollider;
    [HideInInspector] public bool facingRight = true;

    // 计时器
    [HideInInspector] public float strikeTimer = 0f;
    [HideInInspector] public float attackTimer = 0f;
    [HideInInspector] public float jumpTimer = 0f;
    [HideInInspector] public float flyKickTimer = 0f;
    [HideInInspector] public float crouchTimer = 0f;
    [HideInInspector] public float dizzyTimer = 0f;

    // 阶段管理
    [HideInInspector] public int currentPhase = 1;

    void Awake()
    {
        EventDispatcher.AddObserver(this, MessageEvent.OnGameUILoad, OnGameUILoad, null);
    }

    void OnDisable()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.OnGameUILoad, null);
    }

    void OnDestroy()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.OnGameUILoad, null);
    }

    private bool OnGameUILoad(params object[] args)
    {
        healthBar = (HealthBar)args[0];
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(maxHealth);
            healthBar.SetHealth(currentHealth);
            healthBar.gameObject.SetActive(true);
        }
        return false;
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        bossCollider = GetComponent<Collider2D>();

        currentHealth = maxHealth;

        if (healthBar != null)
        {
            healthBar.SetMaxHealth(maxHealth);
            healthBar.SetHealth(currentHealth);
            healthBar.gameObject.SetActive(true);
        }
        // 初始化状态机
        stateMachine = new BossStateMachine();

        // 注册所有状态
        stateMachine.AddState(typeof(IdleBossState), new IdleBossState(this));
        stateMachine.AddState(typeof(PatrolBossState), new PatrolBossState(this));
        stateMachine.AddState(typeof(ChaseBossState), new ChaseBossState(this));
        stateMachine.AddState(typeof(StrikeBossState), new StrikeBossState(this));
        stateMachine.AddState(typeof(AttackBossState), new AttackBossState(this));
        stateMachine.AddState(typeof(JumpBossState), new JumpBossState(this));
        stateMachine.AddState(typeof(JumpAttackBossState), new JumpAttackBossState(this));
        stateMachine.AddState(typeof(HurtBossState), new HurtBossState(this));
        stateMachine.AddState(typeof(FlyKickBossState), new FlyKickBossState(this));
        stateMachine.AddState(typeof(DizzyBossState), new DizzyBossState(this));
        stateMachine.AddState(typeof(CrouchBossState), new CrouchBossState(this));
        stateMachine.AddState(typeof(CrouchAttackBossState), new CrouchAttackBossState(this));
        stateMachine.AddState(typeof(DeathBossState), new DeathBossState(this));

        // 初始状态设为巡逻
        stateMachine.ChangeState(typeof(PatrolBossState));
    }

    void Update()
    {
        // 更新所有计时器
        UpdateTimers();

        // 更新状态机
        stateMachine.Update();
    }

    void UpdateTimers()
    {
        if (strikeTimer > 0) strikeTimer -= Time.deltaTime;
        if (attackTimer > 0) attackTimer -= Time.deltaTime;
        if (jumpTimer > 0) jumpTimer -= Time.deltaTime;
        if (flyKickTimer > 0) flyKickTimer -= Time.deltaTime;
        if (crouchTimer > 0) crouchTimer -= Time.deltaTime;
        if (dizzyTimer > 0) dizzyTimer -= Time.deltaTime;
    }

    public void TakeDamage(int damage, Vector2 damageSource)
    {
        if (stateMachine.GetCurrentStateType() == typeof(DeathBossState))
            return;

        currentHealth -= damage;

        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth);
        }

        // 检查阶段转换
        CheckPhaseTransition();

        // 进入受伤状态
        stateMachine.ChangeState(typeof(HurtBossState));

        // 击退效果
        Vector2 knockbackDirection = (transform.position - (Vector3)damageSource).normalized;
        rb.AddForce(knockbackDirection * 5f, ForceMode2D.Impulse);

        // 有一定概率进入眩晕状态
        if (Random.Range(0, 100) < 20) // 20%概率进入眩晕
        {
            dizzyTimer = 3f;
        }

        // 检查是否死亡
        if (currentHealth <= 0)
        {
            stateMachine.ChangeState(typeof(DeathBossState));
        }
    }

    void CheckPhaseTransition()
    {
        int oldPhase = currentPhase;

        if (currentHealth <= phase4HealthThreshold) currentPhase = 4;
        else if (currentHealth <= phase3HealthThreshold) currentPhase = 3;
        else if (currentHealth <= phase2HealthThreshold) currentPhase = 2;
        else currentPhase = 1;

        // 阶段提升时增强攻击性
        if (currentPhase > oldPhase)
        {
            // 减少冷却时间，增加攻击频率
            strikeCooldown *= 0.8f;
            attackCooldown *= 0.8f;
            jumpCooldown *= 0.8f;
            flyKickCooldown *= 0.8f;

            // 进入狂暴状态
            StartCoroutine(RageMode());
        }
    }

    IEnumerator RageMode()
    {
        // 狂暴状态效果（变红）
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) yield break;
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.red;

        yield return new WaitForSeconds(2f);

        spriteRenderer.color = originalColor;
    }

    public void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    public bool IsGrounded()
    {
        // 检测是否在地面上
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.1f);
        return hit.collider != null && hit.collider.gameObject != gameObject;
    }

    // 攻击动画事件回调
    public void OnAttackAnimationEnd()
    {
        System.Type currentState = stateMachine.GetCurrentStateType();

        if (currentState == typeof(StrikeBossState) ||
            currentState == typeof(AttackBossState) ||
            currentState == typeof(JumpAttackBossState) ||
            currentState == typeof(FlyKickBossState) ||
            currentState == typeof(CrouchAttackBossState))
        {
            stateMachine.ChangeState(typeof(ChaseBossState));
        }
    }

    // 伤害检测
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("NetPlayer"))
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                int damage = 0;

                System.Type currentState = stateMachine.GetCurrentStateType();

                if (currentState == typeof(StrikeBossState)) damage = strikeDamage;
                else if (currentState == typeof(AttackBossState)) damage = attackDamage;
                else if (currentState == typeof(JumpAttackBossState)) damage = jumpAttackDamage;
                else if (currentState == typeof(FlyKickBossState)) damage = flyKickDamage;
                else if (currentState == typeof(CrouchAttackBossState)) damage = crouchAttackDamage;

                if (damage > 0)
                {
                    DamageData damageData = new DamageData(damage, transform.position, DamageType.Boss);
                    damageable.TakeDamage(damageData);
                }
            }
        }
    }

    public void TakeDamage(DamageData damage)
    {
        TakeDamage(damage.damageAmount, damage.damageSource);
    }

    public bool IsAlive()
    {
        return currentHealth > 0;
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }
}

// Boss状态枚举
public enum BossStateType
{
    Idle,
    Patrol,
    Chase,
    Strike,
    Attack,
    Jump,
    JumpAttack,
    Hurt,
    FlyKick,
    Dizzy,
    Crouch,
    CrouchAttack,
    Die,
    Win
}
}