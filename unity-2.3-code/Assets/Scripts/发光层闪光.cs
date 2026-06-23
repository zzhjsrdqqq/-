using UnityEngine;

public class GlowPulse : MonoBehaviour
{
    public SpriteRenderer sr;
    public float minAlpha = 0.15f;
    public float maxAlpha = 0.45f;
    public float pulseSpeed = 2f;

    void Reset()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (sr == null) return;

        Color c = sr.color;
        float a = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
        sr.color = new Color(c.r, c.g, c.b, a);
    }
}