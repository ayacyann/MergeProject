using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class GameManager : Singleton<GameManager>
{
    public int exp = 0;
    public int ExpForLevel
    {
        get
        {
            return level * 25;
        }
    }
    public int level = 1;
    public int coins = 0;
    // 特殊金币，需要付费购买，暂留接口
    public int specialCoins = 0;

    // 体力/能量，由资源物品获取
    [FormerlySerializedAs("remainedTimes")]
    public int energy = 100;

    /// <summary>经验、金币、等级、能量等基础数值任一变化时广播，UI 订阅刷新</summary>
    public event Action OnStatsChanged;

    // 当棋盘中的槽位任意物品发生变动时触发
    // 比如发生合并，订单消耗或者生成新物品时
    // 使用列表方便合并和消耗时一次广播到位，避免多次触发，生成新物品可以传入单个物品进行多次触发
    public delegate void OnChestSlotItemsChange(List<Item> items,bool isAdd);

    public event OnChestSlotItemsChange OnChestSlotItemsChangeEvent;

    public void TriggerOnChestSlotItemsChange(List<Item> items, bool isAdd)
    {
        OnChestSlotItemsChangeEvent?.Invoke(items, isAdd);
    }

    public void TriggerOnChestSlotItemChange(Item item, bool isAdd)
    {
        OnChestSlotItemsChangeEvent?.Invoke(new List<Item>() { item },isAdd);
    }

    private void NotifyStatsChanged()
    {
        OnStatsChanged?.Invoke();
    }

    #region 数值增减

    /// <summary>增加经验，达到本级需求后连续升级</summary>
    public void AddExp(int amount)
    {
        if (amount <= 0)
            return;

        exp += amount;

        while (exp >= ExpForLevel)
        {
            exp -= ExpForLevel;
            level++;
        }

        NotifyStatsChanged();
    }

    /// <summary>增加金币</summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        coins += amount;
        NotifyStatsChanged();
    }

    /// <summary>消耗金币，数量不足时返回 false 且不扣除</summary>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0)
            return true;

        if (coins < amount)
            return false;

        coins -= amount;
        NotifyStatsChanged();
        return true;
    }

    /// <summary>增加特殊金币</summary>
    public void AddSpecialCoins(int amount)
    {
        if (amount <= 0)
            return;

        specialCoins += amount;
        NotifyStatsChanged();
    }

    /// <summary>消耗特殊金币，数量不足时返回 false 且不扣除</summary>
    public bool SpendSpecialCoins(int amount)
    {
        if (amount <= 0)
            return true;

        if (specialCoins < amount)
            return false;

        specialCoins -= amount;
        NotifyStatsChanged();
        return true;
    }

    /// <summary>增加能量</summary>
    public void AddEnergy(int amount)
    {
        if (amount <= 0)
            return;

        energy += amount;
        NotifyStatsChanged();
    }

    /// <summary>消耗能量，不足时返回 false 且不扣除</summary>
    public bool SpendEnergy(int amount)
    {
        if (amount <= 0)
            return true;

        if (energy < amount)
            return false;

        energy -= amount;
        NotifyStatsChanged();
        return true;
    }

    /// <summary>按资源类型增加对应数值（资源物品点击时调用）</summary>
    public void CollectResource(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.XP:
                AddExp(amount);
                break;
            case ResourceType.Coin:
                AddCoins(amount);
                break;
            case ResourceType.Energy:
                AddEnergy(amount);
                break;
            default:
                Debug.LogWarning($"未处理的资源类型：{type}");
                break;
        }
    }

    /// <summary>
    /// 发放订单奖励：金币直接累加，经验累加后按等级需求升级。
    /// </summary>
    public void AddRewards(int addCoins, int addExp)
    {
        AddCoins(addCoins);
        AddExp(addExp);
    }

    #endregion
}
