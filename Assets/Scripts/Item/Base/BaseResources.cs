using UnityEngine;

/// <summary>
/// 资源类物品（经验 / 金币 / 能量）：
/// 可以正常合并升级，点击时把物品数值加进对应资源并回收自身。
/// 具体资源类型由所属合成链的 chainID 决定（与 ResourceType 一一对应）。
/// </summary>
public class BaseResources : ClickAndDragItem
{
    protected override void OnClickFunc()
    {
        if (MID == null || MC == null)
            return;

        int amount = MID.value > 0 ? MID.value : MID.tier;
        GameManager.Instance.CollectResource((ResourceType)MC.chainID, amount);

        // 收取后回收进对象池，PushItem 会断开槽位并清空定义
        GridSpawnManager.Instance.PushItem(this);
    }
}
