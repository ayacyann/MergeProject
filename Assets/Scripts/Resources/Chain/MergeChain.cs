using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMergeChain",menuName = "MergeThings/MergeChain/Normal",order =0)]
public class MergeChain : ScriptableObject
{
    public int chainID;
    //同步关联当前的阶段等级，数值越高生成越靠外越靠后才会解锁
    public int stage;

    //物品类型，从物品资源迁移到合成链，同一条链下的物品共用
    public ItemType itemType = ItemType.Normal;

    public List<MergeItemDefinition> mergeItemDefinitions;

    public int MaxTier
    {
        get
        {
            return mergeItemDefinitions == null ? 0 : mergeItemDefinitions.Count;
        }
    }

    public MergeItemDefinition GetMergeItemDefinitionFromTier(int tier)
    {
        if (tier >= 1 && tier <= MaxTier)
        {
            return mergeItemDefinitions[tier - 1];
        }
        else
        {
            Debug.Log("等级溢出");
            return null;
        }
    }

    public MergeItemDefinition GetRandomMergeItemDefinition()
    {
        int maxTier = mergeItemDefinitions.Max((x) => x.tier);
        int rdTier = Random.Range(1, maxTier);
        return GetMergeItemDefinitionFromTier(rdTier);
    }
}
