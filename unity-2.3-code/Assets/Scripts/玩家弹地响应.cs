using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFloorBounceControl : MonoBehaviour
{
    [Header("向上弹起速度")]
    public float bounceUpSpeed = 16f;

    [Header("锁控制时间")]
    public float controlLockTime = 0.12f;

    private Rigidbody2D rb;
    private PlayerController playerController;
    private bool isLocked = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();
    }

    public void TryBounceUp()
    {
        if (isLocked) return;
        if (playerController == null) return;

        StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        isLocked = true;

        playerController.RefreshDashAndJump();
        playerController.enabled = false;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounceUpSpeed);

        yield return new WaitForSeconds(controlLockTime);

        playerController.enabled = true;
        isLocked = false;
    }
}