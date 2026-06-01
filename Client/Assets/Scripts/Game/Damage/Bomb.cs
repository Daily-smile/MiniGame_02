using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

public class Bomb : MonoBehaviour
{
    [Header("��ը�뾶")]
    public float explosionRadius = 1f;
    [Header("���˺�����Ĳ㼶")]
    public LayerMask damageableLayer;

    Rigidbody2D rb;
    Animator animator;
    Coroutine playBombAnim;
    SpriteRenderer timerSprite;
    Sprite[] numbers;
    Tilemap destructibleTilemap;
    Tilemap decorationTilemap;
    Transform centerPoint;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        Transform timerTran = transform.Find("timer");
        if (timerTran != null) timerSprite = timerTran.GetComponent<SpriteRenderer>();
        SpriteAtlas atlas = ResourceManager.Instance.LoadAsset<SpriteAtlas>("Atlas/GameScene");
        if (atlas != null)
        {
            numbers = new Sprite[3];
            numbers[0] = atlas.GetSprite("hud_character_1");
            numbers[1] = atlas.GetSprite("hud_character_2");
            numbers[2] = atlas.GetSprite("hud_character_3");
        }

        centerPoint = transform.Find("centerPoint");
        if (GameManager.Instance.gameModel != GameManager.GameModel.Infinity)
        {
            // ���ҿɱ���ը�����Tilemap
            GameObject grid = GameObject.Find("Environment/Grid");
            if (grid != null)
            {
                Transform platformTran = grid.transform.Find("Platform");
                if (platformTran != null) destructibleTilemap = platformTran.GetComponent<Tilemap>();
                Transform decoTran = grid.transform.Find("Decoration");
                if (decoTran != null) decorationTilemap = decoTran.GetComponent<Tilemap>();
                playBombAnim = StartCoroutine(PlayAnim());
            }
        }
        else
        {
            playBombAnim = StartCoroutine(PlayAnim());
        }
    }

    private void OnDisable()
    {
        if (playBombAnim != null)
        {
            StopCoroutine(playBombAnim);
            playBombAnim = null;
        }
    }

    IEnumerator PlayAnim()
    {
        timerSprite.gameObject.SetActive(true);
        int timer = numbers.Length - 1;
        while (true)
        {
            if (timer < 0)
            {
                timerSprite.gameObject.SetActive(false);
                break;
            }
            timerSprite.sprite = numbers[timer];
            timer--;
            yield return new WaitForSeconds(1f);
        }
        CameraController.instance.TranslateCameraEffect(CameraEffect.Shake);
        animator.SetTrigger("start");
        while (true)
        {
            AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
            yield return new WaitForSeconds(animInfo.length / 2);

            DamageEntitiesInRadius();
            if (GameManager.Instance.gameModel != GameManager.GameModel.Infinity)
            {
                // ��ⱬը��Χ�ڵķ���
                Vector3Int bombCell = destructibleTilemap.WorldToCell(centerPoint.position);
                int radiusInCells = Mathf.CeilToInt(explosionRadius / destructibleTilemap.cellSize.x);

                // ����Բ�������ڵ����и���
                for (int x = -radiusInCells; x <= radiusInCells; x++)
                {
                    for (int y = -radiusInCells; y <= radiusInCells; y++)
                    {
                        // ���㵱ǰ�����뱬ը���ĵľ���
                        float distance = Vector2.Distance(Vector2.zero, new Vector2(x, y));

                        // ��������ڱ�ը�뾶��
                        if (distance <= radiusInCells)
                        {
                            Vector3Int cellPosition = new Vector3Int(bombCell.x + x, bombCell.y + y, bombCell.z);

                            // ����λ���Ƿ��п��ƻ��ķ���
                            TileBase tile1 = destructibleTilemap.GetTile(cellPosition);
                            if (tile1 != null)
                            {
                                // �������
                                destructibleTilemap.SetTile(cellPosition, null);
                                GameReferee.instance.AddOneCleanupTile(cellPosition, destructibleTilemap, tile1);
                                Vector3Int cellUpPosition = new Vector3Int(cellPosition.x, cellPosition.y + 1, cellPosition.z);
                                TileBase tile2 = decorationTilemap.GetTile(cellUpPosition);
                                if (tile2 != null)
                                {
                                    decorationTilemap.SetTile(cellUpPosition, null);
                                    GameReferee.instance.AddOneCleanupTile(cellUpPosition, decorationTilemap, tile2);
                                }
                            }
                            TileBase tile3 = decorationTilemap.GetTile(cellPosition);
                            if (decorationTilemap.GetTile(cellPosition) != null)
                            {
                                decorationTilemap.SetTile(cellPosition, null);
                                GameReferee.instance.AddOneCleanupTile(cellPosition, decorationTilemap, tile3);
                            }
                        }
                    }
                }
            }
            yield return new WaitForSeconds(animInfo.length / 2);
            transform.Find("sprite").gameObject.SetActive(false);
            break;
        }

        playBombAnim = null;
        GameObject.Destroy(gameObject);
    }

    void DamageEntitiesInRadius()
    {
        // ʹ��OverlapCircleAll��ⱬը��Χ�ڵ�������ײ��
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(centerPoint.position, explosionRadius, damageableLayer);

        foreach (Collider2D collider in hitColliders)
        {
            // ��ȡ���˺����
            IDamageable damageable = collider.GetComponent<IDamageable>();

            if (damageable != null && damageable.IsAlive())
            {
                // �����˺�˥��������ԽԶ�˺�Խ�ͣ�
                //float distance = Vector2.Distance(centerPoint.position, collider.transform.position);
                //float damageMultiplier = 1 - Mathf.Clamp01(distance / explosionRadius);
                //int calculatedDamage = Mathf.RoundToInt(damage * damageMultiplier);

                DamageData damage = new DamageData(1, centerPoint.position, DamageType.Bomb);
                // Ӧ���˺�
                damageable.TakeDamage(damage);
                // ����Ч��
                //ApplyKnockback(collider.attachedRigidbody, collider.transform.position);
            }
        }
    }

    void ApplyKnockback(Rigidbody2D rb, Vector2 targetPosition)
    {
        if (rb != null)
        {
            // ������˷���
            Vector2 knockbackDirection = (targetPosition - (Vector2)centerPoint.position).normalized;

            // Ӧ�û�����������Խ��������Խ��
            float distance = Vector2.Distance(centerPoint.position, targetPosition);
            float forceMultiplier = 1 - Mathf.Clamp01(distance / explosionRadius);
            float knockbackForce = 10f * forceMultiplier;

            rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);
        }
    }

    private void OnTriggerEnter2D(Collider2D c)
    {
        if (!c.isTrigger && !c.CompareTag("Player") && !c.CompareTag("NetPlayer"))
        {
            rb.simulated = false;
        }
    }
}
