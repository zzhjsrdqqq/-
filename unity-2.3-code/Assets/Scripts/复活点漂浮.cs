using UnityEngine;

public class CheckpointFloat : MonoBehaviour
{
    public float floatHeight = 0.08f;
    public float floatSpeed = 2f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.localPosition = startPos + new Vector3(0f, y, 0f);
    }
}