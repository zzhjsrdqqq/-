using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 显示宝石数、关卡名、计时（支持多语言）
/// 挂载到Canvas下的空对象上
/// </summary>
public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI gemText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI timeText;

    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshLocalization;
    }

    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLocalization;
    }

    void Start()
    {
        if (GameManager.Instance != null)
            UpdateGemCount(GameManager.Instance.totalGems);

        RefreshLocalization();
    }

    void Update()
    {
        if (GameManager.Instance != null && timeText != null)
            timeText.text = Mathf.FloorToInt(GameManager.Instance.playTime) + "s";
    }

    private void RefreshLocalization()
    {
        if (levelText != null && GameManager.Instance != null)
            levelText.text = LocalizationManager.Get("hud_level") + " "
                             + GameManager.Instance.GetCurrentLevelIndex();
    }

    public void UpdateGemCount(int count)
    {
        if (gemText != null)
            gemText.text = count.ToString();
    }
}
