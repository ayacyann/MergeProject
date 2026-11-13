using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 侧边宝箱背包：负责宝箱数量的显示与取出。
/// 点击 OpenAndClose 左右滑动背包，点击某个宝箱在网格空位生成对应等级的宝箱并扣数量。
/// </summary>
public class ChestMenuUI : MonoBehaviour
{
    [Header("UI 引用（留空自动查找）")]
    public Button openAndCloseButton;
    [Tooltip("宝箱条目，留空自动收集子物体上的 ChestInMenu")]
    public ChestInMenu[] chestEntries;

    [Header("背包滑动")]
    [Tooltip("打开背包时 SideButtons 的本地 X")]
    public float openX = 450f;
    [Tooltip("关闭背包时 SideButtons 的本地 X")]
    public float closeX = 630f;
    [Tooltip("滑动时长（秒）")]
    public float slideDuration = 0.3f;

    // 条目 -> 数量角标上的 Image，缓存避免每次刷新都 Find
    private readonly Dictionary<ChestInMenu, Image> addImages = new Dictionary<ChestInMenu, Image>();

    private GameManager gameManager;
    private bool isPanelOpen = true;
    // 上一次看到的等级，用于从 OnStatsChanged 里识别升级
    private int lastLevel;

    private void Awake()
    {
        ResolveRefs();
        WireButtons();

        // 按当前 X 判断背包是开着还是关着
        float x = transform.localPosition.x;
        isPanelOpen = Mathf.Abs(x - openX) < Mathf.Abs(x - closeX);
    }

    private void OnEnable()
    {
        gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.OnStatsChanged += OnStatsChanged;
            lastLevel = gameManager.level;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnStatsChanged -= OnStatsChanged;

        gameManager = null;

        // 面板被隐藏时别让滑动动画继续挂着
        transform.DOKill();
    }

    private void Start()
    {
        // 进入游戏时每种宝箱各送 1 个
        foreach (ChestInMenu entry in chestEntries)
        {
            if (entry != null && entry.mid != null)
                AddChest(entry.mid.tier, 1);
        }
    }

    private void ResolveRefs()
    {
        if (openAndCloseButton == null)
        {
            Transform t = transform.Find("OpenAndClose");
            if (t != null)
                openAndCloseButton = t.GetComponent<Button>();
        }

        if (chestEntries == null || chestEntries.Length == 0)
            chestEntries = GetComponentsInChildren<ChestInMenu>(true);

        addImages.Clear();
        foreach (ChestInMenu entry in chestEntries)
        {
            if (entry == null || addImages.ContainsKey(entry))
                continue;

            Transform add = entry.transform.Find("Add");
            addImages.Add(entry, add != null ? add.GetComponent<Image>() : null);
        }
    }

    private void WireButtons()
    {
        if (openAndCloseButton != null)
        {
            openAndCloseButton.onClick.RemoveListener(ToggleChestPanel);
            openAndCloseButton.onClick.AddListener(ToggleChestPanel);
        }

        foreach (ChestInMenu entry in chestEntries)
        {
            if (entry == null)
                continue;

            // 宝箱图标上的 Button（ChestMask/Image）
            Transform chestIcon = entry.transform.Find("ChestMask/Image");
            Button chestButton = chestIcon != null ? chestIcon.GetComponent<Button>() : null;
            if (chestButton == null)
                continue;

            ChestInMenu target = entry;
            chestButton.onClick.AddListener(() => ClaimChest(target));
        }
    }

    /// <summary>打开 / 关闭宝箱背包：在 openX 与 closeX 之间滑动</summary>
    public void ToggleChestPanel()
    {
        SetChestPanelOpen(!isPanelOpen);
    }

    /// <summary>按状态把 SideButtons 滑到开/关位置</summary>
    public void SetChestPanelOpen(bool open)
    {
        isPanelOpen = open;

        float targetX = isPanelOpen ? openX : closeX;

        // 连续点击时打断上一段动画，从当前位置继续滑
        transform.DOKill();
        transform.DOLocalMoveX(targetX, slideDuration).SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// 取出宝箱：数量大于 0 且网格有空位时，在空位生成对应等级的宝箱并扣掉 1 个。
    /// </summary>
    public void ClaimChest(ChestInMenu entry)
    {
        if (entry == null)
            return;

        if (entry.count <= 0)
        {
            Debug.Log($"{GetChestName(entry)} 数量不足，无法取出");
            return;
        }

        GridSpawnManager grid = GridSpawnManager.Instance;
        if (grid == null)
            return;

        if (grid.isContainerFull)
        {
            Debug.Log("网格已满，无法取出宝箱");
            return;
        }

        Slot emptySlot = FindEmptySlot();
        if (emptySlot == null)
        {
            Debug.Log("网格没有空位，无法取出宝箱");
            return;
        }

        Item item = grid.CreateItemFromMID(entry.mid, entry.mc);
        if (item == null)
            return;

        // 直接落在槽位上，不走移动动画
        item.ConnectSlot(emptySlot, true);
        GameManager.Instance.TriggerOnChestSlotItemChange(item, true);

        entry.count--;
        RefreshEntry(entry);
    }

    /// <summary>
    /// 添加宝箱的对外钩子：按等级增加数量，供奖励、升级等系统调用。
    /// </summary>
    public bool AddChest(int tier, int amount = 1)
    {
        if (amount <= 0)
            return false;

        ChestInMenu entry = FindEntry(tier);
        if (entry == null)
        {
            Debug.LogWarning($"没有 {tier} 级宝箱的条目");
            return false;
        }

        entry.count += amount;
        RefreshEntry(entry);
        return true;
    }

    /// <summary>查询某个等级的宝箱数量</summary>
    public int GetChestCount(int tier)
    {
        ChestInMenu entry = FindEntry(tier);
        return entry != null ? entry.count : 0;
    }

    private void OnStatsChanged()
    {
        if (gameManager == null)
            return;

        // 一次可能连升多级，逐级发放
        while (lastLevel < gameManager.level)
        {
            lastLevel++;
            AddRandomChestForLevel(lastLevel);
        }
    }

    /// <summary>升级奖励：随机给一个等级不高于当前等级的宝箱</summary>
    private void AddRandomChestForLevel(int level)
    {
        List<int> tiers = new List<int>();
        foreach (ChestInMenu entry in chestEntries)
        {
            if (entry != null && entry.mid != null && entry.mid.tier <= level)
                tiers.Add(entry.mid.tier);
        }

        if (tiers.Count == 0)
            return;

        int tier = tiers[Random.Range(0, tiers.Count)];
        if (AddChest(tier, 1))
            Debug.Log($"升级到 {level} 级：获得 {tier} 级宝箱，当前数量 {GetChestCount(tier)}");
    }

    /// <summary>刷新条目的角标图片与数量文本：有数量显示角标+数字，没有则显示空图标</summary>
    private void RefreshEntry(ChestInMenu entry)
    {
        if (entry == null)
            return;

        bool hasCount = entry.count > 0;

        if (addImages.TryGetValue(entry, out Image addImage) && addImage != null)
        {
            Sprite sprite = hasCount ? entry.addHasNumSprite : entry.addEmptySprite;
            if (sprite != null)
                addImage.sprite = sprite;
        }

        if (entry.addText != null)
        {
            entry.addText.text = entry.count.ToString();
            if (entry.addText.gameObject.activeSelf != hasCount)
                entry.addText.gameObject.SetActive(hasCount);
        }
    }

    private ChestInMenu FindEntry(int tier)
    {
        foreach (ChestInMenu entry in chestEntries)
        {
            if (entry != null && entry.mid != null && entry.mid.tier == tier)
                return entry;
        }

        return null;
    }

    private Slot FindEmptySlot()
    {
        GridSpawnManager grid = GridSpawnManager.Instance;
        if (grid == null || grid.itemContainer == null)
            return null;

        foreach (Slot slot in grid.itemContainer.GetComponentsInChildren<Slot>())
        {
            if (slot != null && slot.target == null)
                return slot;
        }

        return null;
    }

    private static string GetChestName(ChestInMenu entry)
    {
        return entry.mid != null ? entry.mid.name : entry.name;
    }
}
