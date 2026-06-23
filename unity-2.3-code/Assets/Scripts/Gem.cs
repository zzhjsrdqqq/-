using UnityEngine;

/// <summary>
/// 宝石收集物 - 挂载到宝石Prefab上
/// 需要Collider2D设为IsTrigger，Tag为"Gem"
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Gem : MonoBehaviour
{
    public int scoreValue = 1;
    public float bobSpeed = 2f;
    public float bobHeight = 0.3f;
    public float rotateSpeed = 90f;
    public GameObject collectEffectPrefab;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 上下浮动
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(startPos.x, newY, startPos.z);
        // 旋转
        transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 更新HUD计分
            if (HUDController.Instance != null)
                HUDController.Instance.AddScore(scoreValue);

            // 更新GameManager（如果存在）
            if (GameManager.Instance != null)
                GameManager.Instance.CollectGem();

            if (collectEffectPrefab != null)
                Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
