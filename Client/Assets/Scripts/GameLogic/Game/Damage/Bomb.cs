using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class Bomb : MonoBehaviour
{
    [Header("爆炸半径")]
    public float explosionRadius = 1f;
    [Header("受伤害的层级")]
    public LayerMask damageableLayer;

    Rigidbody2D rb;
    Animator animator;
    Coroutine playBombAnim;
    SpriteRenderer timerSprite;
    Sprite[] numbers;
    private static SpriteAtlas _cachedAtlas;
    private static Sprite[] _cachedNumbers;
    Tilemap destructibleTilemap;
    Tilemap decorationTilemap;
    Transform centerPoint;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        Transform timerTran = transform.Find("timer");
        if (timerTran != null) timerSprite = timerTran.GetComponent<SpriteRenderer>();
        if (_cachedAtlas == null)
        {
            _cachedAtlas = ResourceManager.Instance.LoadAsset<SpriteAtlas>("Atlas_GameScene");
            if (_cachedAtlas != null)
            {
                _cachedNumbers = new Sprite[3];
                _cachedNumbers[0] = _cachedAtlas.GetSprite("hud_character_1");
                _cachedNumbers[1] = _cachedAtlas.GetSprite("hud_character_2");
                _cachedNumbers[2] = _cachedAtlas.GetSprite("hud_character_3");
            }
        }
        numbers = _cachedNumbers;

        centerPoint = transform.Find("centerPoint");
        if (GameManager.Instance.gameModel != GameManager.GameModel.Infinity)
        {
            // 查找可被爆炸破坏的Tilemap
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
                // 检测爆炸范围内的方块
                Vector3Int bombCell = destructibleTilemap.WorldToCell(centerPoint.position);
                int radiusInCells = Mathf.CeilToInt(explosionRadius / destructibleTilemap.cellSize.x);

                // 遍历圆形区域内的所有格子
                for (int x = -radiusInCells; x <= radiusInCells; x++)
                {
                    for (int y = -radiusInCells; y <= radiusInCells; y++)
                    {
                        // 计算当前格子与爆炸中心的距离
                        float distance = Vector2.Distance(Vector2.zero, new Vector2(x, y));

                        // 如果格子在爆炸半径内
                        if (distance <= radiusInCells)
                        {
                            Vector3Int cellPosition = new Vector3Int(bombCell.x + x, bombCell.y + y, bombCell.z);

                            // 检查该位置是否有可破坏的方块
                            TileBase tile1 = destructibleTilemap.GetTile(cellPosition);
                            if (tile1 != null)
                            {
                                // 移除方块
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
        // 使用OverlapCircleAll检测爆炸范围内的所有碰撞体
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(centerPoint.position, explosionRadius, damageableLayer);

        foreach (Collider2D collider in hitColliders)
        {
            // 获取受伤害接口
            IDamageable damageable = collider.GetComponent<IDamageable>();

            if (damageable != null && damageable.IsAlive())
            {
                // 计算伤害衰减（距离越远伤害越低）
                //float distance = Vector2.Distance(centerPoint.position, collider.transform.position);
                //float damageMultiplier = 1 - Mathf.Clamp01(distance / explosionRadius);
                //int calculatedDamage = Mathf.RoundToInt(damage * damageMultiplier);

                DamageData damage = new DamageData(1, centerPoint.position, DamageType.Bomb);
                // 应用伤害
                damageable.TakeDamage(damage);
                // 击退效果
                //ApplyKnockback(collider.attachedRigidbody, collider.transform.position);
            }
        }
    }

    void ApplyKnockback(Rigidbody2D rb, Vector2 targetPosition)
    {
        if (rb != null)
        {
            // 计算击退方向
            Vector2 knockbackDirection = (targetPosition - (Vector2)centerPoint.position).normalized;

            // 应用击退力（距离越远力越小）
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
}
