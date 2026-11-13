public enum ItemType
{
    Normal,         // 只能合并并交付
    Producer,       // 点击生产物品的，次数用完后进入冷却
    TimesProducer,       // 点击生产物品，次数用完后销毁或者转化为另一个物品
    Chest,            // 宝箱类，开启需要时间，同时可以开出Producer和Consumer以及Normal类型的物品
    Resource,       //资源类，比如经验、金币、特殊金币或者其他特殊道具
}

/// <summary>
/// 资源类型，数值与资源合成链的 chainID 一一对应（XPChain=0, CoinChain=1, EnergyChain=2）
/// </summary>
public enum ResourceType
{
    XP = 0,
    Coin = 1,
    Energy = 2,
}
