using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏管理器 (单例) - 管理关卡、宝石、游戏状态
/// 挂载到场景中的空GameObject上
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("关卡设置")]
    [Tooltip("按顺序填入所有关卡Scene名: Level0_Tutorial, Level1, Level2, Level3...")]
    public string[] levelSceneNames;

    [HideInInspector] public int totalGems = 0;
    [HideInInspector] public float playTime = 0f;

    private int currentLevelIndex = 0;
    private bool isPaused = false;

    private const string UnlockKey = "MaxUnlockedLevel";

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

        if (levelSceneNames == null || levelSceneNames.Length == 0)
            levelSceneNames = new[] { "LV0", "LV1", "LV2", "LV3", "LV4" };

        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;

        // 确保本地化管理器存在（从任意场景启动时也能正常工作）
        if (LocalizationManager.Instance == null)
            new GameObject("LocalizationManager").AddComponent<LocalizationManager>();
    }

    public int GetMaxUnlockedLevel()
    {
        return PlayerPrefs.GetInt(UnlockKey, 0);
    }

    private void SaveUnlockProgress(int levelIndex)
    {
        if (levelIndex > GetMaxUnlockedLevel())
        {
            PlayerPrefs.SetInt(UnlockKey, levelIndex);
            PlayerPrefs.Save();
        }
    }

    void Update()
    {
        if (!isPaused)
            playTime += Time.deltaTime;

        // 暂停
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void CollectGem()
    {
        totalGems++;
        UIManager uiManager = Object.FindFirstObjectByType<UIManager>();
        if (uiManager != null)
            uiManager.UpdateGemCount(totalGems);
    }

    public void CompleteLevel()
    {
        currentLevelIndex++;
        SaveUnlockProgress(currentLevelIndex);
        if (currentLevelIndex < levelSceneNames.Length)
        {
            SceneManager.LoadScene(levelSceneNames[currentLevelIndex]);
        }
        else
        {
            SceneManager.LoadScene("WinScreen");
        }
    }

    public void LoadLevel(int index)
    {
        if (index >= 0 && index < levelSceneNames.Length)
        {
            currentLevelIndex = index;
            isPaused = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(levelSceneNames[index]);
        }
    }

    public void LoadMainMenu()
    {
        isPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void LoadLevelSelect()
    {
        // LevelSelect 场景暂未创建，回主菜单
        LoadMainMenu();
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        PauseMenu pm = Object.FindFirstObjectByType<PauseMenu>();
        if (pm != null) pm.SetActive(isPaused);
    }

    public void RestartLevel()
    {
        isPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ResetGame()
    {
        totalGems = 0;
        playTime = 0f;
        currentLevelIndex = 0;
    }

    public void ResetUnlockProgress()
    {
        PlayerPrefs.DeleteKey(UnlockKey);
        PlayerPrefs.Save();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public int GetCurrentLevelIndex() => currentLevelIndex;
}
