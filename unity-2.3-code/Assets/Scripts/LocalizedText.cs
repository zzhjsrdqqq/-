using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 挂载到含有 Text 或 TextMeshProUGUI 的 GameObject 上。
/// 在 Inspector 中填写 localizationKey，
/// 语言改变时自动刷新文本内容，无需手动调用。
/// </summary>
[AddComponentMenu("Localization/Localized Text")]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("本地化字典中的 Key，例如：menu_start / pause_resume / win_title 等")]
    public string localizationKey;

    private TextMeshProUGUI tmpText;
    private Text uiText;

    void Awake()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
        uiText  = GetComponent<Text>();
    }

    void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    void Start()
    {
        // Awake 执行顺序问题的保底：所有 Awake 完成后再刷新一次
        Refresh();
    }

    void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= Refresh;
    }

    /// <summary>手动刷新文本（也可从外部调用）</summary>
    public void Refresh()
    {
        if (string.IsNullOrEmpty(localizationKey)) return;
        string text = LocalizationManager.Get(localizationKey);
        if (tmpText != null) tmpText.text = text;
        else if (uiText != null) uiText.text = text;
    }
}
