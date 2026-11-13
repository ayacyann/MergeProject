using System.Collections.Generic;
using UnityEngine;

public static class GlobalUtils
{
    public static T PickRandomItemFromList<T>(List<T> list)
    {
        return list[Random.Range(0, list.Count)];
    }
}