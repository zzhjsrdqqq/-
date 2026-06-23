using UnityEngine;
using UnityEngine.UI;

public class PlayerCountdownTimer : MonoBehaviour
{
    [Header("初始时间")]
    public float startTime = 20f;

    [Header("显示")]
    public Text timerText;
    public bool showTenths = true;

    [Header("颜色")]
    public Color normalColor = Color.white;
    public Color warningColor = Color.red;
    public float warningThreshold = 5f;

    [Header("低时间闪烁")]
    public bool enableWarningFlash = true;
    public bool accelerateFlashWhenTimeIsLow = true;
    public float flashSpeedAtThreshold = 6f;
    public float flashSpeedAtZero = 18f;
    [Range(0f, 1f)]
    public float minFlashAlpha = 0.25f;

    [Header("屏幕边缘白边闪烁")]
    public bool enableScreenEdgeFlash = true;
    public Color screenEdgeColor = Color.white;
    [Range(1f, 200f)]
    public float screenEdgeThickness = 24f;
    [Range(0f, 1f)]
    public float screenEdgeMinAlpha = 0.05f;
    [Range(0f, 1f)]
    public float screenEdgeMaxAlpha = 0.45f;

    [Header("规则")]
    public bool startOnAwake = true;
    public bool resetTimerWhenRespawn = true;

    public float CurrentTime => currentTime;
    public bool IsWarning => currentTime > 0f && currentTime <= warningThreshold;

    private float currentTime;
    private bool isRunning = false;
    private bool timeoutTriggered = false;
    private float flashPulseTime = 0f;

    private PlayerController playerController;
    private static Texture2D whiteTexture;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        currentTime = startTime;

        if (timerText == null && HUDController.Instance != null)
            timerText = HUDController.Instance.timerText;

        if (startOnAwake)
            isRunning = true;

        EnsureWhiteTexture();
        UpdateUI();
    }

    void Update()
    {
        if (!isRunning) return;
        if (timeoutTriggered) return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            UpdateUI();
            TriggerTimeOutDeath();
            return;
        }

        if (IsWarning && enableWarningFlash)
            flashPulseTime += Time.deltaTime * GetCurrentFlashSpeed();
        else
            flashPulseTime = 0f;

        UpdateUI();
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (!enableScreenEdgeFlash) return;
        if (Event.current.type != EventType.Repaint) return;
        if (!IsScreenEdgeWarningActive()) return;

        EnsureWhiteTexture();

        float alpha = GetScreenEdgeAlpha();
        if (alpha <= 0.001f) return;

        float thickness = Mathf.Max(1f, screenEdgeThickness);
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        Color oldColor = GUI.color;
        Color drawColor = screenEdgeColor;
        drawColor.a *= alpha;
        GUI.color = drawColor;

        GUI.DrawTexture(new Rect(0f, 0f, screenWidth, thickness), whiteTexture);
        GUI.DrawTexture(new Rect(0f, screenHeight - thickness, screenWidth, thickness), whiteTexture);
        GUI.DrawTexture(new Rect(0f, 0f, thickness, screenHeight), whiteTexture);
        GUI.DrawTexture(new Rect(screenWidth - thickness, 0f, thickness, screenHeight), whiteTexture);

        GUI.color = oldColor;
    }

    public void AddTime(float amount)
    {
        if (amount <= 0f) return;

        currentTime += amount;
        UpdateUI();
    }

    public void ResetTimer()
    {
        currentTime = startTime;
        timeoutTriggered = false;
        UpdateUI();
    }

    public void SetRunning(bool running)
    {
        isRunning = running;
    }

    private void TriggerTimeOutDeath()
    {
        if (timeoutTriggered) return;
        timeoutTriggered = true;

        if (playerController != null)
            playerController.Respawn();
    }

    private void UpdateUI()
    {
        string text = FormatTime(currentTime);
        Color color = GetDisplayColor();

        if (timerText != null)
        {
            timerText.text = text;
            timerText.color = color;
        }

        if (HUDController.Instance != null)
        {
            HUDController.Instance.SetTimerDisplay(text);
            HUDController.Instance.SetTimerColor(color);
        }
    }

    private Color GetDisplayColor()
    {
        Color color = IsWarning ? warningColor : normalColor;

        if (IsWarning && enableWarningFlash)
        {
            float alpha = Mathf.Lerp(minFlashAlpha, 1f, GetWarningPulse01());
            color.a = alpha;
        }
        else
        {
            color.a = 1f;
        }

        return color;
    }

    private float GetScreenEdgeAlpha()
    {
        if (!enableWarningFlash)
            return screenEdgeMaxAlpha;

        return Mathf.Lerp(screenEdgeMinAlpha, screenEdgeMaxAlpha, GetWarningPulse01());
    }

    private float GetCurrentFlashSpeed()
    {
        if (!accelerateFlashWhenTimeIsLow)
            return flashSpeedAtThreshold;

        float threshold = Mathf.Max(0.01f, warningThreshold);
        float t = 1f - Mathf.Clamp01(currentTime / threshold);
        return Mathf.Lerp(flashSpeedAtThreshold, flashSpeedAtZero, t);
    }

    private float GetWarningPulse01()
    {
        return (Mathf.Sin(flashPulseTime) + 1f) * 0.5f;
    }

    private bool IsScreenEdgeWarningActive()
    {
        return isRunning && !timeoutTriggered && IsWarning;
    }

    private void EnsureWhiteTexture()
    {
        if (whiteTexture != null) return;

        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
    }

    private string FormatTime(float time)
    {
        time = Mathf.Max(0f, time);

        if (showTenths)
            return time.ToString("0.0");

        int totalSeconds = Mathf.CeilToInt(time);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}