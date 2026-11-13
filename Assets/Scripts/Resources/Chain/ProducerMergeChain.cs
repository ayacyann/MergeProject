using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "NewProducerMergeChain",menuName = "MergeThings/MergeChain/Producer",order =1)]
public class ProducerMergeChain : MergeChain
{
    //是否为次数生产者
    public bool isTimesProducer;

    //可以生产出哪些合成链的物品，每个元素带概率权重
    public List<ProduceChainEntry> produceChains = new List<ProduceChainEntry>();

    //达到该等级才能点击生产，等级不足只能合并
    public int produceLevel = 4;

    //各级生产者对应的最大点击次数（等级需 >= produceLevel）
    public List<ProduceInfo> produceInfos = new List<ProduceInfo>();

    //所有权重之和，用于归一化
    public float TotalProduceWeight
    {
        get
        {
            if (produceChains == null)
            {
                return 0f;
            }

            return produceChains.Where((x) => x != null && x.chain && x.probability > 0f).Sum((x) => x.probability);
        }
    }

    //该等级是否可以点击生产
    public bool CanProduce(int tier)
    {
        return tier >= produceLevel;
    }

    //该等级的最大点击次数，没配置返回 0
    public int GetMaxProduceInfo(int tier)
    {
        ProduceInfo setting = produceInfos.Find((x) => x.tier == tier);
        return setting == null ? 0 : setting.maxCount;
    }

    public ProduceInfo GetProduceInfo(int tier)
    {
        return produceInfos.Find((x) => x.tier == tier);
    }

    //产出物品的等级上限：必须低于生产者自身等级且最大不会生成4级以上物品
    public int GetMaxProduceTier(int producerTier)
    {
        return Mathf.Min(3, producerTier - 1);
    }

    //某条产出链实际生效的概率（已归一化，0~1）
    public float GetProduceProbability(MergeChain chain)
    {
        float total = TotalProduceWeight;
        if (total <= 0f)
        {
            return 0f;
        }

        ProduceChainEntry entry = produceChains.Find((x) => x != null && x.chain == chain);
        return entry == null ? 0f : entry.probability / total;
    }

    //按权重随机抽一条产出链
    public MergeChain GetRandomProduceChain()
    {
        float total = TotalProduceWeight;
        if (total <= 0f)
        {
            return null;
        }

        float rd = Random.value * total;
        float sum = 0f;
        foreach (ProduceChainEntry entry in produceChains)
        {
            if (entry == null || !entry.chain || entry.probability <= 0f)
            {
                continue;
            }

            sum += entry.probability;
            if (rd <= sum)
            {
                return entry.chain;
            }
        }

        //浮点误差兜底
        return produceChains.Last((x) => x != null && x.chain && x.probability > 0f).chain;
    }

    //某条链里当前等级能产出的物品（等级低于生产者自身等级）
    public List<MergeItemDefinition> GetProduceCandidates(MergeChain chain, int producerTier)
    {
        List<MergeItemDefinition> result = new List<MergeItemDefinition>();
        int maxTier = GetMaxProduceTier(producerTier);
        if (!chain || chain.mergeItemDefinitions == null || maxTier < 1)
        {
            return result;
        }

        result.AddRange(chain.mergeItemDefinitions.Where((x) => x && x.tier <= maxTier));
        return result;
    }

    //随机产出一个物品：先按概率抽链，再在链内随机取物品，没有可产出物品时返回 null
    public MergeItemDefinition GetRandomProduceItem(int producerTier)
    {
        MergeChain chain = GetRandomProduceChain();
        if (!chain)
        {
            return null;
        }

        List<MergeItemDefinition> candidates = GetProduceCandidates(chain, producerTier);
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }
}

[System.Serializable]
public class ProduceChainEntry
{
    public MergeChain chain;
    [Tooltip("概率权重，多个元素时按总和归一化；只有一个元素时填 100 即可")]
    public float probability = 100f;
}

[System.Serializable]
public class ProduceInfo
{
    public int tier;        //生产者的物品等级
    public int maxCount;    //该等级下的最大点击次数
    public float timesInterval = 30.0f;     // 次数使用完之后需要等待的时间
}
