/// <summary>
/// 次数生产者：次数用完后消耗自身，回收进对应类型的对象池。
/// </summary>
public class TimesProducer : BaseProducer
{
    protected override void OnTimesExhausted()
    {
        // PushItem 会断开槽位，并按物品类型回收进对应的池子容器
        GridSpawnManager.Instance.PushItem(this);
    }
}
