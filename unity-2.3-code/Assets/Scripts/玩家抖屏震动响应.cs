using UnityEngine;
using System.Collections;

public class PlayerJuiceEffects : MonoBehaviour
{
    [System.Serializable]
    public class JuicePreset
    {
        public float shakeAmount = 0.12f;
        public float shakeDuration = 0.06f;
        public float hitStopDuration = 0.03f;
    }

    [Header("相机抖动")]
    public CameraShake2D cameraShake;

    [Header("二段跳")]
    public JuicePreset doubleJumpJuice = new JuicePreset
    {
        shakeAmount = 0.08f,
        shakeDuration = 0.05f,
        hitStopDuration = 0.02f
    };

    [Header("冲刺")]
    public JuicePreset dashJuice = new JuicePreset
    {
        shakeAmount = 0.12f,
        shakeDuration = 0.06f,
        hitStopDuration = 0.03f
    };

    [Header("普通泡泡")]
    public JuicePreset normalBubbleJuice = new JuicePreset
    {
        shakeAmount = 0.04f,
        shakeDuration = 0.03f,
        hitStopDuration = 0.01f
    };

    [Header("特殊泡泡")]
    public JuicePreset specialBubbleJuice = new JuicePreset
    {
        shakeAmount = 0.08f,
        shakeDuration = 0.05f,
        hitStopDuration = 0.02f
    };

    [Header("弹墙")]
    public JuicePreset wallBounceJuice = new JuicePreset
    {
        shakeAmount = 0.13f,
        shakeDuration = 0.07f,
        hitStopDuration = 0.03f
    };

    [Header("弹跳板")]
    public JuicePreset springPadJuice = new JuicePreset
    {
        shakeAmount = 0.09f,
        shakeDuration = 0.05f,
        hitStopDuration = 0.015f
    };

    private Coroutine hitStopRoutine;

    void Awake()
    {
        if (cameraShake == null && Camera.main != null)
            cameraShake = Camera.main.GetComponent<CameraShake2D>();
    }

    public void PlayDoubleJumpJuice()
    {
        PlayJuice(doubleJumpJuice);
    }

    public void PlayDashJuice()
    {
        PlayJuice(dashJuice);
    }

    public void PlayNormalBubbleJuice()
    {
        PlayJuice(normalBubbleJuice);
    }

    public void PlaySpecialBubbleJuice()
    {
        PlayJuice(specialBubbleJuice);
    }

    public void PlayWallBounceJuice()
    {
        PlayJuice(wallBounceJuice);
    }

    public void PlaySpringPadJuice()
    {
        PlayJuice(springPadJuice);
    }

    // 兼容旧代码：旧脚本如果还在调用 PlayBubbleJuice，就默认走特殊泡泡
    public void PlayBubbleJuice()
    {
        PlaySpecialBubbleJuice();
    }

    private void PlayJuice(JuicePreset preset)
    {
        if (cameraShake != null && preset.shakeDuration > 0f && preset.shakeAmount > 0f)
            cameraShake.Shake(preset.shakeAmount, preset.shakeDuration);

        if (preset.hitStopDuration > 0f)
        {
            if (hitStopRoutine != null)
                StopCoroutine(hitStopRoutine);

            hitStopRoutine = StartCoroutine(HitStopRoutine(preset.hitStopDuration));
        }
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        hitStopRoutine = null;
    }

    void OnDisable()
    {
        if (Time.timeScale == 0f)
            Time.timeScale = 1f;
    }
}