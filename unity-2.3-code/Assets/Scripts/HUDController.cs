using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 处理计分、HUD显示、暂停逻辑（支持多语言）
/// </summary>
public class HUDController : MonoBehaviour
{
    public enum TimerMode
    {
        External,   // 由别的系统控制 TimerText（倒计时）
        Stopwatch   // HUD 自己计时
    }

    [Header("自动连线")]
    public Text scoreText;
    public Text timerText;
    public Text levelText;
    public GameObject pausePanel;
    public Button pauseButton;
    public Button resumeButton;
    public Button restartButton;
    public Button menuButton;

    [Header("暂停菜单按钮文字（可选，留空则不更新）")]
    public Text resumeButtonText;
    public Text restartButtonText;
    public Text menuButtonText;

    [Header("计时模式")]
    public TimerMode timerMode = TimerMode.External;

    public static HUDController Instance;

    private int score = 0;
    private int maxScore = 0;
    private float timer = 0f;
    private bool isPaused = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

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
        if (levelText != null)
            levelText.text = SceneManager.GetActiveScene().name;

        if (pauseButton  != null) pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton != null) resumeButton.onClick.AddListener(TogglePause);
        if (restartButton != null) restartButton.onClick.AddListener(RestartLevel);
        if (menuButton   != null) menuButton.onClick.AddListener(GoToMenu);

        maxScore = 0;
        foreach (var gem in FindObjectsByType<Gem>(FindObjectsSortMode.None))
            maxScore += gem.scoreValue;
        RefreshScoreDisplay();
        RefreshLocalization();
    }

    void Update()
    {
        if (!isPaused && timerMode == TimerMode.Stopwatch)
        {
            timer += Time.deltaTime;
            if (timerText != null)
            {
                int m = Mathf.FloorToInt(timer / 60f);
                int s = Mathf.FloorToInt(timer % 60f);
                timerText.text = string.Format("{0:00}:{1:00}", m, s);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void RefreshLocalization()
    {
        if (resumeButtonText  != null) resumeButtonText.text  = LocalizationManager.Get("pause_resume");
        if (restartButtonText != null) restartButtonText.text = LocalizationManager.Get("pause_restart");
        if (menuButtonText    != null) menuButtonText.text    = LocalizationManager.Get("pause_mainmenu");
    }

    public void AddScore(int points = 1)
    {
        score += points;
        RefreshScoreDisplay();
    }

    private void RefreshScoreDisplay()
    {
        if (scoreText != null)
            scoreText.text = $"{score}/{maxScore}";
    }

    public int   GetScore() => score;
    public float GetTime()  => timer;

    public void SetTimerDisplay(string text)
    {
        if (timerText != null) timerText.text = text;
    }

    public void SetTimerColor(Color color)
    {
        if (timerText != null) timerText.color = color;
    }

    void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        if (pausePanel != null) pausePanel.SetActive(isPaused);
    }

    void RestartLevel()
    {
        isPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoToMenu()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.LoadMainMenu();
        else
            SceneManager.LoadScene(0);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
