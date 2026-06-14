using DG.Tweening;
using System;
using UnityEngine;

/// <summary>
/// VERY primitive animator example.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    /// <summary>
    /// 是否由本地玩家控制。由 MirrorPlayer 设置：远程玩家 = false
    /// </summary>
    [HideInInspector] public bool isLocalPlayer = true;

    [Header("References")]
    [SerializeField]
    private Animator _anim;

    [SerializeField] private SpriteRenderer _sprite;

    [Header("Settings")]
    [SerializeField] private float _maxTilt = 5;
    [SerializeField] private float _tiltSpeed = 20;

    [Header("Audio Clips")]
    [SerializeField]
    private AudioClip[] _footsteps;
    [SerializeField]
    private AudioClip _jump_audio;

    public AudioClip[] Footsteps
    {
        get { return _footsteps; }
    }
    public AudioClip JumpAudio
    {
        get { return _jump_audio; }
    }

    private AudioSource _source;
    private IPlayerController _player;
    private Sequence _sequence;
    private bool _grounded;
    private Transform fireOriginR;
    private Transform fireOriginL;

    private static readonly int IsGroundKey = Animator.StringToHash("IsGround");
    private static readonly int SpeedKey = Animator.StringToHash("Speed");
    private static readonly int JumpKey = Animator.StringToHash("Jump");
    private static readonly int HitKey = Animator.StringToHash("Hit");
    private static readonly int DeadKey = Animator.StringToHash("Dead");

    private MirrorPlayer _mirrorPlayer;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _player = GetComponentInParent<IPlayerController>();
        _mirrorPlayer = GetComponentInParent<MirrorPlayer>();
        Transform parent = transform.parent;
        if (parent != null)
        {
            fireOriginR = parent.Find("FirePosR");
            fireOriginL = parent.Find("FirePosL");
        }
        // 诊断日志：记录 _sprite 引用指向哪个对象
        //Debug.Log($"[PlayerAnimator] Awake: GameObject={gameObject.name}, _sprite指向={(_sprite != null ? _sprite.gameObject.name : "NULL")}, _sprite所在根对象={(_sprite != null ? _sprite.transform.root.name : "NULL")}, isLocalPlayer默认={isLocalPlayer}");
    }

    private void OnEnable()
    {
        EventDispatcher.AddObserver(this, MessageEvent.AgainGame, OnAgainGame, null);
        if (_player != null)
        {
            _player.Jumped += OnJumped;
            _player.Fire += OnFire;
            _player.GroundedChanged += OnGroundedChanged;
        }
    }

    private void OnDisable()
    {
        OnDispose();
    }

    private void OnDestroy()
    {
        OnDispose();
    }

    private void OnDispose()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.AgainGame, null);
        KillPlayerInvincibleAnim();
        if (_player != null)
        {
            _player.Jumped -= OnJumped;
            _player.Fire -= OnFire;
            _player.GroundedChanged -= OnGroundedChanged;
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || _player == null) return;

        HandleSpriteFlip();
        HandleIdleSpeed();
        HandleCharacterTilt();
    }

    private void HandleSpriteFlip()
    {
        if (_player.FrameInput.x != 0)
        {
            bool newFlipX = _player.FrameInput.x < 0;
            //if (_sprite.flipX != newFlipX)
            //{
            //    Debug.Log($"[PlayerAnimator] HandleSpriteFlip: GameObject={gameObject.name}, isLocalPlayer={isLocalPlayer}, hasAuthority={_mirrorPlayer != null && _mirrorPlayer.authority}, FrameInput.x={_player.FrameInput.x}, oldFlipX={_sprite.flipX}, newFlipX={newFlipX}");
            //}
            _sprite.flipX = newFlipX;
        }
    }

    private void HandleIdleSpeed()
    {
        float inputStrength = Mathf.Abs(_player.FrameInput.x);
        _anim.SetFloat(SpeedKey, inputStrength);
    }

    private void HandleCharacterTilt()
    {
        var runningTilt = _grounded ? Quaternion.Euler(0, 0, _maxTilt * _player.FrameInput.x) : Quaternion.identity;
        _anim.transform.up = Vector3.RotateTowards(_anim.transform.up, runningTilt * Vector2.up, _tiltSpeed * Time.deltaTime, 0f);
    }

    private void OnJumped()
    {
        _anim.SetTrigger(JumpKey);
        if (_source != null && _jump_audio != null)
            _source.PlayOneShot(_jump_audio);
        //_anim.ResetTrigger(GroundedKey);
        //if (_grounded) 
        //{
            
        //}
    }

    private void OnFire()
    {
        Transform origin = _sprite.flipX ? fireOriginL : fireOriginR;
        if (origin == null) return;

        // 在线模式：通过 MirrorPlayer 的 Command 同步到所有客户端
        // 本地立即生成（无延迟），RpcOnFire 设置为 includeOwner=false 避免重复
        if (_mirrorPlayer != null && _mirrorPlayer.playerName == GameManager.Instance.userName
            && CustomNetworkManager.singleton != null && CustomNetworkManager.singleton.IsConnectedToServer())
        {
            _mirrorPlayer.CmdFire(origin.position, !_sprite.flipX);
        }

        // 本地生成火球（在线和离线模式下都需要）
        GameObject fire = ObjectPool.instance.GetInPool("Fireball");
        if (fire == null)
        {
            GameObject prefab = ResourceManager.Instance.LoadAsset<GameObject>("Bullets_Fireball");
            if (prefab == null) return;
            fire = GameObject.Instantiate(prefab);
            fire.name = "Fireball";
        }
        fire.transform.parent = null;
        fire.transform.position = origin.position;
        Fireball fb = fire.GetComponent<Fireball>();
        if (fb != null) fb.Initialized(!_sprite.flipX);
    }

    private void OnGroundedChanged(bool grounded, float impact)
    {
        _grounded = grounded;

        if (grounded)
        {
            _anim.SetBool(IsGroundKey, true);
            _source.PlayOneShot(_footsteps[UnityEngine.Random.Range(0, _footsteps.Length)]);
        }
        else
        {
            _anim.SetBool(IsGroundKey, false);
        }
    }

    private bool OnAgainGame(params object[] args)
    {
        KillPlayerInvincibleAnim();
        _sprite.color = Color.white;
        return false;
    }

    private void KillPlayerInvincibleAnim()
    {
        // 移除玩家受击无敌动画，确保在再次受击时能正确播放闪烁效果
        if (_sequence != null && _sequence.IsActive())
        {
            _sequence.Kill();
        }
        _sequence = null;
    }

    public void OnHit(bool isDead, Action invincibleEvent)
    {
        if (isDead)
        {
            _anim.SetTrigger(DeadKey);
        }
        else
        {
            _anim.SetTrigger(HitKey);
        }

        // 统一播放受击闪烁效果（致死伤害也需要闪烁）
        KillPlayerInvincibleAnim();
        _sprite.color = Color.white;
        if (isDead)
        {
            invincibleEvent();
            return;
        }
        _sequence = DOTween.Sequence();
        _sequence.Append(_sprite.DOBlendableColor(new Color(1, 1, 1, .2f), .5f));
        _sequence.Insert(.5f, _sprite.DOBlendableColor(Color.white, .5f));
        _sequence.SetLoops(3).OnComplete(() => {
            _sprite.color = Color.white;
            invincibleEvent();
            _sequence.Kill();
            _sequence = null;
        }).Play();
    }

    public void OnResurrect()
    {
        _anim.Play("beige_idle");
    }
}