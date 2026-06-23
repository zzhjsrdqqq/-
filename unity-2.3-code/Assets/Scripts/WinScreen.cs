using UnityEngine;
using TMPro;

/// <summary>
/// 通关画面 - 显示统计信息（支持多语言）
/// </summary>
public class WinScreen : MonoBehaviour
{
    public TextMeshProUGUI statsText;

    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshStats;
        RefreshStats();
    }

    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshStats;
    }

    private void RefreshStats()
    {
        if (GameManager.Instance == null || statsText == null) return;

        int gems   = GameManager.Instance.totalGems;
        float time = GameManager.Instance.playTime;

        string gemsLabel = LocalizationManager.Get("win_gems");
        string timeLabel = LocalizationManager.Get("win_time");
        statsText.text = $"{gemsLabel} {gems}\n{timeLabel} {time:F1}s";
    }

    public void PlayAgain()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGame();
            GameManager.Instance.LoadMainMenu();
        }
    }
}
