using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 绑定 Canvas 下全屏 Image（或带 CanvasGroup）；透明→不透明→保持→透明。
/// </summary>
public class ScreenFadeController : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private CanvasGroup targetGroup;

    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float holdBlackDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    [Header("Debug")]
    [Tooltip("勾选后在 Console 输出淡入淡出诊断信息（无引用、Canvas 未激活、busy 跳过等）")]
    [SerializeField] private bool enableFadeDebugLogs = true;

    bool busy;

    public bool IsBusy => busy;

    public void RunFadeInHoldFadeOut(Action onMidBlack)
    {
        if (busy)
        {
            FadeDbgWarn("RunFadeInHoldFadeOut 被调用但上一段淡入淡出尚未结束 (busy=true)，本次已忽略。");
            return;
        }

        FadeDbg("RunFadeInHoldFadeOut 开始");
        LogTargetsAndCanvas("调用时");
        if (targetImage == null && targetGroup == null)
        {
            FadeDbgError("targetImage 与 targetGroup 均未赋值，淡入淡出不会产生任何画面。请在 Inspector 中指定其一。");
            return;
        }

        StartCoroutine(FadeRoutine(onMidBlack));
    }

    IEnumerator FadeRoutine(Action onMidBlack)
    {
        busy = true;
        EnsureRenderable();
        LogTargetsAndCanvas("EnsureRenderable 之后");

        yield return FadeToAlpha(1f, Mathf.Max(0.01f, fadeInDuration));
        FadeDbg("淡入结束，alpha 应≈1，即将执行 onMidBlack（传送/换日等）");
        LogTargetsAndCanvas("淡入结束");
        onMidBlack?.Invoke();
        if (holdBlackDuration > 0f)
            yield return new WaitForSeconds(holdBlackDuration);
        yield return FadeToAlpha(0f, Mathf.Max(0.01f, fadeOutDuration));

        HideIfZero();
        busy = false;
        FadeDbg("淡出结束，FadeRoutine 完成");
        LogTargetsAndCanvas("结束");
    }

    void EnsureRenderable()
    {
        if (targetGroup != null)
        {
            targetGroup.alpha = 0f;
            targetGroup.gameObject.SetActive(true);
            EnsureParentCanvasActive(targetGroup.transform);
            FadeDbg("EnsureRenderable: 使用 CanvasGroup，已设为 alpha=0 并 SetActive(true)");
            return;
        }
        if (targetImage != null)
        {
            var c = targetImage.color;
            c.a = 0f;
            targetImage.color = c;
            targetImage.gameObject.SetActive(true);
            EnsureParentCanvasActive(targetImage.transform);
            FadeDbg("EnsureRenderable: 使用 Image，已设为 alpha=0 并 SetActive(true)");
        }
    }

    void EnsureParentCanvasActive(Transform t)
    {
        var canvas = t != null ? t.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            FadeDbgError("未找到父级 Canvas：UI 不会显示。请把遮罩放在 Canvas 下。");
            return;
        }
        if (!canvas.gameObject.activeInHierarchy)
        {
            FadeDbgWarn($"父级 Canvas「{canvas.name}」未激活，已尝试 SetActive(true)。");
            canvas.gameObject.SetActive(true);
        }
        if (!canvas.enabled)
        {
            FadeDbgWarn($"父级 Canvas「{canvas.name}」的 Canvas 组件被禁用，已尝试 enabled=true。");
            canvas.enabled = true;
        }
    }

    void HideIfZero()
    {
        if (targetGroup != null && targetGroup.alpha <= 0.001f)
            targetGroup.gameObject.SetActive(false);
        if (targetImage != null && targetImage.color.a <= 0.001f)
            targetImage.gameObject.SetActive(false);
    }

    void LogTargetsAndCanvas(string moment)
    {
        if (!enableFadeDebugLogs) return;

        if (targetGroup != null)
        {
            var go = targetGroup.gameObject;
            var c = targetGroup.GetComponentInParent<Canvas>();
            Debug.Log(
                $"[ScreenFade][{moment}] CanvasGroup on {go.name} | alpha={targetGroup.alpha:F3} " +
                $"activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} " +
                $"interactable={targetGroup.interactable} blocksRaycasts={targetGroup.blocksRaycasts}" +
                (c != null ? $" | Canvas[{c.name}] enabled={c.enabled} sortOrder={c.sortingOrder} renderMode={c.renderMode}" : " | 无 Canvas 父节点"));
        }
        else if (targetImage != null)
        {
            var go = targetImage.gameObject;
            var c = targetImage.GetComponentInParent<Canvas>();
            Debug.Log(
                $"[ScreenFade][{moment}] Image on {go.name} | color={targetImage.color} " +
                $"activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} raycastTarget={targetImage.raycastTarget}" +
                (c != null ? $" | Canvas[{c.name}] enabled={c.enabled} sortOrder={c.sortingOrder} renderMode={c.renderMode}" : " | 无 Canvas 父节点"));
        }
    }

    void FadeDbg(string msg)
    {
        if (enableFadeDebugLogs) Debug.Log("[ScreenFade] " + msg);
    }

    void FadeDbgWarn(string msg)
    {
        if (enableFadeDebugLogs) Debug.LogWarning("[ScreenFade] " + msg);
    }

    void FadeDbgError(string msg)
    {
        Debug.LogError("[ScreenFade] " + msg);
    }

    IEnumerator FadeToAlpha(float endAlpha, float duration)
    {
        float start;
        if (targetGroup != null)
        {
            start = targetGroup.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                targetGroup.alpha = Mathf.Lerp(start, endAlpha, t / duration);
                yield return null;
            }
            targetGroup.alpha = endAlpha;
            yield break;
        }

        if (targetImage == null) yield break;
        start = targetImage.color.a;
        float t2 = 0f;
        while (t2 < duration)
        {
            t2 += Time.deltaTime;
            float a = Mathf.Lerp(start, endAlpha, t2 / duration);
            var c = targetImage.color;
            c.a = a;
            targetImage.color = c;
            yield return null;
        }
        {
            var c = targetImage.color;
            c.a = endAlpha;
            targetImage.color = c;
        }
    }
}
