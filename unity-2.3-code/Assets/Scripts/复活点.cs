using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class CheckpointPoint : MonoBehaviour
{
    [Header("玩家复活位置，不填就用当前物体位置")]
    public Transform respawnPoint;

    [Header("是否只能触发一次")]
    public bool oneTimeOnly = false;

    [Header("触碰时刷新倒计时")]
    public bool refreshTimerOnTouch = true;

    [Tooltip("如果复活点已经激活过，再次触碰是否仍然刷新倒计时")]
    public bool refreshTimerEvenIfAlreadyActivated = true;

    [Header("主显示")]
    public SpriteRenderer bodyRenderer;

    [Header("发光层")]
    public SpriteRenderer glowRenderer;

    [Header("未激活颜色")]
    public Color inactiveBodyColor = Color.gray;
    public Color inactiveGlowColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("激活颜色")]
    public Color activeBodyColor = Color.white;
    public Color activeGlowColor = new Color(0.4f, 1f, 1f, 0.75f);

    [Header("激活时是否播放弹一下动画")]
    public bool playPopAnimation = true;

    private bool hasBeenActivated = false;
    private Coroutine animRoutine;

    void Start()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        SetActiveVisual(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerCheckpointState checkpointState = other.GetComponentInParent<PlayerCheckpointState>();
        if (checkpointState == null) return;

        bool alreadyActivated = hasBeenActivated;

        if (refreshTimerOnTouch)
        {
            if (!alreadyActivated || refreshTimerEvenIfAlreadyActivated)
            {
                RefreshPlayerTimer(other);
            }
        }

        if (oneTimeOnly && hasBeenActivated)
            return;

        checkpointState.SetCheckpoint(this);
        hasBeenActivated = true;

        Debug.Log($"激活存档点：{gameObject.name}");
    }

    private void RefreshPlayerTimer(Collider2D other)
    {
        if (other == null) return;

        PlayerCountdownTimer timer = other.GetComponentInParent<PlayerCountdownTimer>();
        if (timer == null) return;

        timer.ResetTimer();
    }

    public Vector3 GetRespawnPosition()
    {
        if (respawnPoint != null)
            return respawnPoint.position;

        return transform.position;
    }

    public void SetActiveVisual(bool active)
    {
        if (bodyRenderer != null)
            bodyRenderer.color = active ? activeBodyColor : inactiveBodyColor;

        if (glowRenderer != null)
        {
            glowRenderer.enabled = active;

            if (active)
                glowRenderer.color = activeGlowColor;
        }

        if (active && playPopAnimation)
        {
            if (animRoutine != null)
                StopCoroutine(animRoutine);

            animRoutine = StartCoroutine(PopAnimation());
        }
    }

    private IEnumerator PopAnimation()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 bigScale = originalScale * 1.15f;

        float t = 0f;
        float duration = 0.08f;

        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, bigScale, t / duration);
            yield return null;
        }

        t = 0f;
        duration = 0.12f;

        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(bigScale, originalScale, t / duration);
            yield return null;
        }

        transform.localScale = originalScale;
        animRoutine = null;
    }
}