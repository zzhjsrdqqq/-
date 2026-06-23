using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AbilityUnlockSwitch : MonoBehaviour
{
    [Header("触发设置")]
    [Tooltip("是否只能触发一次")]
    public bool oneTimeOnly = true;

    [Tooltip("是否需要按上键才触发。关闭后玩家碰到就触发")]
    public bool requireUpKey = false;

    [Tooltip("触发后是否禁用这个开关")]
    public bool disableAfterUse = false;

    [Header("要打开的能力")]
    [Tooltip("打开二段跳")]
    public bool unlockDoubleJump = true;

    [Tooltip("打开冲刺")]
    public bool unlockDash = true;

    [Tooltip("打开斜上冲刺")]
    public bool unlockDiagonalUpDash = true;

    [Header("能力联动")]
    [Tooltip("如果打开斜上冲刺，是否自动同时打开冲刺。建议开启，否则斜上冲刺没有冲刺能力时不会生效")]
    public bool autoEnableDashWhenUnlockDiagonalUpDash = true;

    [Tooltip("打开能力后，是否立刻刷新玩家跳跃/冲刺次数")]
    public bool refreshDashAndJumpAfterUnlock = true;

    [Header("表现设置")]
    [Tooltip("触发后是否改变颜色")]
    public bool changeColorAfterUse = true;

    [Tooltip("触发后的颜色")]
    public Color usedColor = new Color(0.4f, 1f, 0.8f, 1f);

    private bool hasTriggered = false;

    private PlayerController playerInside;
    private SpriteRenderer sr;
    private Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        if (col != null)
            col.isTrigger = true;
    }

    void Update()
    {
        if (!requireUpKey)
            return;

        if (playerInside == null)
            return;

        if (hasTriggered && oneTimeOnly)
            return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            UnlockAbilities(playerInside);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        playerInside = player;

        if (!requireUpKey)
        {
            UnlockAbilities(player);
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        playerInside = player;

        if (!requireUpKey)
        {
            UnlockAbilities(player);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        if (player == playerInside)
            playerInside = null;
    }

    private void UnlockAbilities(PlayerController player)
    {
        if (player == null)
            return;

        if (hasTriggered && oneTimeOnly)
            return;

        if (unlockDoubleJump)
            player.SetDoubleJumpEnabled(true);

        if (unlockDash)
            player.SetDashEnabled(true);

        if (unlockDiagonalUpDash)
        {
            player.SetDiagonalUpDashEnabled(true);

            if (autoEnableDashWhenUnlockDiagonalUpDash)
                player.SetDashEnabled(true);
        }

        if (refreshDashAndJumpAfterUnlock)
            player.RefreshDashAndJump();

        hasTriggered = true;

        if (changeColorAfterUse && sr != null)
            sr.color = usedColor;

        if (disableAfterUse)
        {
            if (col != null)
                col.enabled = false;

            if (sr != null)
                sr.enabled = false;
        }
    }
}