using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class FlipCarryBlock : MonoBehaviour
{
    [Header("携带条件")]
    [Tooltip("玩家站在方块上方时，翻转会带着玩家移动。最常用，建议开启。")]
    public bool carryPlayerOnTop = true;

    [Tooltip("玩家贴在方块下方时，翻转会带着玩家移动")]
    public bool carryPlayerOnBottom = false;

    [Tooltip("玩家贴在方块左侧时，翻转会带着玩家移动")]
    public bool carryPlayerOnLeft = false;

    [Tooltip("玩家贴在方块右侧时，翻转会带着玩家移动")]
    public bool carryPlayerOnRight = false;

    [Tooltip("无论玩家从哪边接触这个方块，翻转都会带着玩家移动")]
    public bool carryPlayerOnAnySide = false;

    [Header("判断宽松度")]
    [Tooltip("判断玩家是否贴着方块的距离容错。一般 0.15 到 0.35")]
    public float contactTolerance = 0.25f;

    [Header("移动设置")]
    [Tooltip("带玩家移动时，是否保持玩家当前速度。建议开启。")]
    public bool keepPlayerVelocity = true;

    [Tooltip("带玩家移动后，是否清理玩家的旋转速度")]
    public bool clearPlayerAngularVelocity = true;

    [Header("调试")]
    public bool debugLog = false;

    private Collider2D blockCollider;

    private Vector3 positionBeforeFlip;

    private List<PlayerController> touchingPlayers = new List<PlayerController>();
    private Dictionary<PlayerController, Collider2D> touchingPlayerColliders = new Dictionary<PlayerController, Collider2D>();

    private List<PlayerController> playersToCarry = new List<PlayerController>();

    private static HashSet<PlayerController> carriedPlayersThisFlip = new HashSet<PlayerController>();

    void Awake()
    {
        blockCollider = GetComponent<Collider2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        RegisterPlayerCollision(collision.collider);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        RegisterPlayerCollision(collision.collider);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        UnregisterPlayerCollision(collision.collider);
    }

    private void RegisterPlayerCollision(Collider2D other)
    {
        if (other == null)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        if (!touchingPlayers.Contains(player))
            touchingPlayers.Add(player);

        touchingPlayerColliders[player] = other;
    }

    private void UnregisterPlayerCollision(Collider2D other)
    {
        if (other == null)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        touchingPlayers.Remove(player);

        if (touchingPlayerColliders.ContainsKey(player))
            touchingPlayerColliders.Remove(player);
    }

    public static void ResetCarriedPlayersThisFlip()
    {
        carriedPlayersThisFlip.Clear();
    }

    public void BeforeSceneFlip()
    {
        positionBeforeFlip = transform.position;

        playersToCarry.Clear();

        for (int i = touchingPlayers.Count - 1; i >= 0; i--)
        {
            PlayerController player = touchingPlayers[i];

            if (player == null)
            {
                touchingPlayers.RemoveAt(i);
                continue;
            }

            Collider2D playerCollider = null;
            touchingPlayerColliders.TryGetValue(player, out playerCollider);

            if (ShouldCarryPlayer(player, playerCollider))
            {
                if (!playersToCarry.Contains(player))
                    playersToCarry.Add(player);
            }
        }
    }

    public void AfterSceneFlip()
    {
        Vector3 delta = transform.position - positionBeforeFlip;

        if (delta.sqrMagnitude <= 0.000001f)
            return;

        for (int i = 0; i < playersToCarry.Count; i++)
        {
            PlayerController player = playersToCarry[i];

            if (player == null)
                continue;

            if (carriedPlayersThisFlip.Contains(player))
                continue;

            MovePlayerByDelta(player, delta);

            carriedPlayersThisFlip.Add(player);

            if (debugLog)
                Debug.Log($"FlipCarryBlock 带动玩家：{gameObject.name}, Delta = {delta}");
        }

        playersToCarry.Clear();
    }

    private bool ShouldCarryPlayer(PlayerController player, Collider2D playerCollider)
    {
        if (player == null)
            return false;

        if (carryPlayerOnAnySide)
            return true;

        if (blockCollider == null)
            blockCollider = GetComponent<Collider2D>();

        if (blockCollider == null)
            return false;

        if (playerCollider == null)
            playerCollider = player.GetComponent<Collider2D>();

        if (playerCollider == null)
            return false;

        Bounds blockBounds = blockCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;

        float tolerance = Mathf.Max(0.01f, contactTolerance);

        bool overlapX =
            playerBounds.max.x >= blockBounds.min.x - tolerance &&
            playerBounds.min.x <= blockBounds.max.x + tolerance;

        bool overlapY =
            playerBounds.max.y >= blockBounds.min.y - tolerance &&
            playerBounds.min.y <= blockBounds.max.y + tolerance;

        bool playerIsOnTop =
            carryPlayerOnTop &&
            overlapX &&
            playerBounds.center.y >= blockBounds.center.y &&
            Mathf.Abs(playerBounds.min.y - blockBounds.max.y) <= tolerance;

        bool playerIsOnBottom =
            carryPlayerOnBottom &&
            overlapX &&
            playerBounds.center.y <= blockBounds.center.y &&
            Mathf.Abs(playerBounds.max.y - blockBounds.min.y) <= tolerance;

        bool playerIsOnLeft =
            carryPlayerOnLeft &&
            overlapY &&
            playerBounds.center.x <= blockBounds.center.x &&
            Mathf.Abs(playerBounds.max.x - blockBounds.min.x) <= tolerance;

        bool playerIsOnRight =
            carryPlayerOnRight &&
            overlapY &&
            playerBounds.center.x >= blockBounds.center.x &&
            Mathf.Abs(playerBounds.min.x - blockBounds.max.x) <= tolerance;

        return playerIsOnTop || playerIsOnBottom || playerIsOnLeft || playerIsOnRight;
    }

    private void MovePlayerByDelta(PlayerController player, Vector3 delta)
    {
        if (player == null)
            return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            Vector2 oldVelocity = rb.linearVelocity;

            Vector2 newPosition = rb.position + new Vector2(delta.x, delta.y);
            rb.position = newPosition;

            Vector3 tfPos = player.transform.position;
            tfPos.x = newPosition.x;
            tfPos.y = newPosition.y;
            player.transform.position = tfPos;

            if (keepPlayerVelocity)
                rb.linearVelocity = oldVelocity;

            if (clearPlayerAngularVelocity)
                rb.angularVelocity = 0f;
        }
        else
        {
            player.transform.position += delta;
        }
    }
}