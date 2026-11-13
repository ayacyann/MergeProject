using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;

/// <summary>
/// 物品类型与预制体的对应配置，每种类型在 Awake 时创建一个独立的对象池
/// </summary>
[System.Serializable]
public class ItemPrefabEntry
{
    public ItemType itemType;
    public Item prefab;
}

/// <summary>
/// 合成掉落用的资源链配置，weight 为权重
/// </summary>
[System.Serializable]
public class MergeResourceEntry
{
    public MergeChain chain;
    [Min(0f)] public float weight = 1f;
}

public class GridSpawnManager : Singleton<GridSpawnManager>
{
    // 生成的物品放在对象池组件下方便管理
    public Transform poolTransform;
    [Header("对象池配置")]
    // 每种物品类型对应一个预制体，一个类型一个池子
    public List<ItemPrefabEntry> itemPrefabs = new List<ItemPrefabEntry>();

    public GridLayoutGroup itemContainer;
    public List<MergeChain> itemMergeChain;
    public List<MergeChain> specialMergeChain;

    [Header("合成掉落资源")]
    [Tooltip("每次合成成功且有空格时，生成资源物品的概率")]
    [Range(0f, 1f)] public float mergeResourceChance = 0.25f;
    [Tooltip("可掉落的资源链及权重，掉落时生成对应链的第 1 级")]
    public List<MergeResourceEntry> mergeResources = new List<MergeResourceEntry>();

    // 物品类型 -> 对象池
    private readonly Dictionary<ItemType, ObjectPool<Item>> pools = new Dictionary<ItemType, ObjectPool<Item>>();

    // 物品类型 -> 存放该类型物品的空子物体（挂在 ObjectPool 下）
    private readonly Dictionary<ItemType, Transform> poolContainers = new Dictionary<ItemType, Transform>();

    // 物品类型 -> 泛型创建方法
    private Dictionary<ItemType, Func<Item, Transform, Item>> itemFactories;

    // 脚本类型 -> 物品类型，回收时靠它从物品本身找到对应的池子
    private static readonly Dictionary<Type, ItemType> scriptTypeToItemType = new Dictionary<Type, ItemType>
    {
        { typeof(Item), ItemType.Normal },
        { typeof(Producer), ItemType.Producer },
        { typeof(TimesProducer), ItemType.TimesProducer },
        { typeof(Chest), ItemType.Chest },
        { typeof(BaseResources), ItemType.Resource },
    };

    public bool isContainerFull
    {
        get
        {
            return emptySlots.Count == 0;
        }
    }

    const int slotRow = 8;
    const int slotColume = 5;

    private Slot[,] slots = new Slot[slotRow, slotColume];

    private List<Slot> occupiedSlots = new List<Slot>(40);
    private List<Slot> emptySlots = new List<Slot>(40);

    void Awake()
    {
        itemFactories = new Dictionary<ItemType, Func<Item, Transform, Item>>
        {
            { ItemType.Normal, (prefab, parent) => CreateItem<Item>(prefab, parent) },
            { ItemType.Producer, (prefab, parent) => CreateItem<Producer>(prefab, parent) },
            { ItemType.TimesProducer, (prefab, parent) => CreateItem<TimesProducer>(prefab, parent) },
            { ItemType.Chest, (prefab, parent) => CreateItem<Chest>(prefab, parent) },
            { ItemType.Resource, (prefab, parent) => CreateItem<BaseResources>(prefab, parent) },
        };

        foreach (ItemPrefabEntry entry in itemPrefabs)
        {
            if (entry == null || !entry.prefab)
            {
                continue;
            }

            if (pools.ContainsKey(entry.itemType))
            {
                Debug.LogWarning($"重复配置的物品类型：{entry.itemType}，已忽略后者");
                continue;
            }

            // 每个类型一个空子物体，池里的物品都挂到它下面
            Transform container = CreatePoolContainer(entry.itemType);
            poolContainers.Add(entry.itemType, container);
            pools.Add(entry.itemType, CreatePool(entry.itemType, entry.prefab, container));
        }
    }

    // 在 ObjectPool 下创建该类型专用的空子物体
    private Transform CreatePoolContainer(ItemType itemType)
    {
        string containerName = $"{itemType}Pool";
        Transform existing = poolTransform.Find(containerName);
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject(containerName, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.FitToParent(poolTransform);
        return rt;
    }

    void Start()
    {
        int count = 0;
        foreach (Slot slot in itemContainer.GetComponentsInChildren<Slot>())
        {
            if (slot.isSpawnOnFirstLoad)
            {
                // 生成随机物体
                int rdInt = UnityEngine.Random.Range(0, 3);
                int[] rdIDs = new int[] { 3, 4, 5 };
                MergeChain mc = GetMergeChainFromId(rdIDs[rdInt]);
                Item item = CreateItemFromMID(GetRandomMergeItemDefinition(mc), mc);
                if (item)
                {
                    // Start 里初始生成：直接落在槽位原点，不做移动动画
                    item.ConnectSlot(slot, true);
                }
            }
            else
            {
                emptySlots.Add(slot);
            }
            int row = count / slotColume;
            int col = count % slotColume;
            slots[row, col] = slot;
            count++;
        }
        // 在空闲槽位上创建一个4级烘焙机
        MergeChain ovenChain = GetMergeChainFromId(7);
        for(int i = 0; i < 1; i++)
        {
            Item oven = CreateItemFromMID(ovenChain.GetMergeItemDefinitionFromTier(4), ovenChain);
            oven.ConnectSlot(GlobalUtils.PickRandomItemFromList<Slot>(emptySlots), true);
        }
        Debug.Log($"占用数量：{occupiedSlots.Count}");
        Debug.Log($"空闲数量：{emptySlots.Count}");
    }



    #region 对象池

    // 按配置创建某一类型的池子，createFunc 使用泛型创建方法
    private ObjectPool<Item> CreatePool(ItemType itemType, Item prefab, Transform container)
    {
        return new ObjectPool<Item>(
            createFunc: () => CreateItemOfType(itemType, prefab, container),
            actionOnGet: OnGetItem,
            actionOnRelease: OnReleaseItem,
            actionOnDestroy: OnDestroyItem,
            collectionCheck: true,
            defaultCapacity: 16,
            maxSize: 40
        );
    }

    private Item CreateItemOfType(ItemType itemType, Item prefab, Transform container)
    {
        if (itemFactories != null && itemFactories.TryGetValue(itemType, out Func<Item, Transform, Item> factory))
        {
            return factory(prefab, container);
        }

        Debug.LogError($"没有为物品类型 {itemType} 配置创建方法");
        return null;
    }

    // createFunc：池中没有闲置对象时才会调用，负责创建一个全新的 Item
    private T CreateItem<T>(Item prefab, Transform parent) where T : Item
    {
        GameObject go = Instantiate(prefab.gameObject, parent);
        T item = go.GetComponent<T>();
        if (item == null)
        {
            Debug.LogError($"{prefab.name} 上没有 {typeof(T).Name} 组件");
        }
        return item;
    }

    // 取出时显示
    private void OnGetItem(Item item)
    {
        item.gameObject.SetActive(true);
    }

    // 放回时清空状态、归位到对应类型的子物体下、隐藏
    private void OnReleaseItem(Item item)
    {
        item.ResetItem();

        // 回收时清掉冷却表现，避免带冷却状态复用
        ItemCooldown cooldown = item.GetComponent<ItemCooldown>();
        if (cooldown != null)
        {
            cooldown.ResetCooldown();
        }

        Transform container = GetPoolContainer(item);
        if (container != null)
        {
            item.transform.SetParent(container, false);
        }
        else
        {
            item.transform.SetParent(poolTransform, false);
        }

        item.gameObject.SetActive(false);
    }

    // 通过物品自身找到它所属类型的存放物体
    public Transform GetPoolContainer(Item item)
    {
        return GetPoolContainer(GetItemType(item));
    }

    public Transform GetPoolContainer(ItemType itemType)
    {
        return poolContainers.TryGetValue(itemType, out Transform container) ? container : null;
    }

    // 超过 maxSize 被淘汰时销毁
    private void OnDestroyItem(Item item)
    {
        if (item) Destroy(item.gameObject);
    }

    // 从脚本类型反查物品类型（先查自身再向上找基类）
    public ItemType GetItemType(Item item)
    {
        for (Type type = item.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            if (scriptTypeToItemType.TryGetValue(type, out ItemType itemType))
            {
                return itemType;
            }
        }

        Debug.LogError($"{item.name} 的脚本类型 {item.GetType().Name} 没有配置物品类型映射");
        return ItemType.Normal;
    }

    public ObjectPool<Item> GetPool(ItemType itemType)
    {
        if (pools.TryGetValue(itemType, out ObjectPool<Item> pool))
        {
            return pool;
        }

        Debug.LogError($"没有 {itemType} 类型的对象池");
        return null;
    }

    // 通过物品自身找到池子，回调里不需要再传物品类型
    public ObjectPool<Item> GetPool(Item item)
    {
        return GetPool(GetItemType(item));
    }

    public Item GetItem(ItemType itemType)
    {
        ObjectPool<Item> pool = GetPool(itemType);
        return pool?.Get();
    }

    public void PushItem(Item item)
    {
        if (!item)
        {
            return;
        }

        // 通知订单等监听者：该物品已经离开网格
        GameManager.Instance.TriggerOnChestSlotItemChange(item, false);

        if (item.slot)
        {
            item.DisconnectSlot();
        }

        ObjectPool<Item> pool = GetPool(item);
        if (pool != null)
        {
            pool.Release(item);
        }
        else
        {
            Destroy(item.gameObject);
        }
    }

    #endregion

    public Item CreateItemFromMID(MergeItemDefinition mid, MergeChain mc)
    {
        if (mid == null)
        {
            return null;
        }

        // 物品类型由合成链决定，从对应池子取出对应脚本的预制体
        ItemType itemType = mc ? mc.itemType : ItemType.Normal;
        Item item = GetItem(itemType);
        if (item == null)
        {
            return null;
        }

        item.SetDefinitionInfo(mid,mc);
        return item;
    }

    public MergeItemDefinition GetRandomMergeItemDefinition(MergeChain mc)
    {
        if (!mc)
        {
            Debug.LogError("没有对应阶段的合成链");
            return null;
        }
        return mc.GetRandomMergeItemDefinition();
    }

    /// <summary>
    /// 根据 chainID 查找对应的合成链（普通链优先，找不到再去特殊链里找）。
    /// </summary>
    public MergeChain GetMergeChainFromId(int chainId)
    {
        MergeChain mc = itemMergeChain.Find((x) => x.chainID == chainId);
        if (mc == null)
            mc = specialMergeChain.Find((x) => x.chainID == chainId);

        if (mc == null)
            Debug.LogError($"没有 chainID 为 {chainId} 的合成链");

        return mc;
    }

    /// <summary>
    /// 获取某个阶段下的所有合成链（普通链 + 特殊链）。
    /// </summary>
    public List<MergeChain> GetMergeChainsFromStage(int stage)
    {
        List<MergeChain> result = new List<MergeChain>();
        result.AddRange(itemMergeChain.FindAll((x) => x.stage == stage));
        result.AddRange(specialMergeChain.FindAll((x) => x.stage == stage));
        return result;
    }

    public void OccupiedSlot(Slot slot, bool isOccupied)
    {
        if (isOccupied)
        {
            if (!occupiedSlots.Contains(slot))
                occupiedSlots.Add(slot);
            emptySlots.Remove(slot);
        }
        else
        {
            occupiedSlots.Remove(slot);
            if (!emptySlots.Contains(slot))
                emptySlots.Add(slot);
        }
    }

    /// <summary>
    /// 获取当前网格上所有物品（订单创建时扫描用）。
    /// </summary>
    public List<Item> GetAllGridItems()
    {
        List<Item> result = new List<Item>();

        if (itemContainer == null)
            return result;

        foreach (Slot slot in itemContainer.GetComponentsInChildren<Slot>())
        {
            if (slot != null && slot.target != null)
                result.Add(slot.target);
        }

        return result;
    }

    /// <summary>
    /// 合成成功后尝试在附近空格生成一个资源物品：
    /// 必须先有空槽，再按 mergeResourceChance 概率触发，资源种类按权重随机。
    /// 返回生成的物品，没有生成时返回 null。
    /// </summary>
    public Item TrySpawnMergeResource(Slot fromSlot)
    {
        if (mergeResourceChance <= 0f || mergeResources == null || mergeResources.Count == 0)
            return null;

        if (fromSlot == null || isContainerFull)
            return null;

        if (UnityEngine.Random.value >= mergeResourceChance)
            return null;

        MergeChain chain = PickMergeResourceChain();
        if (chain == null || chain.mergeItemDefinitions == null || chain.mergeItemDefinitions.Count == 0)
            return null;

        MergeItemDefinition definition = chain.mergeItemDefinitions[0];
        if (definition == null)
            return null;

        Slot targetSlot = GetNearestEmptySlot(fromSlot);
        if (targetSlot == null)
            return null;

        Item item = CreateItemFromMID(definition, chain);
        if (item == null)
            return null;

        // 从合成位置出现，再由 Update 平滑移动到空槽
        item.transform.position = fromSlot.transform.position;
        item.ConnectSlot(targetSlot);

        // 槽位在这一瞬间被占用时 ConnectSlot 会直接放弃，物品会留在棋盘外，
        // 这里回收掉，避免出现悬空且点不到的物品
        if (item.slot == null)
        {
            Debug.LogWarning($"合成掉落的资源放不进槽位 {targetSlot.name}，已回收");
            PushItem(item);
            return null;
        }

        GameManager.Instance.TriggerOnChestSlotItemChange(item, true);
        return item;
    }

    // 按权重随机取一条资源链
    private MergeChain PickMergeResourceChain()
    {
        float total = 0f;
        foreach (MergeResourceEntry entry in mergeResources)
        {
            if (entry != null && entry.chain != null && entry.weight > 0f)
                total += entry.weight;
        }

        if (total <= 0f)
            return null;

        float rd = UnityEngine.Random.value * total;
        float sum = 0f;
        MergeChain last = null;

        foreach (MergeResourceEntry entry in mergeResources)
        {
            if (entry == null || entry.chain == null || entry.weight <= 0f)
                continue;

            last = entry.chain;
            sum += entry.weight;
            if (rd <= sum)
                return entry.chain;
        }

        return last;
    }

    /// <summary>
    /// 以 fromSlot 为中心向四周逐圈扩散，返回距离最近的一个空闲槽位。
    /// 距离按曼哈顿距离计算（只走上下左右，斜方向算 2 格）。
    /// 没有空闲槽位时返回 null。
    /// </summary>
    public Slot GetNearestEmptySlot(Slot fromSlot)
    {
        if (fromSlot == null || slots == null)
            return null;

        // 提前判断：全部占满就不用搜索了
        if (emptySlots.Count == 0)
            return null;

        if (!TryGetSlotIndex(fromSlot, out int startRow, out int startCol))
        {
            Debug.LogWarning("传入的槽位不在 slots 中");
            return null;
        }

        int rows = slots.GetLength(0);
        int cols = slots.GetLength(1);

        // 曼哈顿距离的最大值：从左上角到右下角
        int maxRadius = (rows - 1) + (cols - 1);

        // radius 就是当前搜索的圈数（曼哈顿距离）
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int row = startRow - radius; row <= startRow + radius; row++)
            {
                for (int col = startCol - radius; col <= startCol + radius; col++)
                {
                    // 只检查这一圈，圈内已在上一轮检查过
                    if (Mathf.Abs(row - startRow) + Mathf.Abs(col - startCol) != radius)
                        continue;

                    if (row < 0 || row >= rows || col < 0 || col >= cols)
                        continue;

                    Slot candidate = slots[row, col];
                    if (candidate != null && candidate.target == null)
                        return candidate;
                }
            }
        }

        return null;
    }

    // 查找某个槽位在二维数组中的行列索引
    private bool TryGetSlotIndex(Slot slot, out int row, out int col)
    {
        int rows = slots.GetLength(0);
        int cols = slots.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (slots[r, c] == slot)
                {
                    row = r;
                    col = c;
                    return true;
                }
            }
        }

        row = -1;
        col = -1;
        return false;
    }


    /// <summary>
    /// 根据拖拽/指针事件计算最近的槽位。
    /// 计算出的行列索引越界时返回 null。
    /// </summary>
    public Slot GetSlotAtScreenPosition(PointerEventData eventData)
    {
        if (eventData == null || itemContainer == null || slots == null)
            return null;

        RectTransform rt = itemContainer.transform as RectTransform;
        if (rt == null)
            return null;

        // 画布只有一个，直接取主相机。
        // 注意：Screen Space - Overlay 画布必须传 null，传相机反而会算错
        Canvas canvas = itemContainer.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : Camera.main;

        // 屏幕坐标 -> itemContainer 本地坐标（相对 pivot）
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, cam, out Vector2 localPoint))
            return null;

        Rect rect = rt.rect;
        RectOffset padding = itemContainer.padding;
        Vector2 cell = itemContainer.cellSize;
        Vector2 spacing = itemContainer.spacing;

        // 内容区左上角在本地坐标中的位置
        float startX = rect.xMin + padding.left;
        float startY = rect.yMax - padding.top;

        // 相对内容区左上角的偏移，向右、向下为正
        float dx = localPoint.x - startX;
        float dy = startY - localPoint.y;

        // 每个格子的步长（含间距）
        float stepX = cell.x + spacing.x;
        float stepY = cell.y + spacing.y;

        // 用格子中心做四舍五入，得到最近的列/行
        int col = Mathf.RoundToInt((dx - cell.x * 0.5f) / stepX);
        int row = Mathf.RoundToInt((dy - cell.y * 0.5f) / stepY);

        if (row < 0 || row >= slots.GetLength(0) ||
            col < 0 || col >= slots.GetLength(1))
            return null;

        return slots[row, col];
    }

    void OnDestroy()
    {
        foreach (ObjectPool<Item> pool in pools.Values)
        {
            pool?.Dispose();
        }
        pools.Clear();
    }
}
