using UnityEngine;

/// <summary>
/// 缩小平台 - 玩家站上去后慢慢变窄
/// 离开后慢慢恢复原始大小
/// </summary>
public class ShrinkingPlatform : MonoBehaviour
{
    public float shrinkSpeed = 0.8f;       // 每秒缩小多少
    public float minWidth = 0.3f;          // 最小宽度（不会完全消失）
    public float recoverSpeed = 0.5f;      // 每秒恢复多少
    public float recoverDelay = 1.0f;      // 离开后多久开始恢复

    private Vector3 originalScale;
    private float currentWidth;
    private bool playerOnTop = false;
    private float recoverTimer = 0f;
    private SpriteRenderer sr;
    private Color originalColor;

    void Start()
    {
        originalScale = transform.localScale;
        currentWidth = originalScale.x;
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalColor = sr.color;
    }

    void Update()
    {
        if (playerOnTop)
        {
            // 持续缩小
            currentWidth -= shrinkSpeed * Time.deltaTime;
            currentWidth = Mathf.Max(currentWidth, minWidth);
            recoverTimer = 0f;

            // 颜色随缩小程度变红警告
            if (sr != null)
            {
                float t = 1f - (currentWidth - minWidth) / (originalScale.x - minWidth);
                sr.color = Color.Lerp(originalColor, new Color(1f, 0.3f, 0.2f), t * 0.6f);
            }
        }
        else
        {
            // 离开后延迟恢复
            recoverTimer += Time.deltaTime;
            if (recoverTimer >= recoverDelay && currentWidth < originalScale.x)
            {
                currentWidth += recoverSpeed * Time.deltaTime;
                currentWidth = Mathf.Min(currentWidth, originalScale.x);

                // 颜色恢复
                if (sr != null)
                {
                    float t = 1f - (currentWidth - minWidth) / (originalScale.x - minWidth);
                    sr.color = Color.Lerp(originalColor, new Color(1f, 0.3f, 0.2f), t * 0.6f);
                }
            }
        }

        // 更新平台宽度
        transform.localScale = new Vector3(currentWidth, originalScale.y, originalScale.z);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y < -0.5f)
                {
                    playerOnTop = true;
                    break;
                }
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            playerOnTop = false;
    }
}
