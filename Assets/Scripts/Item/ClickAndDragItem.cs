using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 支持"点击触发效果 + 拖拽移动/合并"的物品基类。
/// 按下到抬起的位移小于 clickThreshold 时视为点击，调用 OnClickFunc；
/// 超过阈值后才真正进入 Item 的拖拽流程。
/// </summary>
public class ClickAndDragItem : Item, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("按下到抬起的位移小于该像素值视为点击，否则视为拖拽")]
    public float clickThreshold = 50f;

    private Vector2 pointerDownPosition;
    private bool isDragging;
    private bool clickHandled;

    /// <summary>
    /// 点击时触发的效果，子类按需重写。
    /// 比如资源类转换成经验/金币，生产者执行生产。
    /// </summary>
    protected virtual void OnClickFunc()
    {
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 移动过程中不响应点击/拖拽
        if (isItemMoving)
        {
            interactionBlocked = true;
            return;
        }

        interactionBlocked = false;
        pointerDownPosition = eventData.position;
        isDragging = false;
        clickHandled = false;

        // 按下即播放缩放到原始大小的动画（点击和拖拽共用这个入口）
        MouseScaleEvent scaleEvent = GetComponent<MouseScaleEvent>();
        if (scaleEvent != null)
            scaleEvent.SetScaleEnabled(false);
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        // Unity 的拖拽阈值很小（默认 10 像素），这里先不移动。
        // 等 OnDrag 里位移超过 clickThreshold 后再真正开始拖拽。
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (interactionBlocked)
            return;

        if (!isDragging)
        {
            if (Vector2.Distance(pointerDownPosition, eventData.position) < clickThreshold)
                return;

            // 位移够了，补上真正的拖拽开始逻辑：
            // 记录 lastSlot、断开槽位、换父物体
            isDragging = true;
            base.OnBeginDrag(eventData);
        }

        base.OnDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        // 被"移动中"拦截的交互，等 OnPointerUp 统一解除
        if (interactionBlocked)
            return;

        if (isDragging)
        {
            base.OnEndDrag(eventData);
            return;
        }

        // 拖拽事件已触发，但位移没超过阈值 → 视为点击
        OnClickFunc();
        clickHandled = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 松开后恢复聚焦缩放（鼠标仍悬停时会重新放大）
        MouseScaleEvent scaleEvent = GetComponent<MouseScaleEvent>();
        if (scaleEvent != null)
            scaleEvent.SetScaleEnabled(true);

        // 移动中按下的这次交互直接作废
        if (interactionBlocked)
        {
            interactionBlocked = false;
            return;
        }

        // 纯点击时 EventSystem 不会触发 OnBeginDrag / OnEndDrag，只能在这里处理
        if (isDragging || clickHandled)
            return;

        if (Vector2.Distance(pointerDownPosition, eventData.position) < clickThreshold)
            OnClickFunc();
    }
}
