using UnityEngine;

public class PlayerCheckpointState : MonoBehaviour
{
    [Header("默认出生点，不填就用玩家初始位置")]
    public Transform defaultSpawnPoint;

    private Vector3 currentCheckpointPosition;
    private CheckpointPoint currentCheckpoint;

    void Awake()
    {
        PlayerController pc = GetComponent<PlayerController>();

        if (defaultSpawnPoint != null)
            currentCheckpointPosition = defaultSpawnPoint.position;
        else if (pc != null && pc.spawnPoint != null)
            currentCheckpointPosition = pc.spawnPoint.position;
        else
            currentCheckpointPosition = transform.position;
    }

    public void SetCheckpoint(CheckpointPoint checkpoint)
    {
        if (checkpoint == null) return;

        // 旧存档点取消高亮
        if (currentCheckpoint != null && currentCheckpoint != checkpoint)
            currentCheckpoint.SetActiveVisual(false);

        currentCheckpoint = checkpoint;
        currentCheckpointPosition = checkpoint.GetRespawnPosition();

        // 新存档点高亮
        currentCheckpoint.SetActiveVisual(true);
    }

    public Vector3 GetRespawnPosition()
    {
        return currentCheckpointPosition;
    }
}