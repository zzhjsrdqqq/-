using UnityEngine;
using System.Collections;

/// <summary>
/// 掉落重生区域 - 触发后播死亡特效，等特效播完再传送
/// </summary>
public class KillZone : MonoBehaviour
{
    public Transform respawnPoint;

    [Header("死亡特效")]
    public GameObject deathEffectPrefab;     // 死亡特效预制体
    public float effectDuration = 1.5f;      // 特效持续时间（秒）

    private static float immuneTimer = 0f;
    private bool isPlayingDeath = false;

    void Update()
    {
        if (immuneTimer > 0f) immuneTimer -= Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && immuneTimer <= 0f && !isPlayingDeath)
        {
            StartCoroutine(DeathSequence(other.gameObject));
        }
    }

    IEnumerator DeathSequence(GameObject player)
    {
        isPlayingDeath = true;
        Vector3 deathPos = player.transform.position;

        // 1. 冻结玩家
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        PlayerController pc = player.GetComponent<PlayerController>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        if (pc != null) pc.enabled = false;

        // 2. 暂停倒计时
        PlayerCountdownTimer countdown = player.GetComponent<PlayerCountdownTimer>();
        if (countdown != null) countdown.SetRunning(false);

        // 3. 隐藏玩家精灵（让特效来表现）
        // 直接用 BubblePlayerVisual 里明确的渲染器，避免 GetComponentsInChildren 误伤其他子物体
        BubblePlayerVisual bpv = player.GetComponent<BubblePlayerVisual>();
        SpriteRenderer srBody      = bpv != null ? bpv.bodyRenderer      : null;
        SpriteRenderer srCore      = bpv != null ? bpv.coreRenderer      : null;
        SpriteRenderer srHighlight = bpv != null ? bpv.highlightRenderer : null;
        // 没有 BubblePlayerVisual 时降级处理
        SpriteRenderer srFallback  = (bpv == null) ? player.GetComponent<SpriteRenderer>() : null;
        if (srBody      != null) srBody.enabled      = false;
        if (srCore      != null) srCore.enabled      = false;
        if (srHighlight != null) srHighlight.enabled = false;
        if (srFallback  != null) srFallback.enabled  = false;

        // 4. 暂停摄像机跟随
        CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
        if (cam != null) cam.enabled = false;

        // 5. 在死亡位置生成特效
        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, deathPos, Quaternion.identity);

        // 6. 等特效播完
        yield return new WaitForSeconds(effectDuration);

        // 7. 传送到重生点（优先用存档点系统，其次用 Inspector 指定的 respawnPoint）
        PlayerCheckpointState checkpointState = player.GetComponent<PlayerCheckpointState>();
        if (checkpointState != null)
            player.transform.position = checkpointState.GetRespawnPosition();
        else if (respawnPoint != null)
            player.transform.position = respawnPoint.position;
        else if (pc != null && pc.spawnPoint != null)
            player.transform.position = pc.spawnPoint.position;
        else
            player.transform.position = new Vector3(1f, 2f, 0f);

        // 8. 恢复玩家
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
        if (pc != null) pc.enabled = true;
        if (srBody      != null) srBody.enabled      = true;
        if (srCore      != null) srCore.enabled      = true;
        if (srHighlight != null) srHighlight.enabled = true;
        if (srFallback  != null) srFallback.enabled  = true;

        // 9. 恢复摄像机
        if (cam != null) cam.enabled = true;

        // 10. 重置并恢复倒计时
        if (countdown != null)
        {
            if (countdown.resetTimerWhenRespawn) countdown.ResetTimer();
            countdown.SetRunning(true);
        }

        isPlayingDeath = false;
    }

    public static void SetImmune(float duration = 0.5f)
    {
        immuneTimer = duration;
    }
}