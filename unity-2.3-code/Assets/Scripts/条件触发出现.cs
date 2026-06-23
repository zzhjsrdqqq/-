using System.Collections.Generic;
using UnityEngine;

public class SignTrigger : MonoBehaviour
{
    [Header("要显示的目标，可以放很多个")]
    public List<GameObject> targetObjects = new List<GameObject>();

    [Header("触发设置")]
    public string playerTag = "Player";

    [Header("开始时是否隐藏这些目标")]
    public bool hideOnStart = true;

    [Header("玩家离开后是否再次隐藏")]
    public bool hideWhenExit = false;

    private void Start()
    {
        if (!hideOnStart) return;

        SetTargetsActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        SetTargetsActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!hideWhenExit) return;
        if (!other.CompareTag(playerTag)) return;

        SetTargetsActive(false);
    }

    private void SetTargetsActive(bool active)
    {
        for (int i = 0; i < targetObjects.Count; i++)
        {
            if (targetObjects[i] != null)
            {
                targetObjects[i].SetActive(active);
            }
        }
    }
}