using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 订单中的一条需求。
/// referencedItems 保存当前网格中满足该需求的物品引用，交付时直接使用，不再重新扫描。
/// </summary>
[System.Serializable]
public class OrderRequirement
{
    public MergeItemDefinition mid;
    public int count;

    [System.NonSerialized] public List<Item> referencedItems = new List<Item>();
}

/// <summary>
/// 一条交付订单：
/// - 创建时扫描整个网格，记录满足需求的物品引用，并给这些物品显示 Check 图标
/// - 监听 GameManager 的网格变化事件（合并 / 生成新物品）
/// - 满足数量后打开交付按钮，交付时回收物品并广播给其他订单
/// </summary>
public class ItemOrder : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject itemOrderPrefab;
    public Transform itemContainer;
    public Text coinText;
    public Text expText;
    public Button deliverButton;

    [Header("订单需求")]
    public List<OrderRequirement> requirements = new List<OrderRequirement>();

    public bool IsDelivered { get; private set; }
    public bool CanDeliver { get; private set; }
    public int RewardCoins { get; private set; }
    public int RewardExp { get; private set; }

    // 每条需求对应的 UI（图标 + 数量文本）
    private class RequirementView
    {
        public OrderRequirement requirement;
        public GameObject root;
        public Image icon;
        public Text countText;
    }

    private readonly List<RequirementView> views = new List<RequirementView>();

    private GameManager gameManager;
    private DeliveryOrderManager deliveryOrderManager;

    private void Awake()
    {
        ResolveUI();

        if (deliverButton != null)
        {
            deliverButton.onClick.RemoveListener(Deliver);
            deliverButton.onClick.AddListener(Deliver);
        }
    }

    private void OnEnable()
    {
        gameManager = GameManager.Instance;
        if (gameManager != null)
            gameManager.OnChestSlotItemsChangeEvent += OnGridItemsChanged;

        deliveryOrderManager = DeliveryOrderManager.Instance;
        if (deliveryOrderManager != null)
            deliveryOrderManager.OnOrderDeliveredEvent += OnOtherOrderDelivered;
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnChestSlotItemsChangeEvent -= OnGridItemsChanged;

        if (deliveryOrderManager != null)
            deliveryOrderManager.OnOrderDeliveredEvent -= OnOtherOrderDelivered;

        gameManager = null;
        deliveryOrderManager = null;
    }

    #region 初始化

    private void ResolveUI()
    {
        if (itemContainer == null)
            itemContainer = transform.Find("Background/Container");

        if (coinText == null)
            coinText = FindText("Background/ResourceContainer/Coin/Text (Legacy)");

        if (expText == null)
            expText = FindText("Background/ResourceContainer/Exp/Text (Legacy)");

        if (deliverButton == null)
        {
            Transform button = transform.Find("Background/Button");
            if (button != null)
                deliverButton = button.GetComponent<Button>();
        }
    }

    private Text FindText(string path)
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    /// <summary>由 DeliveryOrderManager 创建订单后调用</summary>
    public void Setup(List<OrderRequirement> newRequirements)
    {
        IsDelivered = false;
        CanDeliver = false;
        RewardCoins = 0;
        RewardExp = 0;

        // 过滤异常数据，避免需求数量和视图数量对不上
        requirements = new List<OrderRequirement>();
        if (newRequirements != null)
        {
            foreach (OrderRequirement req in newRequirements)
            {
                if (req != null && req.mid != null)
                    requirements.Add(req);
            }
        }

        // 复用池化订单上已有的视图：同一帧内 Destroy + Instantiate 会让新旧视图同时参与布局
        SyncViews();

        foreach (OrderRequirement req in requirements)
        {
            // 金币 = 等级 * 数量 * 5，经验 = 等级 * 数量，最后求和
            RewardCoins += req.mid.tier * req.count * 5;
            RewardExp += req.mid.tier * req.count;
        }

        if (coinText != null)
            coinText.text = "+" + RewardCoins;

        if (expText != null)
            expText.text = "+" + RewardExp;

        // 订单创建时扫描一次当前整个网格
        ScanGrid();
        RefreshView();
    }

    /// <summary>
    /// 按当前需求数量同步视图：
    /// 已有的复用（保留层级顺序），不足的补建，多余的隐藏。
    /// </summary>
    private void SyncViews()
    {
        for (int i = 0; i < requirements.Count; i++)
        {
            OrderRequirement req = requirements[i];
            if (req.referencedItems == null)
                req.referencedItems = new List<Item>();
            else
                req.referencedItems.Clear();

            RequirementView view = i < views.Count ? views[i] : CreateView(req);
            if (view == null)
                continue;

            view.requirement = req;

            if (view.root != null)
                view.root.SetActive(true);

            if (view.icon != null)
                view.icon.sprite = req.mid.icon;

            if (view.countText != null)
                view.countText.text = "0/" + req.count;
        }

        // 需求变少时隐藏多出来的视图，避免它们仍然出现在布局里
        for (int i = requirements.Count; i < views.Count; i++)
        {
            RequirementView view = views[i];
            if (view.root != null)
                view.root.SetActive(false);
        }
    }

    private RequirementView CreateView(OrderRequirement req)
    {
        if (itemOrderPrefab == null || itemContainer == null)
            return null;

        GameObject go = Instantiate(itemOrderPrefab, itemContainer);

        RequirementView view = new RequirementView
        {
            requirement = req,
            root = go,
            countText = go.GetComponentInChildren<Text>(true),
        };

        // 图标是内层带 sprite 的 Image（外层 Image 的 sprite 为空）
        Image[] images = go.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            if (img.sprite != null)
            {
                view.icon = img;
                break;
            }
        }

        if (view.icon == null)
        {
            Transform iconT = go.transform.Find("Image/Image");
            if (iconT != null)
                view.icon = iconT.GetComponent<Image>();
        }

        if (view.icon != null)
            view.icon.sprite = req.mid != null ? req.mid.icon : null;

        if (view.countText != null)
            view.countText.text = "0/" + req.count;

        views.Add(view);
        return view;
    }

    #endregion

    #region 引用维护

    private void ScanGrid()
    {
        if (GridSpawnManager.Instance == null)
            return;

        foreach (Item item in GridSpawnManager.Instance.GetAllGridItems())
            TryAddReference(item);
    }

    private bool TryAddReference(Item item)
    {
        if (item == null || item.MID == null)
            return false;

        bool added = false;

        foreach (OrderRequirement req in requirements)
        {
            if (req == null || req.mid == null)
                continue;

            if (req.mid.itemId != item.MID.itemId)
                continue;

            if (req.referencedItems.Contains(item))
                continue;

            req.referencedItems.Add(item);
            added = true;
        }

        if (added)
            item.SetCheck(true);

        return added;
    }

    private bool RemoveReference(Item item)
    {
        if (item == null)
            return false;

        bool removed = false;

        foreach (OrderRequirement req in requirements)
        {
            if (req != null && req.referencedItems != null && req.referencedItems.Remove(item))
                removed = true;
        }

        return removed;
    }

    /// <summary>该物品是否被本订单引用</summary>
    public bool References(Item item)
    {
        if (item == null)
            return false;

        foreach (OrderRequirement req in requirements)
        {
            if (req != null && req.referencedItems != null && req.referencedItems.Contains(item))
                return true;
        }

        return false;
    }

    /// <summary>清理已经被回收 / 销毁的物品引用</summary>
    private void PruneInvalidReferences(OrderRequirement req)
    {
        if (req == null || req.referencedItems == null)
            return;

        for (int i = req.referencedItems.Count - 1; i >= 0; i--)
        {
            Item item = req.referencedItems[i];
            if (item == null || item.MID == null || !item.gameObject.activeInHierarchy)
                req.referencedItems.RemoveAt(i);
        }
    }

    #endregion

    #region 事件

    /// <summary>合并 / 生成新物品时由 GameManager 广播</summary>
    private void OnGridItemsChanged(List<Item> items, bool isAdd)
    {
        if (IsDelivered || items == null)
            return;

        foreach (Item item in items)
        {
            if (item == null)
                continue;

            if (isAdd)
                TryAddReference(item);
            else
                RemoveReference(item);
        }

        // 勾选状态以“是否还有任意订单引用”为准
        foreach (Item item in items)
        {
            if (item == null)
                continue;

            item.SetCheck(deliveryOrderManager != null && deliveryOrderManager.IsItemReferenced(item));
        }

        RefreshView();
    }

    /// <summary>其他订单交付时，移除对已交付物品的引用</summary>
    private void OnOtherOrderDelivered(ItemOrder order, List<Item> deliveredItems)
    {
        if (order == this || IsDelivered || deliveredItems == null)
            return;

        bool changed = false;

        foreach (Item item in deliveredItems)
            changed |= RemoveReference(item);

        // 交付的物品已经被消耗，只有仍被引用时才重新显示勾选
        foreach (Item item in deliveredItems)
        {
            if (item == null)
                continue;

            item.SetCheck(deliveryOrderManager != null && deliveryOrderManager.IsItemReferenced(item));
        }

        if (changed)
            RefreshView();
    }

    private void RefreshView()
    {
        // 视图数量可能多于当前需求（池化复用保留了旧视图），只要够用即可
        bool canDeliver = requirements.Count > 0 && views.Count >= requirements.Count;

        for (int i = 0; i < requirements.Count && i < views.Count; i++)
        {
            RequirementView view = views[i];
            OrderRequirement req = requirements[i];
            PruneInvalidReferences(req);

            // 显示实际引用数量，超出需求时也照实显示（例如 2/1），不截断
            int have = req.referencedItems.Count;

            if (view.countText != null)
                view.countText.text = have + "/" + req.count;

            if (view.icon != null)
                view.icon.sprite = req.mid != null ? req.mid.icon : null;

            if (req.referencedItems.Count < req.count)
                canDeliver = false;
        }

        CanDeliver = canDeliver;

        if (deliverButton != null)
            deliverButton.interactable = CanDeliver;
    }

    #endregion

    #region 交付

    public void Deliver()
    {
        if (IsDelivered || !CanDeliver)
            return;

        // 交付前再清理一次失效引用
        foreach (OrderRequirement req in requirements)
            PruneInvalidReferences(req);

        foreach (OrderRequirement req in requirements)
        {
            if (req.referencedItems.Count < req.count)
            {
                RefreshView();
                return;
            }
        }

        List<Item> consumed = new List<Item>();
        List<Item> previousReferences = new List<Item>();

        foreach (OrderRequirement req in requirements)
        {
            foreach (Item item in req.referencedItems)
            {
                if (item != null && !previousReferences.Contains(item))
                    previousReferences.Add(item);
            }

            for (int i = 0; i < req.count; i++)
                consumed.Add(req.referencedItems[i]);

            req.referencedItems.Clear();
        }

        IsDelivered = true;
        CanDeliver = false;

        if (deliverButton != null)
            deliverButton.interactable = false;

        // 交付的物品先取消勾选
        foreach (Item item in consumed)
        {
            if (item != null)
                item.SetCheck(false);
        }

        // 广播给其他订单，让它们移除对这些物品的引用
        if (deliveryOrderManager != null)
            deliveryOrderManager.TriggerOnOrderDelivered(this, consumed);

        // 没被交付消耗的引用物品：只有还被其他订单需要时才保留勾选
        foreach (Item item in previousReferences)
        {
            if (item == null)
                continue;

            item.SetCheck(deliveryOrderManager != null && deliveryOrderManager.IsItemReferenced(item));
        }

        // 回收被交付的物品（PushItem 内部会广播移除并清空 MID）
        foreach (Item item in consumed)
        {
            if (item != null && GridSpawnManager.Instance != null)
                GridSpawnManager.Instance.PushItem(item);
        }

        // 发放奖励
        if (gameManager != null)
            gameManager.AddRewards(RewardCoins, RewardExp);

        if (deliveryOrderManager != null)
            deliveryOrderManager.ReleaseOrder(this);
    }

    #endregion
}
