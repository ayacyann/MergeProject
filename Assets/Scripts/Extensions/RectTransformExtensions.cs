using UnityEngine;

/// <summary>
/// RectTransform 的扩展方法。
/// 扩展方法要求：所在的类是 static 类，方法本身是 static，且第一个参数前面加 this。
/// </summary>
public static class RectTransformExtensions
{
    /// <summary>
    /// 让 rt 填满 parent 的矩形：四边锚点拉伸，offset 全部归零。
    /// 等价于在 Anchor Presets 里选 stretch/stretch 并把上下左右都设为 0。
    /// 只处理布局，不改位置和缩放：位置交给跟随逻辑，缩放交给 MouseScaleEvent。
    /// </summary>
    public static void FitToParent(this RectTransform rt, Transform parent)
    {
        rt.SetParent(parent, false);

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localRotation = Quaternion.identity;
    }
}
