using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 主菜单控制器
/// 语言切换：将语言按钮的 TextMeshProUGUI 赋给 languageButtonText，
/// 按钮 OnClick 绑定 ToggleLanguage()
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("语言切换（可选）")]
    [Tooltip("语言切换按钮上的 TextMeshProUGUI，留空则不更新")]
    public TextMeshProUGUI languageButtonText;

    void Awake()
    {
        if (LocalizationManager.Instance == null)
            new GameObject("LocalizationManager").AddComponent<LocalizationManager>();
    }

    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshLanguageButton;
        RefreshLanguageButton();
    }

    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLanguageButton;
    }

    private void RefreshLanguageButton()
    {
        if (languageButtonText != null)
            languageButtonText.text = LocalizationManager.Get("menu_language");
    }

    public void PlayGame()
    {
        SceneManager.LoadScene("Level select");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>单按钮循环切换：中文 ↔ English</summary>
    public void ToggleLanguage()
    {
        LocalizationManager.Instance?.ToggleLanguage();
    }

    /// <summary>直接切换为英文</summary>
    public void SetEnglish()
    {
        LocalizationManager.Instance?.SetLanguage(LocalizationManager.Language.English);
    }

    /// <summary>直接切换为中文</summary>
    public void SetChinese()
    {
        LocalizationManager.Instance?.SetLanguage(LocalizationManager.Language.Chinese);
    }
}
