using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class CameraZoomController : MonoBehaviour
{
    [Header("默认镜头大小")]
    [Tooltip("游戏开始时是否自动记录当前相机大小作为默认大小")]
    public bool recordDefaultSizeOnAwake = true;

    [Tooltip("默认 Orthographic Size。2D 相机数值越小越近，越大越远")]
    public float defaultOrthographicSize = 5f;

    [Header("默认过渡")]
    public float defaultZoomDuration = 0.35f;

    [Header("区域切换保护")]
    [Tooltip("离开一个镜头区后，不立刻恢复默认，而是等一小会儿。用于防止跨区/传送时镜头抽拉。")]
    public float emptyRestoreDelay = 0.08f;

    [Tooltip("目标镜头大小差值小于这个数时，认为倍率相同，不重新播放缩放动画。")]
    public float targetSizeEpsilon = 0.01f;

    [Header("调试")]
    public bool debugLog = false;

    private Camera cam;

    private Coroutine zoomRoutine;
    private Coroutine evaluateRoutine;
    private Coroutine emptyRestoreRoutine;

    private readonly List<CameraZoomTrigger> activeTriggers = new List<CameraZoomTrigger>();

    private int enterOrderCounter = 0;

    private float currentTargetSize;
    private bool hasCurrentTarget = false;

    private float pendingRestoreDuration;

    void Awake()
    {
        cam = GetComponent<Camera>();

        if (cam != null && recordDefaultSizeOnAwake)
            defaultOrthographicSize = cam.orthographicSize;

        if (cam != null)
        {
            currentTargetSize = cam.orthographicSize;
            hasCurrentTarget = true;
        }

        pendingRestoreDuration = defaultZoomDuration;
    }

    public void RegisterZoomTrigger(CameraZoomTrigger trigger)
    {
        if (trigger == null)
            return;

        if (!activeTriggers.Contains(trigger))
            activeTriggers.Add(trigger);

        enterOrderCounter++;
        trigger.InternalSetLastEnterOrder(enterOrderCounter);

        CancelEmptyRestore();
        QueueEvaluate(trigger.zoomDuration);

        if (debugLog)
            Debug.Log($"登记镜头区：{trigger.name}");
    }

    public void UnregisterZoomTrigger(CameraZoomTrigger trigger, float restoreDuration)
    {
        if (trigger == null)
            return;

        activeTriggers.Remove(trigger);

        pendingRestoreDuration = restoreDuration;
        QueueEvaluate(restoreDuration);

        if (debugLog)
            Debug.Log($"移除镜头区：{trigger.name}");
    }

    private void QueueEvaluate(float preferredDuration)
    {
        if (evaluateRoutine != null)
            StopCoroutine(evaluateRoutine);

        evaluateRoutine = StartCoroutine(EvaluateAtEndOfFrame(preferredDuration));
    }

    private IEnumerator EvaluateAtEndOfFrame(float preferredDuration)
    {
        yield return new WaitForEndOfFrame();

        evaluateRoutine = null;
        ApplyBestActiveZoom(preferredDuration);
    }

    private void ApplyBestActiveZoom(float preferredDuration)
    {
        CleanupInvalidTriggers();

        CameraZoomTrigger best = GetBestActiveTrigger();

        if (best != null)
        {
            CancelEmptyRestore();

            float targetSize = best.GetTargetSize();
            float duration = best.zoomDuration;

            SetZoomInternal(targetSize, duration);

            if (debugLog)
                Debug.Log($"应用镜头区：{best.name}, Size = {targetSize}");

            return;
        }

        if (emptyRestoreDelay > 0f)
        {
            if (emptyRestoreRoutine != null)
                StopCoroutine(emptyRestoreRoutine);

            emptyRestoreRoutine = StartCoroutine(RestoreDefaultAfterDelay());
        }
        else
        {
            RestoreDefaultZoom(pendingRestoreDuration);
        }
    }

    private IEnumerator RestoreDefaultAfterDelay()
    {
        float timer = 0f;

        while (timer < emptyRestoreDelay)
        {
            timer += Time.deltaTime;
            yield return null;

            CleanupInvalidTriggers();

            if (GetBestActiveTrigger() != null)
            {
                emptyRestoreRoutine = null;
                QueueEvaluate(defaultZoomDuration);
                yield break;
            }
        }

        emptyRestoreRoutine = null;
        RestoreDefaultZoom(pendingRestoreDuration);
    }

    private void CleanupInvalidTriggers()
    {
        for (int i = activeTriggers.Count - 1; i >= 0; i--)
        {
            CameraZoomTrigger trigger = activeTriggers[i];

            if (trigger == null)
            {
                activeTriggers.RemoveAt(i);
                continue;
            }

            if (!trigger.gameObject.activeInHierarchy)
            {
                activeTriggers.RemoveAt(i);
                continue;
            }

            if (!trigger.enabled)
            {
                activeTriggers.RemoveAt(i);
                continue;
            }

            if (!trigger.IsRegisteredActive)
            {
                activeTriggers.RemoveAt(i);
                continue;
            }
        }
    }

    private CameraZoomTrigger GetBestActiveTrigger()
    {
        CameraZoomTrigger best = null;

        for (int i = 0; i < activeTriggers.Count; i++)
        {
            CameraZoomTrigger trigger = activeTriggers[i];

            if (trigger == null)
                continue;

            if (!trigger.IsRegisteredActive)
                continue;

            if (best == null)
            {
                best = trigger;
                continue;
            }

            if (trigger.priority > best.priority)
            {
                best = trigger;
                continue;
            }

            if (trigger.priority == best.priority &&
                trigger.LastEnterOrder > best.LastEnterOrder)
            {
                best = trigger;
            }
        }

        return best;
    }

    public void SetZoom(float targetOrthographicSize)
    {
        SetZoom(targetOrthographicSize, defaultZoomDuration);
    }

    public void SetZoom(float targetOrthographicSize, float duration)
    {
        CancelEmptyRestore();
        SetZoomInternal(targetOrthographicSize, duration);
    }

    private void SetZoomInternal(float targetOrthographicSize, float duration)
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        if (cam == null)
            return;

        targetOrthographicSize = Mathf.Max(0.1f, targetOrthographicSize);
        duration = Mathf.Max(0f, duration);

        bool sameTarget =
            hasCurrentTarget &&
            Mathf.Abs(currentTargetSize - targetOrthographicSize) <= targetSizeEpsilon;

        if (sameTarget)
        {
            if (debugLog)
                Debug.Log($"镜头倍率相同，不重新抽拉：{targetOrthographicSize}");

            return;
        }

        currentTargetSize = targetOrthographicSize;
        hasCurrentTarget = true;

        if (zoomRoutine != null)
            StopCoroutine(zoomRoutine);

        zoomRoutine = StartCoroutine(ZoomRoutine(targetOrthographicSize, duration));
    }

    public void RestoreDefaultZoom()
    {
        RestoreDefaultZoom(defaultZoomDuration);
    }

    public void RestoreDefaultZoom(float duration)
    {
        SetZoomInternal(defaultOrthographicSize, duration);
    }

    private void CancelEmptyRestore()
    {
        if (emptyRestoreRoutine != null)
        {
            StopCoroutine(emptyRestoreRoutine);
            emptyRestoreRoutine = null;
        }
    }

    private IEnumerator ZoomRoutine(float targetSize, float duration)
    {
        if (cam == null)
            yield break;

        float startSize = cam.orthographicSize;

        if (duration <= 0f)
        {
            cam.orthographicSize = targetSize;
            zoomRoutine = null;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.Clamp01(t);

            // SmoothStep
            t = t * t * (3f - 2f * t);

            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            yield return null;
        }

        cam.orthographicSize = targetSize;
        zoomRoutine = null;
    }
}