using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本地化管理器（单例）— 管理游戏语言设置
/// 支持中文和英文，语言选择通过 PlayerPrefs 持久化保存
/// DontDestroyOnLoad，跨场景保留
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    public enum Language { English = 0, Chinese = 1 }

    private Language currentLanguage = Language.English;
    private const string LanguageKey = "GameLanguage";

    /// <summary>语言改变时触发，所有 LocalizedText 组件监听此事件自动刷新</summary>
    public static event Action OnLanguageChanged;

    // ── 本地化文本字典 ──────────────────────────────────────────────────────
    // 格式：key → [English, Chinese]
    private static readonly Dictionary<string, string[]> Texts =
        new Dictionary<string, string[]>
    {
        // ── 主菜单 ──────────────────────────────────────────────────────────
        { "menu_start",       new[] { "PLAY",          "开始游戏"   } },
        { "menu_quit",        new[] { "QUIT",          "退出游戏"   } },
        // 语言切换按钮：显示"切换到对方语言"的名字
        { "menu_language",    new[] { "中文",           "English"   } },

        // ── 关卡选择 ────────────────────────────────────────────────────────
        { "level_back",       new[] { "BACK",          "返回"      } },
        { "level_0",          new[] { "LEVEL 0",       "关卡 0"    } },
        { "level_1",          new[] { "LEVEL 1",       "关卡 1"    } },
        { "level_2",          new[] { "LEVEL 2",       "关卡 2"    } },
        { "level_3",          new[] { "LEVEL 3",       "关卡 3"    } },
        { "level_4",          new[] { "LEVEL 4",       "关卡 4"    } },

        // ── 游戏内 HUD ──────────────────────────────────────────────────────
        { "hud_level",        new[] { "Level",         "关卡"      } },
        { "hud_time",         new[] { "Time",          "时间"      } },
        { "hud_gems",         new[] { "Gems",          "宝石"      } },

        // ── 暂停菜单 ────────────────────────────────────────────────────────
        { "pause_resume",     new[] { "RESUME",        "继续"      } },
        { "pause_restart",    new[] { "RESTART",       "重新开始"  } },
        { "pause_mainmenu",   new[] { "MAIN MENU",     "主菜单"    } },
        { "pause_quit",       new[] { "QUIT",          "退出"      } },

        // ── 通关画面 ────────────────────────────────────────────────────────
        { "win_title",        new[] { "You Win!",      "你赢了！"  } },
        { "win_gems",         new[] { "Gems Collected:", "收集宝石：" } },
        { "win_time",         new[] { "Time:",         "用时："    } },
        { "win_play_again",   new[] { "PLAY AGAIN",    "再玩一次"  } },
    };
    // ───────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 读取上次保存的语言设置，默认英文
        currentLanguage = (Language)PlayerPrefs.GetInt(LanguageKey, (int)Language.English);
    }

    public Language CurrentLanguage => currentLanguage;

    /// <summary>根据 key 获取当前语言的文本</summary>
    public string GetText(string key)
    {
        if (Texts.TryGetValue(key, out string[] values))
            return values[(int)currentLanguage];
        Debug.LogWarning($"[LocalizationManager] 找不到 key: \"{key}\"");
        return key;
    }

    /// <summary>静态快捷方法，代码中可直接写 LocalizationManager.Get("key")</summary>
    public static string Get(string key)
    {
        if (Instance != null) return Instance.GetText(key);
        return key;
    }

    /// <summary>切换到指定语言并通知所有 LocalizedText 刷新</summary>
    public void SetLanguage(Language lang)
    {
        if (currentLanguage == lang) return;
        currentLanguage = lang;
        PlayerPrefs.SetInt(LanguageKey, (int)lang);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    /// <summary>在英文和中文之间来回切换</summary>
    public void ToggleLanguage()
    {
        SetLanguage(currentLanguage == Language.English
            ? Language.Chinese
            : Language.English);
    }
}
