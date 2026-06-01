using UnityEngine;

public class Tengman : BaseMechanism
{
    [SerializeField] private float slideSpeed = 1f; // 藤蔓上的下滑速度
    [SerializeField] private float jumpBoostMultiplier = 1.2f; // 从藤蔓跳跃的加成系数

    protected override void Start()
    {
        BoxCollider2D c = GetComponent<BoxCollider2D>();
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        c.offset = new Vector2(0, -sprite.size.y / 2f);
        c.size = sprite.size;
        base.Start();
    }

    public override void TriggerEnter(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.OnVineEnter(this);
            }
        }
    }

    public override void TriggerStay(Collider2D c)
    {
        if (c.CompareTag("Player"))
        {
            PlayerController player = c.GetComponent<PlayerController>();
            if (player != null)
            {
                player.OnVineStay(this);
            }
        }
    }

    public override void TriggerExit(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.OnVineExit();
            }
        }
    }

    public float GetSlideSpeed()
    {
        return slideSpeed;
    }

    public float GetJumpBoost()
    {
        return jumpBoostMultiplier;
    }
}