using UnityEngine;
using System.Collections;

/// <summary>
/// 消失平台 - 接触后闪烁警告，然后消失，一段时间后恢复
/// </summary>
public class DisappearingPlatform : MonoBehaviour
{
    public enum TriggerDirection
    {
        TopOnly,        // 只允许从上往下踩触发
        BottomOnly,     // 只允许从下往上顶触发
        TopAndBottom,   // 上下都能触发
        AnyDirection    // 任意方向接触都触发
    }

    [Header("触发方向")]
    [Tooltip("选择平台被哪个方向接触时会消失")]
    public TriggerDirection triggerDirection = TriggerDirection.TopOnly;

    [Header("时间设置")]
    [Tooltip("触发后闪烁警告多久")]
    public float warningTime = 1.0f;

    [Tooltip("消失多久后恢复")]
    public float disappearTime = 3.0f;

    [Tooltip("闪烁速度")]
    public float flickerSpeed = 0.1f;

    [Header("触发对象")]
    [Tooltip("玩家 Tag")]
    public string playerTag = "Player";

    [Header("调试")]
    public bool debugLog = false;

    private SpriteRenderer[] renderers;
    private Collider2D[] colliders;

    private bool[] originalRendererEnabled;
    private Color[] originalRendererColors;

    private bool[] originalColliderEnabled;

    private bool isTriggered = false;

    void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);

        originalRendererEnabled = new bool[renderers.Length];
        originalRendererColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            originalRendererEnabled[i] = renderers[i].enabled;
            originalRendererColors[i] = renderers[i].color;
        }

        originalColliderEnabled = new bool[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
                continue;

            originalColliderEnabled[i] = colliders[i].enabled;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryTrigger(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryTrigger(collision);
    }

    private void TryTrigger(Collision2D collision)
    {
        if (isTriggered)
            return;

        if (collision == null)
            return;

        if (!collision.gameObject.CompareTag(playerTag))
            return;

        if (!IsValidTriggerDirection(collision))
            return;

        isTriggered = true;

        if (debugLog)
            Debug.Log($"消失平台触发：{gameObject.name}，方向 = {triggerDirection}");

        StartCoroutine(DisappearSequence());
    }

    private bool IsValidTriggerDirection(Collision2D collision)
    {
        if (triggerDirection == TriggerDirection.AnyDirection)
            return true;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            float normalY = contact.normal.y;

            switch (triggerDirection)
            {
                case TriggerDirection.TopOnly:
                    // 玩家从上往下踩平台
                    if (normalY < -0.5f)
                        return true;
                    break;

                case TriggerDirection.BottomOnly:
                    // 玩家从下往上顶平台
                    if (normalY > 0.5f)
                        return true;
                    break;

                case TriggerDirection.TopAndBottom:
                    // 上下都可以
                    if (Mathf.Abs(normalY) > 0.5f)
                        return true;
                    break;
            }
        }

        return false;
    }

    private IEnumerator DisappearSequence()
    {
        float elapsed = 0f;

        while (elapsed < warningTime)
        {
            SetRenderersVisible(false);
            yield return new WaitForSeconds(flickerSpeed);

            SetRenderersVisible(true);
            yield return new WaitForSeconds(flickerSpeed);

            elapsed += flickerSpeed * 2f;
        }

        SetRenderersVisible(false);
        SetCollidersEnabled(false);

        yield return new WaitForSeconds(disappearTime);

        RestorePlatform();

        isTriggered = false;
    }

    private void SetRenderersVisible(bool visible)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].enabled = visible;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
                continue;

            colliders[i].enabled = enabled;
        }
    }

    private void RestorePlatform()
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                renderers[i].enabled = originalRendererEnabled[i];
                renderers[i].color = originalRendererColors[i];
            }
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;

                colliders[i].enabled = originalColliderEnabled[i];
            }
        }
    }
}