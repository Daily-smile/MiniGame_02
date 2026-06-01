using UnityEngine;

public class AttackState : IState
{
    private FSM manager;

    private AnimatorStateInfo info;
    private string anim_name;
    private string anim_attack_name;
    private Transform attackR;
    private Transform attackL;
    private GameObject attackPrefab;
    private SpriteRenderer attackSprite;
    private Animator _anim;

    public AttackState(FSM manager)
    {
        this.manager = manager;
        this.anim_name = manager.GetStateAnimName(EnermyStateType.Attack);
        attackR = manager.transform.Find("AttackR");
        attackL = manager.transform.Find("AttackL");
        anim_attack_name = "half_moon_slash";
    }

    public void OnEnter()
    {
        manager.parameter.moveFrameInput.x = 0;
        if (attackPrefab == null)
        {
            GameObject prefab = ResourceManager.Instance.LoadAsset<GameObject>("Attacks/half_moon_slash");
            attackPrefab = GameObject.Instantiate(prefab);
            attackPrefab.name = prefab.name;
            attackSprite = attackPrefab.GetComponent<SpriteRenderer>();
            _anim = attackPrefab.GetComponent<Animator>();
        }
        attackPrefab.gameObject.SetActive(true);
        manager.parameter.animator.Play(anim_name);
        _anim.Play(anim_attack_name, 0, 0);
    }

    public void OnExist()
    {
        attackPrefab.gameObject.SetActive(false);
    }

    public void OnUpdate()
    {
        if (manager.parameter.getHit)
        {
            manager.TransitionState(EnermyStateType.Hit);
        }
        attackPrefab.transform.parent = manager.parameter.curFlipX ? attackR : attackL;
        attackPrefab.transform.localPosition = Vector3.zero;
        attackPrefab.transform.localScale = Vector3.one;
        attackSprite.flipX = !manager.parameter.curFlipX;
        info = _anim.GetCurrentAnimatorStateInfo(0);

        if (info.normalizedTime >= .95f)
        {
            manager.TransitionState(EnermyStateType.Chase);
        }
    }
}