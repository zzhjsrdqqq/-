using UnityEngine;

/// <summary>
/// 暂停菜单 - 挂载到暂停菜单Panel上(默认隐藏)
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;

    public void SetActive(bool active)
    {
        if (pausePanel != null)
            pausePanel.SetActive(active);
    }

    public void OnResumeButton()
    {
        GameManager.Instance.TogglePause();
    }

    public void OnRestartButton()
    {
        GameManager.Instance.RestartLevel();
    }

    public void OnMainMenuButton()
    {
        GameManager.Instance.LoadMainMenu();
    }

    public void OnQuitButton()
    {
        GameManager.Instance.QuitGame();
    }
}
