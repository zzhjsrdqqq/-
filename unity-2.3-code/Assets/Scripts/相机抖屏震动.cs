using UnityEngine;

public class CameraShake2D : MonoBehaviour
{
    private float shakeTimer = 0f;
    private float shakeDuration = 0f;
    private float shakeAmount = 0f;

    private Vector3 currentOffset = Vector3.zero;

    public void Shake(float amount, float duration)
    {
        shakeAmount = Mathf.Max(shakeAmount, amount);
        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeTimer = Mathf.Max(shakeTimer, duration);
    }

    void Update()
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;

            float t = shakeDuration > 0f ? (shakeTimer / shakeDuration) : 0f;
            float currentAmount = shakeAmount * t;

            Vector2 offset2D = Random.insideUnitCircle * currentAmount;
            currentOffset = new Vector3(offset2D.x, offset2D.y, 0f);

            if (shakeTimer <= 0f)
            {
                currentOffset = Vector3.zero;
                shakeAmount = 0f;
                shakeDuration = 0f;
            }
        }
        else
        {
            currentOffset = Vector3.zero;
        }
    }

    public Vector3 GetOffset()
    {
        return currentOffset;
    }
}