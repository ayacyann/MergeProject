using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 鼠标进入时聚焦放大，离开时恢复原始大小。
/// 全局最多只有一个物体处于聚焦状态（新物体进入时会把上一个还原）。
/// </summary>
public class MouseScaleEvent : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("聚焦时的缩放倍率")]
    const float focusScale = 1.2f;

    [Tooltip("缩放过渡时长（秒），和 gamedemo 的 scale_tween_time 一致")]
    const float tweenTime = 0.1f;

    // 当前处于聚焦状态的对象，保证同时最多只有一个
    private static MouseScaleEvent currentFocused;

    private Vector3 originalScale;
    private Coroutine scaleRoutine;

    // 鼠标当前是否停在该物体上
    private bool isHovered;

    // 是否允许聚焦缩放（拖拽过程中会临时关掉）
    private bool scaleEnabled = true;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        // 对象池复用时重置状态
        isHovered = false;
        scaleEnabled = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        if (!scaleEnabled)
            return;

        Focus();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (!scaleEnabled)
            return;

        if (currentFocused == this)
        {
            currentFocused = null;
        }

        SetFocused(false);
    }

    private void Focus()
    {
        if (currentFocused != null && currentFocused != this)
        {
            currentFocused.SetFocused(false);
        }

        currentFocused = this;
        SetFocused(true);
    }

    /// <summary>
    /// 开关聚焦缩放。关闭时播放缩小到原始大小的动画（按下/拖拽过程中物品保持 scale = 1）。
    /// 重新打开时如果鼠标仍停在物体上，会重新聚焦。
    /// </summary>
    public void SetScaleEnabled(bool enabled)
    {
        if (scaleEnabled == enabled)
            return;

        scaleEnabled = enabled;

        if (!enabled)
        {
            if (currentFocused == this)
                currentFocused = null;

            // 用动画缩放到 originalScale，不直接赋值
            SetFocused(false);
        }
        else if (isHovered)
        {
            Focus();
        }
    }

    /// <summary>立即恢复原始缩放并清掉聚焦状态</summary>
    public void ResetScale()
    {
        if (currentFocused == this)
        {
            currentFocused = null;
        }

        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }

        transform.localScale = originalScale;
    }

    private void SetFocused(bool focused)
    {
        Vector3 target = focused ? originalScale * focusScale : originalScale;

        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
        }

        scaleRoutine = StartCoroutine(ScaleTo(target));
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;

        while (elapsed < tweenTime)
        {
            elapsed += Time.deltaTime;
            float t = tweenTime <= 0f ? 1f : Mathf.Clamp01(elapsed / tweenTime);
            transform.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }

        transform.localScale = target;
        scaleRoutine = null;
    }

    private void OnDisable()
    {
        // 回收/销毁时协程不会继续执行，只能直接还原，
        // 避免留下放大状态，也不要让静态引用指向已失效对象
        ResetScale();
    }
}
