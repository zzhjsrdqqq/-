using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 关卡选择界面 - 挂载到LevelSelect场景Canvas上
/// levelButtons 按顺序对应 Level 0, 1, 2, 3
/// </summary>
public class LevelSelectMenu : MonoBehaviour
{
    [Tooltip("按关卡顺序填入所有关卡按钮")]
    public Button[] levelButtons;

    void Start()
    {
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;
            levelButtons[i].interactable = true;
        }
    }

    private static readonly string[] FallbackScenes = { "LV0", "LV1", "LV2", "LV3", "LV4" };

    public void LoadLevel(int index)
    {
        if (index < 0) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadLevel(index);
        }
        else if (index < FallbackScenes.Length)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(FallbackScenes[index]);
        }
    }

    public void BackToMainMenu()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoadMainMenu();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");

    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            BackToMainMenu();
    }
}
