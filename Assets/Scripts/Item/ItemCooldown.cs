using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 圆形冷却表现：Image(Filled / Radial360) 的 fillAmount 从 1 走到 0，同时显示剩余时间
/// </summary>
public class ItemCooldown : MonoBehaviour
{
    // 环形/扇形冷却图，Image.type 必须是 Filled + Radial360
    public Image cooldownImage;

    // 剩余时间文本
    public Text timeText;

    //一次完整冷却时长（秒）
    [Min(0.1f)]
    public float cooldown = 3f;

    private float timer;

    public bool IsCooling
    {
        get
        {
            return timer > 0f;
        }
    }

    //剩余比例，1=刚开始，0=冷却结束
    public float RemainingRatio
    {
        get
        {
            return timer <= 0f ? 0f : Mathf.Clamp01(timer / cooldown);
        }
    }

    //剩余秒数
    public float RemainingTime
    {
        get
        {
            return Mathf.Max(0f, timer);
        }
    }

    void Awake()
    {
        if (cooldownImage == null)
        {
            // 约定冷却图放在 Cooldown 子物体下
            Transform child = transform.Find("Cooldown");
            if (child)
            {
                cooldownImage = child.GetComponent<Image>();
            }
        }

        if (timeText == null)
        {
            Transform child = transform.Find("Cooldown/TimeText");
            if (child == null && cooldownImage != null)
            {
                child = cooldownImage.transform.Find("TimeText");
            }
            if (child)
            {
                timeText = child.GetComponent<Text>();
            }
        }

        Apply(0f);
    }

    void Update()
    {
        if (timer <= 0f)
        {
            return;
        }

        timer -= Time.deltaTime;
        Apply(timer <= 0f ? 0f : timer / cooldown);
    }

    //开始一次冷却，seconds > 0 时临时覆盖时长
    public void StartCooldown(float seconds = -1f)
    {
        if (seconds > 0f)
        {
            cooldown = seconds;
        }

        timer = cooldown;
        Apply(1f);
    }

    //立刻结束冷却
    public void ResetCooldown()
    {
        timer = 0f;
        Apply(0f);
    }

    private void Apply(float ratio)
    {
        bool show = ratio > 0f;

        if (cooldownImage != null)
        {
            cooldownImage.fillAmount = ratio;
            cooldownImage.enabled = show;
        }

        if (timeText != null)
        {
            timeText.enabled = show;
            if (show)
            {
                timeText.text = FormatTime(timer);
            }
        }
    }

    /// <summary>
    /// 秒数转显示文本：时:分:秒 / 分:秒 / 秒（高位为 0 时省略）
    /// </summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int hour = total / 3600;
        int minute = total % 3600 / 60;
        int second = total % 60;

        if (hour > 0)
        {
            return $"{hour}:{minute:00}:{second:00}";
        }

        if (minute > 0)
        {
            return $"{minute:00}:{second:00}";
        }

        return $"{second:00}";
    }
}
