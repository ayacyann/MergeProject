using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Item : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerEnterHandler,IPointerExitHandler
{
    public Slot slot = null;
    public MergeItemDefinition MID;
    public MergeChain MC;
    public Image image;

    [Header("订单标记")]
    [Tooltip("被订单需要时显示的勾选图标，留空会自动查找子物体 Check")]
    public GameObject check;

    [Header("槽位跟随")]
    [Tooltip("向槽位移动的插值速度，越大越快（对应 gamedemo 的 damp）")]
    public float followSpeed = 8f;

    [Tooltip("与目标点的距离小于该值时视为到达")]
    public float arriveDistance = 20f;

    [Header("拖拽让位")]
    [Tooltip("悬停在同一被占用槽位多久后，把占用物品挪到最近空槽（秒）")]
    public float occupantMoveDelay = 0.25f;

    /// <summary>是否正在向槽位移动，移动过程中不能点击和拖拽</summary>
    public bool isItemMoving { get; private set; }

    private Slot lastSlot;
    private Slot hoveredSlot;

    // 让位计时：正在等待的槽位及其协程
    private Slot pendingMoveSlot;
    private Coroutine moveOccupantRoutine;

    // 本次指针交互是否因为物品正在移动而被拦截
    protected bool interactionBlocked;

    /// <summary>
    /// 是否是同一种物品（同 itemId）。
    /// 不要重写 Equals/GetHashCode：会干扰 Unity 编辑器与序列化内部的引用比较，
    /// 而且 MID 是可变的，合并升级后哈希值会跟着变。
    /// </summary>
    public bool IsSameItem(Item other)
    {
        if (other == null || MID == null || other.MID == null)
            return false;

        return MID.itemId == other.MID.itemId;
    }

    public virtual void SetDefinitionInfo(MergeItemDefinition mid, MergeChain mc)
    {
        MC = mc;
        MID = mid;
        image.sprite = mid.icon;
    }

    public virtual void ResetItem()
    {
        // 对象池复用时清掉上一轮遗留的交互/移动状态：
        // 残留的 interactionBlocked 会让 FollowSlot 直接 return，物品就停在槽位外
        StopOccupantMoveTimer();
        interactionBlocked = false;
        isItemMoving = false;
        hoveredSlot = null;
        lastSlot = null;

        MC = null;
        MID = null;
        image.sprite = null;
        SetCheck(false);
        DisconnectSlot();
    }

    /// <summary>显示 / 隐藏被订单需要的勾选标记</summary>
    public void SetCheck(bool show)
    {
        if (check == null)
        {
            Transform checkTransform = transform.Find("Check");
            if (checkTransform != null)
                check = checkTransform.gameObject;
        }

        if (check != null && check.activeSelf != show)
            check.SetActive(show);
    }

    protected virtual void Update()
    {
        FollowSlot();
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (isItemMoving)
        {
            interactionBlocked = true;
            return;
        }

        interactionBlocked = false;

        // 清掉可能残留的让位计时
        StopOccupantMoveTimer();

        // 拖拽期间取消聚焦放大，保持原始大小
        MouseScaleEvent scaleEvent = GetComponent<MouseScaleEvent>();
        if (scaleEvent != null)
            scaleEvent.SetScaleEnabled(false);

        lastSlot = slot;
        transform.SetParent(GridSpawnManager.Instance.transform);
        DisconnectSlot();
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (interactionBlocked)
            return;

        transform.position = eventData.position;
        hoveredSlot = GridSpawnManager.Instance.GetSlotAtScreenPosition(eventData);

        // 悬停被占用的槽位达到 occupantMoveDelay 秒后，占用者才让位
        UpdateOccupantMoveTimer();
    }

    /// <summary>
    /// 悬停在同一被占用槽位上开始计时，满 occupantMoveDelay 秒后
    /// 把占用物品挪到离该槽位最近的空槽。
    /// 中途换槽位、移到空槽或移出网格都会取消计时。
    /// 注意 OnDrag 只在鼠标移动时触发，所以用协程计时而不是累加 deltaTime。
    /// </summary>
    private void UpdateOccupantMoveTimer()
    {
        Item occupant = hoveredSlot != null ? hoveredSlot.target : null;

        bool canDisplace = occupant != null
            && occupant != this
            && !CanMergeWith(occupant);   // 只有真能合成时才不挪走，松手走合成

        if (!canDisplace)
        {
            StopOccupantMoveTimer();
            return;
        }

        // 同一个槽位已经在计时，继续等待
        if (pendingMoveSlot == hoveredSlot)
            return;

        // 换到另一个被占用的槽位，重新计时
        StopOccupantMoveTimer();
        pendingMoveSlot = hoveredSlot;
        moveOccupantRoutine = StartCoroutine(MoveOccupantAfterDelay(hoveredSlot, occupant));
    }

    /// <summary>
    /// 两个物品能否合成：同种物品且还没到该链的最高等级。
    /// 已经是最高等级时无法再升级，拖拽悬停就应该把占用者挪走，而不是等松手合成。
    /// </summary>
    private bool CanMergeWith(Item other)
    {
        if (!IsSameItem(other))
            return false;

        // 缺少合成链属于数据异常，按不可合成处理，避免悬停时卡住不让位
        if (MC == null)
            return false;

        return MID.tier < MC.MaxTier;
    }

    private IEnumerator MoveOccupantAfterDelay(Slot targetSlot, Item occupant)
    {
        yield return new WaitForSeconds(occupantMoveDelay);

        // 计时结束时再确认一次：槽位没变、占用者没变
        if (hoveredSlot == targetSlot && targetSlot.target == occupant)
        {
            Slot emptySlot = GridSpawnManager.Instance.GetNearestEmptySlot(targetSlot);
            if (emptySlot != null)
            {
                // ConnectSlot 会自动断开原槽位并平滑移动到新的空槽
                occupant.ConnectSlot(emptySlot);
            }
        }

        moveOccupantRoutine = null;
        pendingMoveSlot = null;
    }

    private void StopOccupantMoveTimer()
    {
        if (moveOccupantRoutine != null)
        {
            StopCoroutine(moveOccupantRoutine);
            moveOccupantRoutine = null;
        }

        pendingMoveSlot = null;
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        // 拖拽结束，取消还没执行的让位计时
        StopOccupantMoveTimer();

        // 恢复聚焦缩放（鼠标仍停在物品上时会重新放大）
        MouseScaleEvent scaleEvent = GetComponent<MouseScaleEvent>();
        if (scaleEvent != null)
            scaleEvent.SetScaleEnabled(true);

        if (interactionBlocked)
        {
            interactionBlocked = false;
            return;
        }

        Slot curSlot = hoveredSlot ?? lastSlot;
        if (hoveredSlot && hoveredSlot.target)
        {
            if (IsSameItem(hoveredSlot.target))
            {
                // PushItem 回收本物品时会走 ResetItem 清空 MID/MC/hoveredSlot，
                // 所以合成链和合成目标都要先缓存下来
                MergeChain mergeChain = MC;
                Item mergeTarget = hoveredSlot.target;

                GameManager.Instance.TriggerOnChestSlotItemsChange(new List<Item>() { this, mergeTarget },false);
                int tier = MID.tier + 1;
                MergeItemDefinition newMID = mergeChain.GetMergeItemDefinitionFromTier(tier);
                if (newMID != null)
                {
                    GridSpawnManager.Instance.PushItem(this);
                    mergeTarget.SetDefinitionInfo(newMID, mergeChain);
                    GameManager.Instance.TriggerOnChestSlotItemChange(mergeTarget,true);

                    // 合成成功：附近有空位时按概率再掉落一个资源物品
                    // 合成的本身就是资源物品时不再额外掉落，避免一次合成就白送一份资源
                    if (mergeChain.itemType != ItemType.Resource)
                        GridSpawnManager.Instance.TrySpawnMergeResource(mergeTarget.slot);
                    return;
                }
            }
            curSlot = lastSlot;
        }
        ConnectSlot(curSlot);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        slot?.ShowHighlight(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        slot?.ShowHighlight(false);
    }

    /// <summary>
    /// 连接到槽位。
    /// immediate = true 时直接落在槽位原点（用于 Start 里的初始生成），
    /// false 时从当前位置平滑移动到槽位（用于运行时生成/换槽）。
    /// </summary>
    public void ConnectSlot(Slot newSlot, bool immediate = false)
    {
        if (newSlot == null)
            return;

        if (slot == newSlot)
        {
            if (immediate)
                AttachToSlot(newSlot, true);
            return;
        }

        if (slot)
            DisconnectSlot();

        if (newSlot.target != null && newSlot.target != this)
        {
            Debug.LogWarning($"槽位 {newSlot.name} 已被占用");
            return;
        }

        newSlot.target = this;
        slot = newSlot;
        GridSpawnManager.Instance.OccupiedSlot(newSlot, true);
        AttachToSlot(newSlot, immediate);
    }

    public void DisconnectSlot()
    {
        if (slot)
        {
            GridSpawnManager.Instance.OccupiedSlot(slot, false);
            slot.target = null;
            slot = null;
        }
    }

    /// <summary>
    /// 挂到槽位下并铺满槽位。
    /// 使用 stretch 锚点 + 零偏移，尺寸完全由槽位决定，
    /// 不依赖布局时机（Start 里 slotRect.rect.size 可能还没算好），保证物品完整显示。
    /// 这里只改布局，不动 localScale，缩放完全交给 MouseScaleEvent。
    /// immediate 为 false 时保留当前世界位置，由 Update 平滑移动到目标点。
    /// </summary>
    private void AttachToSlot(Slot targetSlot, bool immediate)
    {
        RectTransform rt = (RectTransform)transform;
        RectTransform slotRect = (RectTransform)targetSlot.transform;

        Vector3 worldPosition = rt.position;

        // 与 FitToParent 一致：锚点拉伸铺满槽位，偏移归零
        rt.FitToParent(slotRect);

        if (immediate)
        {
            // 初始生成：直接落在槽位原点
            rt.anchoredPosition = Vector2.zero;
            isItemMoving = false;
            return;
        }

        // 把挂载前的世界位置换算成相对槽位中心的偏移，作为移动动画的起点
        Vector2 localPoint = slotRect.InverseTransformPoint(worldPosition);
        Vector2 anchorOffset = new Vector2(
            (0.5f - slotRect.pivot.x) * slotRect.rect.width,
            (0.5f - slotRect.pivot.y) * slotRect.rect.height);
        rt.anchoredPosition = localPoint - anchorOffset;

        isItemMoving = rt.anchoredPosition.magnitude > arriveDistance;
    }

    /// <summary>
    /// 每帧向 slot 的位置靠拢。距离小于 arriveDistance 时吸附到 0,0 并结束移动。
    /// 直接给 slot 赋新值也会在这里自动重新挂载。
    /// </summary>
    private void FollowSlot()
    {
        if (slot == null || interactionBlocked)
            return;

        RectTransform rt = (RectTransform)transform;
        RectTransform slotRect = (RectTransform)slot.transform;

        if (rt.parent != slotRect)
        {
            AttachToSlot(slot, false);
            return;
        }

        float distance = rt.anchoredPosition.magnitude;

        if (distance > arriveDistance)
        {
            isItemMoving = true;

            // 与 gamedemo 相同：lerp(pos, target, min(damp * delta, 0.5))
            float t = Mathf.Min(followSpeed * Time.deltaTime, 0.5f);
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, Vector2.zero, t);
        }
        else
        {
            rt.anchoredPosition = Vector2.zero;
            isItemMoving = false;
        }
    }
}
