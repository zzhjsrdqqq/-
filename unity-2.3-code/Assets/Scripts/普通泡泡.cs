using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class Bubble : MonoBehaviour
{
    [Header("吸附设置")]
    [Tooltip("开启后，普通泡泡会主动向玩家移动，而不是硬控玩家")]
    public bool useAttract = true;

    [Tooltip("泡泡飞向玩家的速度")]
    public float attractSpeed = 18f;

    [Tooltip("泡泡距离玩家多近时算触发成功")]
    public float attractStopDistance = 0.12f;

    [Tooltip("泡泡最多追玩家多久，防止异常卡住")]
    public float maxAttractTime = 0.25f;

    [Header("触发设置")]
    [Tooltip("触发后是否刷新玩家跳跃和冲刺")]
    public bool refreshDashAndJump = true;

    [Header("是否使用后重生")]
    public bool respawnBubble = true;

    [Header("重生时间")]
    public float respawnTime = 2f;

    private Collider2D col;
    private SpriteRenderer sr;

    private bool isUsed = false;

    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private Vector3 startLocalScale;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        if (col != null)
            col.isTrigger = true;

        SaveStartTransform();
    }

    private void SaveStartTransform()
    {
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
        startLocalScale = transform.localScale;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryActivate(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryActivate(other);
    }

    private void TryActivate(Collider2D other)
    {
        if (isUsed) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        PlayerTransportResponder transport = other.GetComponentInParent<PlayerTransportResponder>();
        if (transport != null && transport.IsTransporting) return;

        isUsed = true;
        StartCoroutine(ActivateRoutine(other, player));
    }

    private IEnumerator ActivateRoutine(Collider2D other, PlayerController player)
    {
        Transform playerTf = player.transform;

        if (useAttract && playerTf != null)
        {
            float timer = 0f;

            while (timer < maxAttractTime && playerTf != null)
            {
                float distance = Vector2.Distance(transform.position, playerTf.position);

                if (distance <= attractStopDistance)
                    break;

                transform.position = Vector2.MoveTowards(
                    transform.position,
                    playerTf.position,
                    attractSpeed * Time.deltaTime
                );

                timer += Time.deltaTime;
                yield return null;
            }
        }

        BubbleTimeBonusUtility.TryGiveTime(this, other);

        if (refreshDashAndJump && player != null)
            player.RefreshDashAndJump();

        PlayerJuiceEffects juice = other.GetComponentInParent<PlayerJuiceEffects>();
        if (juice != null)
            juice.PlayNormalBubbleJuice();

        if (respawnBubble)
            StartCoroutine(RespawnRoutine());
        else
            Destroy(gameObject);
    }

    private IEnumerator RespawnRoutine()
    {
        if (col != null) col.enabled = false;
        if (sr != null) sr.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        transform.localPosition = startLocalPosition;
        transform.localRotation = startLocalRotation;
        transform.localScale = startLocalScale;

        if (col != null) col.enabled = true;
        if (sr != null) sr.enabled = true;

        isUsed = false;
    }
}