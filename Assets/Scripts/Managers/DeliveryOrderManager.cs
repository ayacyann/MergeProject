using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 订单管理器：
/// - 按 Q 生成随机订单（1~2 种物品，每种 1~3 个）
/// - 维护当前所有订单
/// - 提供订单交付广播，让其他订单同步移除物品引用
/// </summary>
public class DeliveryOrderManager : Singleton<DeliveryOrderManager>
{
    [Header("订单")]
    public GameObject deliveryOrderPrefab;
    [Tooltip("订单生成的父物体，留空则用自身")]
    public Transform orderContainer;
    public ObjectPool<ItemOrder> deliveryOrderPool;

    [Header("随机订单")]
    [Tooltip("订单随机池，留空则使用 GridSpawnManager 的普通合成链")]
    public List<MergeChain> orderChains = new List<MergeChain>();
    [Min(1)] public int minItemTypes = 1;
    [Min(1)] public int maxItemTypes = 2;
    [Min(1)] public int minCount = 1;
    [Min(1)] public int maxCount = 3;

    public delegate void OrderDeliveredHandler(ItemOrder order, List<Item> deliveredItems);

    /// <summary>任意订单交付时广播自己的订单和交付的物品引用</summary>
    public event OrderDeliveredHandler OnOrderDeliveredEvent;

    private readonly List<ItemOrder> orders = new List<ItemOrder>();

    private void Awake()
    {
        deliveryOrderPool = new ObjectPool<ItemOrder>(
            createFunc: CreateOrderInstance,
            actionOnGet: OnGetOrder,
            actionOnRelease: OnReleaseOrder,
            actionOnDestroy: OnDestroyOrder,
            collectionCheck: true,
            defaultCapacity: 3,
            maxSize: 10
        );
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
            GenerateRandomOrder();
    }

    private void OnDestroy()
    {
        deliveryOrderPool?.Dispose();
        deliveryOrderPool = null;
        orders.Clear();
    }

    #region 对象池

    private ItemOrder CreateOrderInstance()
    {
        if (deliveryOrderPrefab == null)
        {
            Debug.LogError("DeliveryOrderManager 没有配置 deliveryOrderPrefab");
            return null;
        }

        Transform parent = orderContainer != null ? orderContainer : transform;
        GameObject go = Instantiate(deliveryOrderPrefab, parent);
        ItemOrder order = go.GetComponent<ItemOrder>();

        if (order == null)
            Debug.LogError($"{deliveryOrderPrefab.name} 上没有 ItemOrder 组件");

        return order;
    }

    private void OnGetOrder(ItemOrder order)
    {
        if (order != null)
            order.gameObject.SetActive(true);
    }

    private void OnReleaseOrder(ItemOrder order)
    {
        if (order != null)
            order.gameObject.SetActive(false);
    }

    private void OnDestroyOrder(ItemOrder order)
    {
        if (order)
            Destroy(order.gameObject);
    }

    #endregion

    /// <summary>生成一条随机订单</summary>
    public ItemOrder GenerateRandomOrder()
    {
        if (deliveryOrderPool == null)
            return null;

        List<OrderRequirement> requirements = BuildRandomRequirements();
        if (requirements.Count == 0)
            return null;

        ItemOrder order = deliveryOrderPool.Get();
        if (order == null)
            return null;

        orders.Add(order);
        order.Setup(requirements);
        return order;
    }

    /// <summary>订单交付完成后回收订单对象</summary>
    public void ReleaseOrder(ItemOrder order)
    {
        if (order == null)
            return;

        orders.Remove(order);

        if (deliveryOrderPool != null)
            deliveryOrderPool.Release(order);
        else
            Destroy(order.gameObject);
    }

    /// <summary>该物品是否还被任意一条订单引用</summary>
    public bool IsItemReferenced(Item item)
    {
        if (item == null)
            return false;

        foreach (ItemOrder order in orders)
        {
            if (order != null && !order.IsDelivered && order.References(item))
                return true;
        }

        return false;
    }

    /// <summary>广播订单交付，其他订单据此移除引用</summary>
    public void TriggerOnOrderDelivered(ItemOrder order, List<Item> deliveredItems)
    {
        OnOrderDeliveredEvent?.Invoke(order, deliveredItems);
    }

    private List<OrderRequirement> BuildRandomRequirements()
    {
        List<OrderRequirement> result = new List<OrderRequirement>();

        // 交付订单只允许普通物品，生产类 / 宝箱 / 资源类链都不参与
        List<MergeChain> chains = GetNormalOrderChains();
        if (chains.Count == 0)
            return result;

        int minTypes = Mathf.Max(1, minItemTypes);
        int maxTypes = Mathf.Max(minTypes, maxItemTypes);
        int types = Random.Range(minTypes, maxTypes + 1);

        int minNum = Mathf.Max(1, minCount);
        int maxNum = Mathf.Max(minNum, maxCount);

        HashSet<int> usedItemIds = new HashSet<int>();

        // 随机链和物品可能重复，限制尝试次数避免死循环
        int guard = 0;
        while (result.Count < types && guard++ < 100)
        {
            MergeChain chain = chains[Random.Range(0, chains.Count)];
            if (chain == null || chain.mergeItemDefinitions == null || chain.mergeItemDefinitions.Count == 0)
                continue;

            MergeItemDefinition mid = chain.GetRandomMergeItemDefinition();
            if (mid == null || usedItemIds.Contains(mid.itemId))
                continue;

            usedItemIds.Add(mid.itemId);
            result.Add(new OrderRequirement
            {
                mid = mid,
                count = Random.Range(minNum, maxNum + 1),
            });
        }

        return result;
    }

    /// <summary>
    /// 订单可用的合成链：只保留 itemType 为 Normal 的链。
    /// orderChains 留空时退回网格配置的普通链列表。
    /// </summary>
    private List<MergeChain> GetNormalOrderChains()
    {
        List<MergeChain> source = orderChains;
        if (source == null || source.Count == 0)
        {
            GridSpawnManager grid = GridSpawnManager.Instance;
            source = grid != null ? grid.itemMergeChain : null;
        }

        List<MergeChain> result = new List<MergeChain>();
        if (source == null)
            return result;

        foreach (MergeChain chain in source)
        {
            if (chain == null || chain.itemType != ItemType.Normal)
                continue;

            if (chain.mergeItemDefinitions == null || chain.mergeItemDefinitions.Count == 0)
                continue;

            result.Add(chain);
        }

        return result;
    }
}
