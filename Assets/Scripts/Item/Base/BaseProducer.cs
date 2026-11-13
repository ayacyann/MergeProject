using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生产者基类，保留 Producer / TimesProducer 共用的逻辑：
/// 点击生产、次数管理、随机产出与生成位置。
/// 次数耗尽后的具体行为由子类重写 OnTimesExhausted。
/// </summary>
public class BaseProducer : ClickAndDragItem
{
    public int currentTimes = 0;
    public ProducerMergeChain ProducerMC { get { return MC as ProducerMergeChain; } }

    public override void SetDefinitionInfo(MergeItemDefinition mid, MergeChain mc)
    {
        base.SetDefinitionInfo(mid, mc);

        if (ProducerMC == null || mid == null)
        {
            currentTimes = 0;
            return;
        }

        ProduceInfo info = ProducerMC.GetProduceInfo(mid.tier);
        currentTimes = info != null ? info.maxCount : 0;
    }

    // 点击生产者时触发生产
    protected override void OnClickFunc()
    {
        TryProduce();
    }

    protected virtual void TryProduce()
    {
        if (MID == null || MC == null || slot == null)
            return;

        if (currentTimes <= 0)
            return;

        ProducerMergeChain pMC = MC as ProducerMergeChain;
        if (pMC == null)
        {
            Debug.LogWarning($"{name} 的合成链不是 ProducerMergeChain");
            return;
        }

        if (!pMC.CanProduce(MID.tier))
        {
            Debug.Log($"{name} 等级 {MID.tier} 不足，需要 {pMC.produceLevel} 级才能生产");
            return;
        }

        // 先按权重抽一条产出链，再在链里随机取一个低于生产者等级的物品
        MergeChain produceChain = pMC.GetRandomProduceChain();
        if (produceChain == null)
            return;

        List<MergeItemDefinition> candidates = pMC.GetProduceCandidates(produceChain, MID.tier);
        if (candidates.Count == 0)
        {
            Debug.Log($"{produceChain.name} 没有低于 {MID.tier} 级的可产出物品");
            return;
        }

        MergeItemDefinition produced = candidates[Random.Range(0, candidates.Count)];

        // 距离当前槽位最近的空槽
        Slot targetSlot = GridSpawnManager.Instance.GetNearestEmptySlot(slot);
        if (targetSlot == null)
        {
            Debug.Log("没有空闲槽位，无法生成物品");
            return;
        }

        // 每次生产消耗 1 点能量（能量即点击生产的次数），不足时不生产也不扣次数
        if (!GameManager.Instance.SpendEnergy(1))
        {
            Debug.Log("能量不足，无法生产");
            return;
        }

        Item item = GridSpawnManager.Instance.CreateItemFromMID(produced, produceChain);
        if (item == null)
            return;

        // CreateItemFromMID 是直接赋字段的，这里再走一遍 SetDefinitionInfo，
        // 保证产出物本身是 Producer 时 currentTimes 等数据也能初始化
        item.SetDefinitionInfo(produced, produceChain);

        // 从生产者当前位置出发，由 Update 平滑移动到目标槽位
        item.transform.position = transform.position;
        item.ConnectSlot(targetSlot);

        // 槽位瞬间被占用时 ConnectSlot 会放弃，产出物会留在棋盘外，这里回收掉
        if (item.slot == null)
        {
            Debug.LogWarning($"产出物放不进槽位 {targetSlot.name}，已回收");
            GridSpawnManager.Instance.PushItem(item);
            return;
        }

        GameManager.Instance.TriggerOnChestSlotItemChange(item, true);

        // 成功生产一次，扣一次数
        currentTimes--;
        if (currentTimes <= 0)
        {
            currentTimes = 0;
            OnTimesExhausted();
        }
    }

    /// <summary>
    /// 次数耗尽时调用：Producer 进入冷却，TimesProducer 消耗回收。
    /// </summary>
    protected virtual void OnTimesExhausted()
    {
    }
}
