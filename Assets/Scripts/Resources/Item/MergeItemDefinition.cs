using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMergeItem", menuName = "MergeThings/MergeItemDefinition/Normal", order = 0)]
public class MergeItemDefinition : ScriptableObject
{
    public int itemId;
    public string displayName;
    public string displayDes;

    [Min(1)]
    public int tier = 1;        //第几层合成

    public Sprite icon;
    public int sellPrice;

    [Tooltip("资源类物品点击后增加的数值（经验/金币/能量），普通物品不使用")]
    public int value;
}
