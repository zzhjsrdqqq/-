using UnityEngine;

/// <summary>
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Goal : MonoBehaviour
{
    public GameObject reachEffectPrefab;
    public int nextLevelIndex = -1; //

    private SpriteRenderer sr;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 光柱闪烁效果
        if (sr != null)
        {
            float a = 0.5f + Mathf.Sin(Time.time * 3f) * 0.3f;
            sr.color = new Color(1f, 0.85f, 0.3f, a);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (reachEffectPrefab != null)
                Instantiate(reachEffectPrefab, transform.position, Quaternion.identity);

            if (nextLevelIndex >= 0)
                GameManager.Instance.LoadLevel(nextLevelIndex);
            else
                GameManager.Instance.CompleteLevel();
        }
    }
}
